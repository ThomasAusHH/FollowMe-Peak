using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using PeakPathfinder.Models;

namespace PeakPathfinder.Services
{
    public class PathDownloadService
    {
        private readonly ManualLogSource _logger;
        private readonly VPSApiService _apiService;
        private readonly ServerConfigService _configService;
        private readonly PathDataService _pathDataService;
        
        private DateTime _lastDownload = DateTime.MinValue;
        private readonly Dictionary<string, DateTime> _levelDownloadTimes = new Dictionary<string, DateTime>();

        public PathDownloadService(ManualLogSource logger, VPSApiService apiService, 
            ServerConfigService configService, PathDataService pathDataService)
        {
            _logger = logger;
            _apiService = apiService;
            _configService = configService;
            _pathDataService = pathDataService;
        }

        public bool IsDownloading { get; private set; } = false;
        public DateTime LastDownload => _lastDownload;

        // Download and merge paths for current level
        public void DownloadAndMergePaths(string levelId, System.Action<int, string> callback = null)
        {
            if (!_configService.Config.EnableCloudSync || !_configService.Config.AutoDownload)
            {
                _logger.LogInfo("Cloud sync or auto-download disabled, skipping download");
                callback?.Invoke(0, "Cloud sync disabled");
                return;
            }

            if (IsDownloading)
            {
                _logger.LogInfo("Download already in progress");
                callback?.Invoke(0, "Download in progress");
                return;
            }

            // Check if we downloaded recently for this level (avoid spam)
            if (_levelDownloadTimes.ContainsKey(levelId))
            {
                var timeSinceLastDownload = DateTime.Now - _levelDownloadTimes[levelId];
                if (timeSinceLastDownload < TimeSpan.FromMinutes(5))
                {
                    _logger.LogInfo($"Downloaded {levelId} recently, skipping");
                    callback?.Invoke(0, "Downloaded recently");
                    return;
                }
            }

            IsDownloading = true;
            _logger.LogInfo($"Starting download for level: {levelId}");

            _apiService.DownloadPaths(levelId, (downloadedPaths, error) =>
            {
                IsDownloading = false;
                _lastDownload = DateTime.Now;
                _levelDownloadTimes[levelId] = DateTime.Now;

                if (error != null)
                {
                    _logger.LogError($"Download failed for level {levelId}: {error}");
                    callback?.Invoke(0, error);
                    return;
                }

                try
                {
                    int mergedCount = MergeDownloadedPaths(downloadedPaths, levelId);
                    _logger.LogInfo($"Downloaded and merged {mergedCount} new paths for level {levelId}");
                    callback?.Invoke(mergedCount, null);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to merge downloaded paths: {e.Message}");
                    callback?.Invoke(0, e.Message);
                }
            });
        }

        // Merge downloaded paths with local paths
        private int MergeDownloadedPaths(List<PathData> downloadedPaths, string levelId)
        {
            if (downloadedPaths == null || downloadedPaths.Count == 0)
            {
                return 0;
            }

            var existingPaths = _pathDataService.GetAllPaths();
            int mergedCount = 0;

            foreach (var downloadedPath in downloadedPaths)
            {
                // Check for duplicates based on ID
                if (existingPaths.Any(x => x.Id == downloadedPath.Id))
                {
                    continue; // Skip duplicate
                }

                // Check for similar paths (same position, similar time)
                if (IsSimilarPathExists(downloadedPath, existingPaths))
                {
                    _logger.LogDebug($"Skipping similar path: {downloadedPath.Id}");
                    continue;
                }

                // Add unique path
                _pathDataService.AddPath(downloadedPath);
                mergedCount++;
                
                _logger.LogDebug($"Merged downloaded path: {downloadedPath.Id} from {downloadedPath.BiomeName}");
            }

            // Save merged data if we added any paths
            if (mergedCount > 0)
            {
                _pathDataService.SavePathsToFile(false);
            }

            return mergedCount;
        }

