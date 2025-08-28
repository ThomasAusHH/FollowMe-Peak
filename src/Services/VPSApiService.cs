using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Logging;
using Newtonsoft.Json;
using FollowMePeak.Models;
using FollowMePeak.Utils;

namespace FollowMePeak.Services
{
    public class VPSApiService
    {
        private readonly ManualLogSource _logger;
        private readonly ServerConfig _config;
        private readonly MonoBehaviour _coroutineRunner;

        public VPSApiService(ManualLogSource logger, ServerConfig config, MonoBehaviour coroutineRunner)
        {
            _logger = logger;
            _config = config;
            _coroutineRunner = coroutineRunner;
        }

        public bool IsServerReachable { get; private set; } = false;
        public DateTime LastHealthCheck { get; private set; } = DateTime.MinValue;

        // Health Check
        public void CheckServerHealth(System.Action<bool> callback = null)
        {
            _coroutineRunner.StartCoroutine(CheckServerHealthCoroutine(callback));
        }

        private IEnumerator CheckServerHealthCoroutine(System.Action<bool> callback)
        {
            string url = $"{_config.BaseUrl}/api/health";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = _config.TimeoutSeconds;
                request.SetRequestHeader("X-API-Key", _config.ApiKey);
                
                yield return request.SendWebRequest();
                
                LastHealthCheck = DateTime.Now;
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<HealthResponse>(request.downloadHandler.text, CommonJsonSettings.Default);
                        IsServerReachable = response.Status == "healthy";
                        
                        if (IsServerReachable)
                        {
                            _logger.LogInfo($"Server health check successful. {response.Stats.TotalClimbs} climbs in database.");
                        }
                        else
                        {
                            _logger.LogWarning($"Server unhealthy: {response.Status}");
                            IsServerReachable = false;
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"Failed to parse health response: {e.Message}");
                        IsServerReachable = false;
                    }
                }
                else
                {
                    _logger.LogError($"Health check failed: {request.error}");
                    IsServerReachable = false;
                }
                
