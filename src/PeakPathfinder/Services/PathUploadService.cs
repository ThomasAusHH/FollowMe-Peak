using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using PeakPathfinder.Models;
using UnityEngine;

namespace PeakPathfinder.Services
{
    public class PathUploadService
    {
        private readonly ManualLogSource _logger;
        private readonly VPSApiService _apiService;
        private readonly ServerConfigService _configService;
        private readonly string _queueFilePath;
        
        private List<UploadQueueItem> _uploadQueue = new List<UploadQueueItem>();
        private bool _isProcessingQueue = false;

        public PathUploadService(ManualLogSource logger, VPSApiService apiService, ServerConfigService configService)
        {
            _logger = logger;
            _apiService = apiService;
            _configService = configService;
            _queueFilePath = Path.Combine(Paths.PluginPath, "PeakPathfinder_Data", "upload_queue.json");
            
            LoadQueue();
        }

        public int QueuedUploads => _uploadQueue.Count(x => x.Status == UploadStatus.Pending || x.Status == UploadStatus.Failed);
        public int CompletedUploads => _uploadQueue.Count(x => x.Status == UploadStatus.Completed);
        public int FailedUploads => _uploadQueue.Count(x => x.Status == UploadStatus.Failed && !x.ShouldRetry());

        // Queue a path for upload
        public void QueueForUpload(PathData pathData, string levelId)
        {
            if (!_configService.Config.EnableCloudSync || !_configService.Config.AutoUpload)
            {
                _logger.LogInfo("Cloud sync or auto-upload disabled, skipping upload");
                return;
            }

            // Check if this path is already queued
            if (_uploadQueue.Any(x => x.PathData.Id == pathData.Id))
            {
                _logger.LogInfo($"Path {pathData.Id} already in upload queue");
                return;
            }

            var queueItem = new UploadQueueItem
            {
                PathData = pathData
            };

            // Store level ID in the PathData for upload (temporary solution)
            queueItem.PathData.BiomeName = $"{pathData.BiomeName}|{levelId}";

            _uploadQueue.Add(queueItem);
            SaveQueue();

            _logger.LogInfo($"Added path {pathData.Id} to upload queue. Queue size: {QueuedUploads}");

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
            
            // Check rate limiting
            if (!_configService.Config.CanUpload())
            {
                _logger.LogWarning("Upload rate limit exceeded, pausing queue processing");
                _isProcessingQueue = false;
                return;
            }

            // Extract level ID from biome name (temporary solution)
            string originalBiomeName = item.PathData.BiomeName;
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

            _logger.LogInfo($"Uploading path {item.PathData.Id} (attempt {item.RetryCount + 1})");

            // Create upload data with correct level ID
            var uploadPath = new PathData
            {
                Id = item.PathData.Id,
                CreationTime = item.PathData.CreationTime,
                BiomeName = originalBiomeName,
                DurationInSeconds = item.PathData.DurationInSeconds,
                Points = item.PathData.Points
            };

            // Perform upload
            _apiService.UploadPath(uploadPath, levelId, (success, error) =>
            {
                if (success)
                {
                    item.Status = UploadStatus.Completed;
                    _logger.LogInfo($"Successfully uploaded path {item.PathData.Id}");
                }
                else
                {
                    item.RetryCount++;
                    item.LastError = error;
                    
                    if (item.ShouldRetry(_configService.Config.RetryAttempts))
                    {
                        item.Status = UploadStatus.Failed;
                        _logger.LogWarning($"Upload failed for path {item.PathData.Id}: {error}. Will retry ({item.RetryCount}/{_configService.Config.RetryAttempts})");
                    }
                    else
                    {
                        item.Status = UploadStatus.Failed;
                        _logger.LogError($"Upload permanently failed for path {item.PathData.Id}: {error}");
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
            try
            {
                string directory = Path.GetDirectoryName(_queueFilePath);
                Directory.CreateDirectory(directory);

                string json = JsonConvert.SerializeObject(_uploadQueue, Formatting.Indented);
                File.WriteAllText(_queueFilePath, json);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to save upload queue: {e.Message}");
            }
        }

        // Load queue from disk
        private void LoadQueue()
        {
            try
            {
                if (File.Exists(_queueFilePath))
                {
                    string json = File.ReadAllText(_queueFilePath);
                    _uploadQueue = JsonConvert.DeserializeObject<List<UploadQueueItem>>(json) ?? new List<UploadQueueItem>();
                    
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