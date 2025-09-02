using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using FollowMePeak.Models;
using FollowMePeak.Utils;
using UnityEngine;

namespace FollowMePeak.Services
{
    public class ClimbUploadService
    {
        private readonly ManualLogSource _logger;
        private readonly VPSApiService _apiService;
        private readonly ServerConfigService _configService;
        private readonly string _queueFilePath;
        
        private List<UploadQueueItem> _uploadQueue = new List<UploadQueueItem>();
        private bool _isProcessingQueue = false;

        public ClimbUploadService(ManualLogSource logger, VPSApiService apiService, ServerConfigService configService)
        {
            _logger = logger;
            _apiService = apiService;
            _configService = configService;
            _queueFilePath = Path.Combine(Paths.PluginPath, "FollowMePeak_Data", "upload_queue.json");
            
            LoadQueue();
        }

        public int QueuedUploads => _uploadQueue.Count(x => x.Status == UploadStatus.Pending || x.Status == UploadStatus.Failed);
        public int CompletedUploads => _uploadQueue.Count(x => x.Status == UploadStatus.Completed);
        public int FailedUploads => _uploadQueue.Count(x => x.Status == UploadStatus.Failed && !x.ShouldRetry());

        // Queue a climb for upload
        public void QueueForUpload(ClimbData climbData, string levelId)
        {
            // Check for fly detection FIRST
            Detection.SimpleFlyDetector.PerformDetection();
            if (Detection.SimpleFlyDetector.ShouldFlagClimb())
            {
                _logger.LogWarning("[FlyDetection] Climb upload blocked - fly mod detected!");
                _logger.LogWarning($"[FlyDetection] Detection Score: {Detection.SimpleFlyDetector.DetectionScore}/100");
                _logger.LogWarning($"[FlyDetection] Reason: {Detection.SimpleFlyDetector.LastDetectionReason}");
                
                // Log the climb details that would have been uploaded
                _logger.LogInfo($"[FlyDetection] Blocked climb: {climbData.Id} in level {levelId}");
                
                // Show warning to user if configured
                if (Plugin.FlyDetection_ShowWarning.Value)
                {
                    // This would trigger UI notification - implementation depends on UI system
                    _logger.LogWarning("[FlyDetection] Climb was not uploaded due to fly mod detection");
                }
                
                return; // Don't queue the climb for upload
            }
            
            if (!_configService.Config.EnableCloudSync || !_configService.Config.AutoUpload)
            {
                _logger.LogInfo("Cloud sync or auto-upload disabled, skipping upload");
                return;
            }

            // Input validation
            if (climbData == null)
            {
                _logger.LogError("Cannot queue null climb data for upload");
                return;
            }

            if (!InputValidator.IsValidLevelId(levelId))
            {
                _logger.LogError($"Cannot queue climb - invalid level ID: {levelId}");
                return;
            }

            if (!InputValidator.IsValidPointCount(climbData.Points?.Count ?? 0))
            {
                _logger.LogError($"Cannot queue climb - invalid point count: {climbData.Points?.Count ?? 0}");
                return;
            }

            // Check if this climb is already queued
            if (_uploadQueue.Any(x => x.ClimbData.Id == climbData.Id))
            {
                _logger.LogInfo($"Climb {climbData.Id} already in upload queue");
                return;
            }

            var queueItem = new UploadQueueItem
            {
                ClimbData = climbData
            };

            // Store level ID in the ClimbData for upload (temporary solution)
            queueItem.ClimbData.BiomeName = $"{climbData.BiomeName}|{levelId}";

            _uploadQueue.Add(queueItem);
            SaveQueue();

            _logger.LogInfo($"Added climb {climbData.Id} to upload queue. Queue size: {QueuedUploads}");

            // Start processing if not already running
            if (!_isProcessingQueue)
            {
                ProcessQueue();
            }
        }

        // Process the upload queue
        public void ProcessQueue()
        {
            if (_isProcessingQueue)
            {
                _logger.LogInfo("Upload queue already being processed");
                return;
            }

            _isProcessingQueue = true;
            _logger.LogInfo($"Starting upload queue processing. {QueuedUploads} items to process");

            // Clean up expired items first
            CleanupExpiredItems();

            // Get items that need processing
            var itemsToProcess = _uploadQueue
                .Where(x => x.Status == UploadStatus.Pending || (x.Status == UploadStatus.Failed && x.ShouldRetry()))
                .OrderBy(x => x.CreatedAt)
                .ToList();

            if (itemsToProcess.Count == 0)
            {
                _logger.LogInfo("No items to process in upload queue");
                _isProcessingQueue = false;
                return;
            }

            // Process items one by one
            ProcessNextItem(itemsToProcess, 0);
        }

