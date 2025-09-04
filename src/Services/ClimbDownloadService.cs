using System;
using System.Collections.Generic;
using System.Linq;
using FollowMePeak.Models;
using FollowMePeak.Utils;

namespace FollowMePeak.Services
{
    public class ClimbDownloadService
    {
        private readonly ModLogger _logger;
        private readonly VPSApiService _apiService;
        private readonly ServerConfigService _configService;
        private readonly ClimbDataService _climbDataService;
        
        private DateTime _lastDownload = DateTime.MinValue;
        private readonly Dictionary<string, DateTime> _levelDownloadTimes = new Dictionary<string, DateTime>();

        public ClimbDownloadService(ModLogger logger, VPSApiService apiService, 
            ServerConfigService configService, ClimbDataService climbDataService)
        {
            _logger = logger;
            _apiService = apiService;
            _configService = configService;
            _climbDataService = climbDataService;
        }

        public bool IsDownloading { get; private set; } = false;
        public DateTime LastDownload => _lastDownload;

        // Download and merge climbs for current level
        public void DownloadAndMergeClimbs(string levelId, System.Action<int, string, ClimbListMeta> callback = null, int limit = 10, int offset = 0)
        {
            if (!_configService.Config.EnableCloudSync || !_configService.Config.AutoDownload)
            {
                _logger.Info("Cloud sync or auto-download disabled, skipping download");
                callback?.Invoke(0, "Cloud sync disabled", null);
                return;
            }

            if (IsDownloading)
            {
                _logger.Info("Download already in progress");
                callback?.Invoke(0, "Download in progress", null);
                return;
            }

            // Check if we downloaded recently for this level (avoid spam)
            if (_levelDownloadTimes.ContainsKey(levelId))
            {
                var timeSinceLastDownload = DateTime.Now - _levelDownloadTimes[levelId];
                if (timeSinceLastDownload < TimeSpan.FromMinutes(5))
                {
                    _logger.Info($"Downloaded {levelId} recently, skipping");
                    callback?.Invoke(0, "Downloaded recently", null);
                    return;
                }
            }

            IsDownloading = true;
            _logger.Info($"Starting download for level: {levelId}");

            _apiService.DownloadClimbs(levelId, (downloadedClimbs, error, meta) =>
            {
                IsDownloading = false;
                _lastDownload = DateTime.Now;
                _levelDownloadTimes[levelId] = DateTime.Now;

                if (error != null)
                {
                    _logger.Error($"Download failed for level {levelId}: {error}");
                    callback?.Invoke(0, error, null);
                    return;
                }

                try
                {
                    int mergedCount = MergeDownloadedClimbs(downloadedClimbs, levelId);
                    _logger.Info($"Downloaded and merged {mergedCount} new climbs for level {levelId}");
                    callback?.Invoke(mergedCount, null, meta);
                }
                catch (Exception e)
                {
                    _logger.Error($"Failed to merge downloaded climbs: {e.Message}");
                    callback?.Invoke(0, e.Message, null);
                }
            }, limit, offset);
        }

        // Merge downloaded climbs with local climbs
        private int MergeDownloadedClimbs(List<ClimbData> downloadedClimbs, string levelId)
        {
            if (downloadedClimbs == null || downloadedClimbs.Count == 0)
            {
                return 0;
            }

            var existingClimbs = _climbDataService.GetAllClimbs();
            int mergedCount = 0;

            foreach (var downloadedClimb in downloadedClimbs)
            {
                // Check for duplicates based on ID
                if (existingClimbs.Any(x => x.Id == downloadedClimb.Id))
                {
                    continue; // Skip duplicate
                }

                // Check for similar climbs (same position, similar time)
                if (IsSimilarClimbExists(downloadedClimb, existingClimbs))
                {
                    _logger.Debug($"Skipping similar climb: {downloadedClimb.Id}");
                    continue;
                }

                // Add unique climb
                _climbDataService.AddClimb(downloadedClimb);
                mergedCount++;
                
                _logger.Debug($"Merged downloaded climb: {downloadedClimb.Id} from {downloadedClimb.BiomeName}");
            }

            // Save merged data if we added any climbs
            if (mergedCount > 0)
            {
                _climbDataService.SaveClimbsToFile(false);
            }

            return mergedCount;
        }

        // Check if a similar climb already exists to avoid near-duplicates
        private bool IsSimilarClimbExists(ClimbData newClimb, List<ClimbData> existingClimbs)
        {
            const float POSITION_THRESHOLD = 2.0f; // 2 units distance
            const float TIME_THRESHOLD = 5.0f; // 5 seconds difference

            foreach (var existingClimb in existingClimbs)
            {
                // Must be same biome
                if (existingClimb.BiomeName != newClimb.BiomeName)
                    continue;

                // Check duration similarity
                if (Math.Abs(existingClimb.DurationInSeconds - newClimb.DurationInSeconds) > TIME_THRESHOLD)
                    continue;

                // Check climb similarity (start and end points)
                if (newClimb.Points.Count < 2 || existingClimb.Points.Count < 2)
                    continue;

                var newStart = newClimb.Points.First();
                var newEnd = newClimb.Points.Last();
                var existingStart = existingClimb.Points.First();
                var existingEnd = existingClimb.Points.Last();

                float startDistance = UnityEngine.Vector3.Distance(newStart, existingStart);
                float endDistance = UnityEngine.Vector3.Distance(newEnd, existingEnd);

                if (startDistance < POSITION_THRESHOLD && endDistance < POSITION_THRESHOLD)
                {
                    return true; // Similar climb found
                }
            }

            return false;
        }

        // Download recent climbs from all levels
        public void DownloadRecentClimbs(System.Action<int, string, ClimbListMeta> callback = null)
        {
            if (!_configService.Config.EnableCloudSync)
            {
                callback?.Invoke(0, "Cloud sync disabled", null);
                return;
            }

            if (IsDownloading)
            {
                callback?.Invoke(0, "Download in progress", null);
                return;
            }

            IsDownloading = true;
            _logger.Info("Downloading recent climbs from all levels");

            // Use the recent climbs endpoint
            _apiService.DownloadClimbs("recent", (recentClimbs, error, meta) =>
            {
                IsDownloading = false;
                _lastDownload = DateTime.Now;

                if (error != null)
                {
                    _logger.Error($"Failed to download recent climbs: {error}");
                    callback?.Invoke(0, error, null);
                    return;
                }

                try
                {
                    int mergedCount = MergeDownloadedClimbs(recentClimbs, "all_levels");
                    _logger.Info($"Downloaded and merged {mergedCount} recent climbs");
                    callback?.Invoke(mergedCount, null, meta);
                }
                catch (Exception e)
                {
                    _logger.Error($"Failed to merge recent climbs: {e.Message}");
                    callback?.Invoke(0, e.Message, null);
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

            DownloadAndMergeClimbs(levelId, (count, error, meta) =>
            {
                if (error == null && count > 0)
                {
                    _logger.Info($"Auto-update found {count} new climbs for {levelId}");
                }
            }, 10, 0);
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
            _logger.Info("Download history cleared");
        }
    }
}