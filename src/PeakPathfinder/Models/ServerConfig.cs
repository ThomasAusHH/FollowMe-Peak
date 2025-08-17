using System;

namespace PeakPathfinder.Models
{
    public class ServerConfig
    {
        public string BaseUrl { get; set; } = "http://localhost:3000";
        public bool EnableCloudSync { get; set; } = false;
        public bool AutoUpload { get; set; } = true;
        public bool AutoDownload { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryAttempts { get; set; } = 3;
        public string PlayerName { get; set; } = "Anonymous";
        
        // Rate limiting
        public int MaxUploadsPerHour { get; set; } = 10;
        public DateTime LastUploadReset { get; set; } = DateTime.MinValue;
        public int UploadsThisHour { get; set; } = 0;
        
        public bool CanUpload()
        {
            // Reset counter every hour
            if (DateTime.Now - LastUploadReset > TimeSpan.FromHours(1))
            {
                UploadsThisHour = 0;
                LastUploadReset = DateTime.Now;
            }
            
            return UploadsThisHour < MaxUploadsPerHour;
        }
        
        public void IncrementUploadCount()
        {
            UploadsThisHour++;
        }
    }
}