        private void ProcessNextItem(List<UploadQueueItem> items, int index)
        {
            if (index >= items.Count)
            {
                _logger.LogInfo("Upload queue processing completed");
                _isProcessingQueue = false;
                SaveQueue();
                return;
            }

            var item = items[index];
            
            // Skip items with invalid data
            if (item == null || item.ClimbData == null)
            {
                _logger.LogWarning($"Skipping invalid upload queue item at index {index}");
                ProcessNextItem(items, index + 1);
                return;
            }
            
            // Check for fly detection before uploading
            Detection.SimpleFlyDetector.PerformDetection();
            if (Detection.SimpleFlyDetector.ShouldFlagClimb())
            {
                _logger.LogWarning($"[FlyDetection] Removing climb {item.ClimbData.Id} from queue - fly mod detected during processing");
                _logger.LogWarning($"[FlyDetection] Score: {Detection.SimpleFlyDetector.DetectionScore}/100");
                
                // Remove from queue
                item.Status = UploadStatus.Failed;
                item.LastError = "Fly mod detected - climb flagged as private";
                
                // Continue with next item
                ProcessNextItem(items, index + 1);
                return;
            }
            
            // Check rate limiting
            if (!_configService.Config.CanUpload())
            {
                _logger.LogWarning("Upload rate limit exceeded, pausing queue processing");
                _isProcessingQueue = false;
                return;
            }

            // Extract level ID from biome name (temporary solution)
            string originalBiomeName = item.ClimbData.BiomeName;
            string levelId = "unknown";
            
            if (originalBiomeName.Contains("|"))
            {
                var parts = originalBiomeName.Split('|');
                originalBiomeName = parts[0];
                levelId = parts[1];
            }

            // Update status
            item.Status = UploadStatus.Uploading;
            item.LastAttempt = DateTime.Now;
            SaveQueue();

            _logger.LogInfo($"Uploading climb {item.ClimbData.Id} (attempt {item.RetryCount + 1})");

            // Create upload data with correct level ID
            var uploadClimb = new ClimbData
            {
                Id = item.ClimbData.Id,
                CreationTime = item.ClimbData.CreationTime,
                BiomeName = originalBiomeName,
                DurationInSeconds = item.ClimbData.DurationInSeconds,
                Points = item.ClimbData.Points,
                AscentLevel = item.ClimbData.AscentLevel
            };

            // Perform upload
            _apiService.UploadClimb(uploadClimb, levelId, (success, error) =>
            {
                if (success)
                {
                    item.Status = UploadStatus.Completed;
                    _logger.LogInfo($"Successfully uploaded climb {item.ClimbData.Id}");
                }
                else
                {
                    item.RetryCount++;
                    item.LastError = error;
                    
                    if (item.ShouldRetry(_configService.Config.RetryAttempts))
                    {
                        item.Status = UploadStatus.Failed;
                        _logger.LogWarning($"Upload failed for climb {item.ClimbData.Id}: {error}. Will retry ({item.RetryCount}/{_configService.Config.RetryAttempts})");
                    }
                    else
                    {
                        item.Status = UploadStatus.Failed;
                        _logger.LogError($"Upload permanently failed for climb {item.ClimbData.Id}: {error}");
                    }
                }

                SaveQueue();

                // Wait a bit before processing next item to avoid overwhelming server
                Plugin.Instance.StartCoroutine(WaitAndProcessNext(items, index + 1, 2.0f));
            });
        }

        private System.Collections.IEnumerator WaitAndProcessNext(List<UploadQueueItem> items, int nextIndex, float delay)
        {
            yield return new WaitForSeconds(delay);
            ProcessNextItem(items, nextIndex);
        }

        // Manual retry for specific failed item
        public void RetryFailedUploads()
        {
            var failedItems = _uploadQueue.Where(x => x.Status == UploadStatus.Failed && x.ShouldRetry()).ToList();
            
            if (failedItems.Count == 0)
            {
                _logger.LogInfo("No failed uploads to retry");
                return;
            }

            _logger.LogInfo($"Retrying {failedItems.Count} failed uploads");
            
            foreach (var item in failedItems)
            {
                item.Status = UploadStatus.Pending;
            }
            
            SaveQueue();
            ProcessQueue();
        }

        // Clear completed uploads from queue
        public void ClearCompletedUploads()
        {
            int beforeCount = _uploadQueue.Count;
            _uploadQueue.RemoveAll(x => x.Status == UploadStatus.Completed);
            int removedCount = beforeCount - _uploadQueue.Count;
            
            if (removedCount > 0)
            {
                _logger.LogInfo($"Cleared {removedCount} completed uploads from queue");
                SaveQueue();
            }
        }

        // Remove expired items
        private void CleanupExpiredItems()
        {
            var maxAge = TimeSpan.FromDays(7); // Keep items for 7 days
            var expiredItems = _uploadQueue.Where(x => x.IsExpired(maxAge)).ToList();
            
            foreach (var item in expiredItems)
            {
                item.Status = UploadStatus.Expired;
            }
            
            _uploadQueue.RemoveAll(x => x.Status == UploadStatus.Expired);
            
            if (expiredItems.Count > 0)
            {
                _logger.LogInfo($"Removed {expiredItems.Count} expired items from upload queue");
            }
        }

        // Save queue to disk
        private void SaveQueue()
        {
            // Create defensive copy of queue for saving
            var queue = new List<UploadQueueItem>(_uploadQueue);
            FileUtils.WriteJsonFileInBackground(_logger, _queueFilePath, queue);
        }

        // Load queue from disk
        private void LoadQueue()
        {
            try
            {
                if (File.Exists(_queueFilePath))
                {
                    string json = File.ReadAllText(_queueFilePath);
                    _uploadQueue = JsonConvert.DeserializeObject<List<UploadQueueItem>>(json, CommonJsonSettings.Default) ?? new List<UploadQueueItem>();
                    
                    // Remove any items with null ClimbData (from old PathData format)
                    _uploadQueue.RemoveAll(item => item.ClimbData == null);
                    
                    // Reset any items that were in uploading state (crashed during upload)
                    foreach (var item in _uploadQueue.Where(x => x.Status == UploadStatus.Uploading))
                    {
                        item.Status = UploadStatus.Pending;
                    }
                    
                    _logger.LogInfo($"Loaded upload queue with {_uploadQueue.Count} items");
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to load upload queue: {e.Message}");
                _uploadQueue = new List<UploadQueueItem>();
            }
        }

        // Get queue status for UI
        public string GetQueueStatus()
        {
            return $"Queue: {QueuedUploads} pending, {CompletedUploads} completed, {FailedUploads} failed";
        }
    }
}