                callback?.Invoke(IsServerReachable);
            }
        }

        // Upload Climb
        public void UploadClimb(ClimbData climbData, string levelId, System.Action<bool, string> callback)
        {
            if (!_config.EnableCloudSync)
            {
                callback?.Invoke(false, "Cloud sync disabled");
                return;
            }

            if (!_config.CanUpload())
            {
                callback?.Invoke(false, "Upload rate limit exceeded");
                return;
            }

            _coroutineRunner.StartCoroutine(UploadClimbCoroutine(climbData, levelId, callback));
        }

        private IEnumerator UploadClimbCoroutine(ClimbData climbData, string levelId, System.Action<bool, string> callback)
        {
            // Input validation
            if (!InputValidator.IsValidLevelId(levelId))
            {
                callback?.Invoke(false, "Invalid level ID format");
                yield break;
            }
            
            if (!InputValidator.IsValidPointCount(climbData.Points?.Count ?? 0))
            {
                callback?.Invoke(false, "Invalid climb data - point count out of range");
                yield break;
            }
            
            string url = $"{_config.BaseUrl}/api/climbs";
            
            // Convert ClimbData to server format and reduce points if necessary
            var apiPoints = climbData.Points;
            apiPoints = ReducePointsIfNeeded(apiPoints);
            
            int clampedAscentLevel = InputValidator.ClampAscentLevel(climbData.AscentLevel);
            _logger.LogInfo($"Upload data: AscentLevel from ClimbData: {climbData.AscentLevel}, Clamped: {clampedAscentLevel}");
            
            var uploadData = new
            {
                levelId = levelId,
                playerName = InputValidator.SanitizePlayerName(_config.PlayerName),
                biomeName = InputValidator.SanitizeBiomeName(climbData.BiomeName),
                duration = InputValidator.ClampDuration(climbData.DurationInSeconds),
                points = apiPoints,
                isSuccessful = true, // Will be determined by validation logic
                tags = new string[] { }, // Can be extended later
                ascentLevel = clampedAscentLevel // Clamp to -1 to 8 range
            };

            string json = JsonConvert.SerializeObject(uploadData, CommonJsonSettings.Compact);
            
            // Find ascentLevel in JSON for debugging
            int ascentIndex = json.IndexOf("\"ascentLevel\":");
            if (ascentIndex >= 0)
            {
                int endIndex = Math.Min(ascentIndex + 50, json.Length);
                string ascentPart = json.Substring(ascentIndex, endIndex - ascentIndex);
                _logger.LogInfo($"Upload JSON contains ascentLevel: {ascentPart}");
            }
            else
            {
                _logger.LogError("Upload JSON does NOT contain ascentLevel field!");
            }
            
            _logger.LogInfo($"Upload JSON payload (first 500 chars): {json.Substring(0, Math.Min(500, json.Length))}...");
            
            // Check payload size before upload
            if (json.Length > GetMaxPayloadSize())
            {
                string error = $"Payload too large ({json.Length} bytes). Consider reducing climb complexity.";
                _logger.LogError(error);
                callback?.Invoke(false, error);
                yield break;
            }
            
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-API-Key", _config.ApiKey);
                request.timeout = _config.TimeoutSeconds;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<ApiResponse<ClimbUploadResponse>>(request.downloadHandler.text, CommonJsonSettings.Default);
                        
                        if (response.Success)
                        {
                            _config.IncrementUploadCount();
                            _logger.LogInfo($"Climb uploaded successfully: {response.Data.ClimbId}");
                            callback?.Invoke(true, response.Data.ClimbId);
                        }
                        else
                        {
                            _logger.LogError($"Upload failed: {response.Error}");
                            callback?.Invoke(false, response.Error);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"Failed to parse upload response: {e.Message}");
                        callback?.Invoke(false, "Parse error");
                    }
                }
                else
                {
                    string error = $"Upload request failed: {request.error} (HTTP {request.responseCode})";
                    _logger.LogError(error);
                    callback?.Invoke(false, error);
                }
            }
        }

        // Download Climbs for Level
        public void DownloadClimbs(string levelId, System.Action<List<ClimbData>, string, ClimbListMeta> callback, int limit = 10, int offset = 0, 
            string playerName = "", string biomeName = "", string peakCode = "", string sortBy = "created_at", string sortOrder = "desc")
        {
            if (!_config.EnableCloudSync)
            {
                callback?.Invoke(new List<ClimbData>(), "Cloud sync disabled", null);
                return;
            }

            _coroutineRunner.StartCoroutine(DownloadClimbsCoroutine(levelId, callback, limit, offset, playerName, biomeName, peakCode, sortBy, sortOrder));
        }

        private IEnumerator DownloadClimbsCoroutine(string levelId, System.Action<List<ClimbData>, string, ClimbListMeta> callback, int limit = 10, int offset = 0,
            string playerName = "", string biomeName = "", string peakCode = "", string sortBy = "created_at", string sortOrder = "desc")
        {
            // Input validation
            if (!InputValidator.IsValidLevelId(levelId))
            {
                callback?.Invoke(new List<ClimbData>(), "Invalid level ID format", null);
                yield break;
            }
            
            // Build URL with search and sort parameters
            var urlBuilder = new StringBuilder($"{_config.BaseUrl}/api/climbs/{levelId}?limit={limit}&offset={offset}");
            
            if (!string.IsNullOrEmpty(playerName))
                urlBuilder.Append($"&player_name={UnityEngine.Networking.UnityWebRequest.EscapeURL(playerName)}");
            
            if (!string.IsNullOrEmpty(biomeName))
                urlBuilder.Append($"&biome_name={UnityEngine.Networking.UnityWebRequest.EscapeURL(biomeName)}");
            
            if (!string.IsNullOrEmpty(peakCode))
                urlBuilder.Append($"&peak_code={UnityEngine.Networking.UnityWebRequest.EscapeURL(peakCode)}");
            
            urlBuilder.Append($"&sort_by={sortBy}&sort_order={sortOrder}");
            
            string url = urlBuilder.ToString();
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = _config.TimeoutSeconds;
                request.SetRequestHeader("X-API-Key", _config.ApiKey);
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<ClimbListResponse>(request.downloadHandler.text, CommonJsonSettings.Default);
                        var climbs = new List<ClimbData>();
                        
                        if (response.Data != null)
                        {
                            foreach (var serverClimb in response.Data)
                            {
                                climbs.Add(serverClimb.ToClimbData());
                            }
                        }
                        
                        _logger.LogInfo($"Downloaded {climbs.Count} climbs for level {levelId}");
                        callback?.Invoke(climbs, null, response.Meta);
                    }
                    catch (Exception e)
                    {
                        string error = $"Failed to parse download response: {e.Message}";
                        _logger.LogError(error);
                        callback?.Invoke(new List<ClimbData>(), error, null);
                    }
                }
                else if (request.responseCode == 404)
                {
                    // No climbs found for this level
                    _logger.LogInfo($"No climbs found for level {levelId}");
                    callback?.Invoke(new List<ClimbData>(), null, null);
                }
                else
                {
                    string error = $"Download request failed: {request.error} (HTTP {request.responseCode})";
                    _logger.LogError(error);
                    callback?.Invoke(new List<ClimbData>(), error, null);
                }
            }
        }

        // Search Climb by Peak Code
        public void SearchClimbByPeakCode(string peakCode, System.Action<ClimbData, string> callback)
        {
            if (!_config.EnableCloudSync)
            {
                callback?.Invoke(null, "Cloud sync disabled");
                return;
            }

            // Sanitize and validate peak code
            string sanitizedPeakCode = InputValidator.SanitizePeakCode(peakCode);
            if (!InputValidator.IsValidPeakCode(sanitizedPeakCode))
            {
                callback?.Invoke(null, "Peak code must be 8 alphanumeric characters");
                return;
            }

            _coroutineRunner.StartCoroutine(SearchClimbByPeakCodeCoroutine(sanitizedPeakCode, callback));
        }

        private IEnumerator SearchClimbByPeakCodeCoroutine(string peakCode, System.Action<ClimbData, string> callback)
        {
            string url = $"{_config.BaseUrl}/api/climbs/search/{peakCode}";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = _config.TimeoutSeconds;
                request.SetRequestHeader("X-API-Key", _config.ApiKey);
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<ClimbSearchResponse>(request.downloadHandler.text, CommonJsonSettings.Default);
                        
                        if (response.Success && response.Data != null)
                        {
                            var climbData = response.Data.ToClimbData();
                            _logger.LogInfo($"Found climb with peak code {peakCode}: {climbData.GetDisplayName()}");
                            callback?.Invoke(climbData, null);
                        }
                        else
                        {
                            _logger.LogInfo($"No climb found with peak code {peakCode}");
                            callback?.Invoke(null, "Climb not found");
                        }
                    }
                    catch (Exception e)
                    {
                        string error = $"Failed to parse search response: {e.Message}";
                        _logger.LogError(error);
                        callback?.Invoke(null, error);
                    }
                }
                else if (request.responseCode == 404)
                {
                    _logger.LogInfo($"No climb found with peak code {peakCode}");
                    callback?.Invoke(null, "Climb not found");
                }
                else
                {
                    string error = $"Search request failed: {request.error} (HTTP {request.responseCode})";
                    _logger.LogError(error);
                    callback?.Invoke(null, error);
                }
            }
        }

        // Get Server Statistics
        public void GetServerStats(System.Action<string, string> callback)
        {
            _coroutineRunner.StartCoroutine(GetServerStatsCoroutine(callback));
        }

        private IEnumerator GetServerStatsCoroutine(System.Action<string, string> callback)
        {
            string url = $"{_config.BaseUrl}/api/stats";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = _config.TimeoutSeconds;
                request.SetRequestHeader("X-API-Key", _config.ApiKey);
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(request.downloadHandler.text, null);
                }
                else
                {
                    string error = $"Stats request failed: {request.error}";
                    _logger.LogError(error);
                    callback?.Invoke(null, error);
                }
            }
        }

        // Helper method to get maximum allowed payload size (in bytes)
        private int GetMaxPayloadSize()
        {
            // Most servers have a default limit of around 1-4MB for POST requests
            // We'll use 2MB as a safe default, but make it configurable
            return 2 * 1024 * 1024; // 2MB
        }

        // Helper method to reduce points if path is too complex
        private List<Vector3> ReducePointsIfNeeded(List<Vector3> points)
        {
            const int maxPoints = 8000; // Reasonable limit for most paths
            
            if (points.Count <= maxPoints)
                return points;

            _logger.LogWarning($"Path has {points.Count} points, reducing to {maxPoints} for upload");

            // Use simple decimation - take every nth point to reduce complexity
            var reducedPoints = new List<Vector3>();
            float step = (float)points.Count / maxPoints;
            
            for (int i = 0; i < maxPoints; i++)
            {
                int index = Mathf.RoundToInt(i * step);
                if (index < points.Count)
                    reducedPoints.Add(points[index]);
            }

            // Always include the last point to maintain path integrity
            if (reducedPoints.Count > 0 && !reducedPoints.Last().Equals(points.Last()))
            {
                reducedPoints[reducedPoints.Count - 1] = points.Last();
            }

            return reducedPoints;
        }
    }
}