        // Check if a similar path already exists to avoid near-duplicates
        private bool IsSimilarPathExists(PathData newPath, List<PathData> existingPaths)
        {
            const float POSITION_THRESHOLD = 2.0f; // 2 units distance
            const float TIME_THRESHOLD = 5.0f; // 5 seconds difference

            foreach (var existingPath in existingPaths)
            {
                // Must be same biome
                if (existingPath.BiomeName != newPath.BiomeName)
                    continue;

                // Check duration similarity
                if (Math.Abs(existingPath.DurationInSeconds - newPath.DurationInSeconds) > TIME_THRESHOLD)
                    continue;

                // Check path similarity (start and end points)
                if (newPath.Points.Count < 2 || existingPath.Points.Count < 2)
                    continue;

                var newStart = newPath.Points.First().ToVector3();
                var newEnd = newPath.Points.Last().ToVector3();
                var existingStart = existingPath.Points.First().ToVector3();
                var existingEnd = existingPath.Points.Last().ToVector3();

                float startDistance = UnityEngine.Vector3.Distance(newStart, existingStart);
                float endDistance = UnityEngine.Vector3.Distance(newEnd, existingEnd);

                if (startDistance < POSITION_THRESHOLD && endDistance < POSITION_THRESHOLD)
                {
                    return true; // Similar path found
                }
            }

            return false;
        }

        // Download recent paths from all levels
        public void DownloadRecentPaths(System.Action<int, string> callback = null)
        {
            if (!_configService.Config.EnableCloudSync)
            {
                callback?.Invoke(0, "Cloud sync disabled");
                return;
            }

            if (IsDownloading)
            {
                callback?.Invoke(0, "Download in progress");
                return;
            }

            IsDownloading = true;
            _logger.LogInfo("Downloading recent paths from all levels");

            // Use the recent paths endpoint
            _apiService.DownloadPaths("recent", (recentPaths, error) =>
            {
                IsDownloading = false;
                _lastDownload = DateTime.Now;

                if (error != null)
                {
                    _logger.LogError($"Failed to download recent paths: {error}");
                    callback?.Invoke(0, error);
                    return;
                }

                try
                {
                    int mergedCount = MergeDownloadedPaths(recentPaths, "all_levels");
                    _logger.LogInfo($"Downloaded and merged {mergedCount} recent paths");
                    callback?.Invoke(mergedCount, null);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to merge recent paths: {e.Message}");
                    callback?.Invoke(0, e.Message);
                }
            });
        }

        // Check for updates (can be called periodically)
        public void CheckForUpdates(string levelId)
        {
            // Only check if enabled and not checked recently
            if (!_configService.Config.EnableCloudSync || !_configService.Config.AutoDownload)
                return;

            if (_levelDownloadTimes.ContainsKey(levelId))
            {
                var timeSinceLastCheck = DateTime.Now - _levelDownloadTimes[levelId];
                if (timeSinceLastCheck < TimeSpan.FromMinutes(10))
                    return; // Don't check too frequently
            }

            DownloadAndMergePaths(levelId, (count, error) =>
            {
                if (error == null && count > 0)
                {
                    _logger.LogInfo($"Auto-update found {count} new paths for {levelId}");
                }
            });
        }

        // Get download statistics
        public string GetDownloadStats()
        {
            var totalDownloads = _levelDownloadTimes.Count;
            var lastDownloadText = _lastDownload == DateTime.MinValue 
                ? "Never" 
                : $"{(DateTime.Now - _lastDownload).TotalMinutes:F0}m ago";

            return $"Downloads: {totalDownloads} levels, last: {lastDownloadText}";
        }

        // Clear download history (for testing)
        public void ClearDownloadHistory()
        {
            _levelDownloadTimes.Clear();
            _lastDownload = DateTime.MinValue;
            _logger.LogInfo("Download history cleared");
        }
    }
}