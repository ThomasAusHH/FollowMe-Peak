using BepInEx.Configuration;

namespace FollowMePeak.Detection
{
    public static class FlyDetectionConfig
    {
        // Config entries - will be initialized from Plugin.cs
        public static ConfigEntry<bool> Enable { get; set; }
        public static ConfigEntry<float> Threshold { get; set; }
        public static ConfigEntry<bool> LogDetections { get; set; }
        public static ConfigEntry<bool> AutoFlagClimbs { get; set; }
        public static ConfigEntry<bool> ShowWarning { get; set; }
        public static ConfigEntry<float> CheckInterval { get; set; }
        
        // Helper properties for easy access
        public static bool IsEnabled => Enable?.Value ?? true;
        public static float DetectionThreshold => Threshold?.Value ?? 50f;
        public static bool ShouldLogDetections => LogDetections?.Value ?? true;
        public static bool ShouldAutoFlagClimbs => AutoFlagClimbs?.Value ?? true;
        public static bool ShouldShowWarning => ShowWarning?.Value ?? true;
        public static float DetectionCheckInterval => CheckInterval?.Value ?? 1.0f;
        
        // Default values
        public const bool DEFAULT_ENABLE = true;
        public const float DEFAULT_THRESHOLD = 50f;
        public const bool DEFAULT_LOG_DETECTIONS = true;
        public const bool DEFAULT_AUTO_FLAG_CLIMBS = true;
        public const bool DEFAULT_SHOW_WARNING = true;
        public const float DEFAULT_CHECK_INTERVAL = 1.0f;
        
        // Threshold ranges
        public const float MIN_THRESHOLD = 10f;
        public const float MAX_THRESHOLD = 100f;
        
        // Check interval ranges
        public const float MIN_CHECK_INTERVAL = 0.1f;
        public const float MAX_CHECK_INTERVAL = 5f;
        
        /// <summary>
        /// Validates and clamps configuration values to valid ranges
        /// </summary>
        public static void ValidateConfig()
        {
            if (Threshold != null && (Threshold.Value < MIN_THRESHOLD || Threshold.Value > MAX_THRESHOLD))
            {
                Threshold.Value = UnityEngine.Mathf.Clamp(Threshold.Value, MIN_THRESHOLD, MAX_THRESHOLD);
            }
            
            if (CheckInterval != null && (CheckInterval.Value < MIN_CHECK_INTERVAL || CheckInterval.Value > MAX_CHECK_INTERVAL))
            {
                CheckInterval.Value = UnityEngine.Mathf.Clamp(CheckInterval.Value, MIN_CHECK_INTERVAL, MAX_CHECK_INTERVAL);
            }
        }
    }
}