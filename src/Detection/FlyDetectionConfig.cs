namespace FollowMePeak.Detection
{
    public static class FlyDetectionConfig
    {
        // Fixed configuration values - not configurable by users
        public const bool IsEnabled = true;
        public const float DetectionThreshold = 50f;
        public const bool ShouldLogDetections = true;
        public const bool ShouldAutoFlagClimbs = true;
        public const bool ShouldShowWarning = true;
        public const float DetectionCheckInterval = 0.5f;
        public const float SpawnGracePeriod = 30f;
        public const int MinGravityBodies = 20;
        
        // Legacy properties for backward compatibility (now just return constants)
        public const bool Enable = IsEnabled;
        public const float Threshold = DetectionThreshold;
        public const bool LogDetections = ShouldLogDetections;
        public const bool AutoFlagClimbs = ShouldAutoFlagClimbs;
        public const bool ShowWarning = ShouldShowWarning;
        public const float CheckInterval = DetectionCheckInterval;
        
        /// <summary>
        /// No validation needed for constants
        /// </summary>
        public static void ValidateConfig() 
        { 
            // Empty - constants don't need validation
        }
    }
}