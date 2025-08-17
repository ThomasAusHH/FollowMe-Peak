using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Logging;
using Newtonsoft.Json;
using PeakPathfinder.Models;

namespace PeakPathfinder.Services
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
                
                yield return request.SendWebRequest();
                
                LastHealthCheck = DateTime.Now;
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<HealthResponse>(request.downloadHandler.text);
                        IsServerReachable = response.Status == "healthy";
                        
                        if (IsServerReachable)
                        {
                            _logger.LogInfo($"Server health check successful. {response.Stats.TotalPaths} paths in database.");
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

        // Upload Path
        public void UploadPath(PathData pathData, string levelId, System.Action<bool, string> callback)
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

            _coroutineRunner.StartCoroutine(UploadPathCoroutine(pathData, levelId, callback));
        }

        private IEnumerator UploadPathCoroutine(PathData pathData, string levelId, System.Action<bool, string> callback)
        {
            string url = $"{_config.BaseUrl}/api/paths";
            
            // Convert PathData to server format
            var apiPoints = pathData.Points.Select(p => p.ToApiVector3()).ToList();
            
            var uploadData = new
            {
                levelId = levelId,
                playerName = string.IsNullOrEmpty(_config.PlayerName) ? "Anonymous" : _config.PlayerName,
                biomeName = pathData.BiomeName,
                duration = pathData.DurationInSeconds,
                points = apiPoints,
                isSuccessful = true, // Will be determined by validation logic
                tags = new string[] { } // Can be extended later
            };

            string json = JsonConvert.SerializeObject(uploadData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = _config.TimeoutSeconds;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<ApiResponse<PathUploadResponse>>(request.downloadHandler.text);
                        
                        if (response.Success)
                        {
                            _config.IncrementUploadCount();
                            _logger.LogInfo($"Path uploaded successfully: {response.Data.PathId}");
                            callback?.Invoke(true, response.Data.PathId);
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

        // Download Paths for Level
        public void DownloadPaths(string levelId, System.Action<List<PathData>, string> callback)
        {
            if (!_config.EnableCloudSync)
            {
                callback?.Invoke(new List<PathData>(), "Cloud sync disabled");
                return;
            }

            _coroutineRunner.StartCoroutine(DownloadPathsCoroutine(levelId, callback));
        }

        private IEnumerator DownloadPathsCoroutine(string levelId, System.Action<List<PathData>, string> callback)
        {
            string url = $"{_config.BaseUrl}/api/paths/{levelId}?limit=200";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = _config.TimeoutSeconds;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<PathListResponse>(request.downloadHandler.text);
                        var paths = new List<PathData>();
                        
                        if (response.Data != null)
                        {
                            foreach (var serverPath in response.Data)
                            {
                                paths.Add(serverPath.ToPathData());
                            }
                        }
                        
                        _logger.LogInfo($"Downloaded {paths.Count} paths for level {levelId}");
                        callback?.Invoke(paths, null);
                    }
                    catch (Exception e)
                    {
                        string error = $"Failed to parse download response: {e.Message}";
                        _logger.LogError(error);
                        callback?.Invoke(new List<PathData>(), error);
                    }
                }
                else if (request.responseCode == 404)
                {
                    // No paths found for this level
                    _logger.LogInfo($"No paths found for level {levelId}");
                    callback?.Invoke(new List<PathData>(), null);
                }
                else
                {
                    string error = $"Download request failed: {request.error} (HTTP {request.responseCode})";
                    _logger.LogError(error);
                    callback?.Invoke(new List<PathData>(), error);
                }
            }
        }

        // Search Path by Gipfelcode
        public void SearchPathByGipfelcode(string gipfelcode, System.Action<PathData, string> callback)
        {
            if (!_config.EnableCloudSync)
            {
                callback?.Invoke(null, "Cloud sync disabled");
                return;
            }

            if (string.IsNullOrEmpty(gipfelcode) || gipfelcode.Length != 8)
            {
                callback?.Invoke(null, "Gipfelcode must be 8 characters long");
                return;
            }

            _coroutineRunner.StartCoroutine(SearchPathByGipfelcodeCoroutine(gipfelcode, callback));
        }

        private IEnumerator SearchPathByGipfelcodeCoroutine(string gipfelcode, System.Action<PathData, string> callback)
        {
            string url = $"{_config.BaseUrl}/api/paths/search/{gipfelcode.ToUpper()}";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = _config.TimeoutSeconds;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<PathSearchResponse>(request.downloadHandler.text);
                        
                        if (response.Success && response.Data != null)
                        {
                            var pathData = response.Data.ToPathData();
                            _logger.LogInfo($"Found path with Gipfelcode {gipfelcode}: {pathData.GetDisplayName()}");
                            callback?.Invoke(pathData, null);
                        }
                        else
                        {
                            _logger.LogInfo($"No path found with Gipfelcode {gipfelcode}");
                            callback?.Invoke(null, "Path not found");
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
                    _logger.LogInfo($"No path found with Gipfelcode {gipfelcode}");
                    callback?.Invoke(null, "Path not found");
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
    }
}