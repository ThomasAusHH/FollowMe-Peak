using System;
using Newtonsoft.Json;
using FollowMePeak.Config;

namespace FollowMePeak.Models
{
    public class ServerConfig
    {
        // Server settings - configurable by build configuration
        [JsonIgnore]
        public string BaseUrl => GetBaseUrl();
        [JsonIgnore]
        public bool AutoUpload => true;
        [JsonIgnore]
        public bool AutoDownload => true;
        [JsonIgnore]
        public int TimeoutSeconds => 30;
        [JsonIgnore]
        public int RetryAttempts => 3;
        [JsonIgnore]
        public int MaxUploadsPerHour => 50;
        
        // API authentication - injected during build process - NEVER serialize this!
        [JsonIgnore]
        public string ApiKey => Config.ApiKeys.ServerApiKey;
        
        // User-configurable settings
        public bool EnableCloudSync { get; set; } = false;
        public string PlayerName { get; set; } = "Anonymous";
        public bool? UseCompressedFormat { get; set; } = true; // Use compressed upload/download by default
        
        // Rate limiting tracking
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
        
        private string GetBaseUrl()
        {
#if LOCAL_SERVER
            return "http://localhost:3000";
#elif DEVELOPMENT
            return "https://followme-peak.ddns.net";
#else
            return "https://followme-peak.ddns.net";
#endif
        }
    }
}