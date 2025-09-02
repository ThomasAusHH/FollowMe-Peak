using System;

namespace FollowMePeak.Models
{
    public class UpdateMessage
    {
        public bool HasUpdate { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } // "info", "warning", "critical"
        public DateTime LastChecked { get; set; }
        
        // Cache validity (5 minutes)
        public bool IsCacheValid()
        {
            return (DateTime.Now - LastChecked).TotalMinutes < 5;
        }
    }
    
    public class UpdateMessageResponse
    {
        public bool HasUpdate { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public string Version { get; set; }
    }
}