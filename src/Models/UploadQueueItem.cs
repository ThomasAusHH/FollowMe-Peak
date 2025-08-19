using System;

namespace FollowMePeak.Models
{
    public class UploadQueueItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ClimbData ClimbData { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastAttempt { get; set; }
        public string LastError { get; set; }
        public UploadStatus Status { get; set; } = UploadStatus.Pending;
        
        public bool ShouldRetry(int maxRetries = 3)
        {
            return RetryCount < maxRetries && Status == UploadStatus.Failed;
        }
        
        public bool IsExpired(TimeSpan maxAge)
        {
            return DateTime.Now - CreatedAt > maxAge;
        }
    }

    public enum UploadStatus
    {
        Pending,
        Uploading,
        Completed,
        Failed,
        Expired
    }
}