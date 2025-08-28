using System;
using System.Text.RegularExpressions;

namespace FollowMePeak.Utils
{
    public static class InputValidator
    {
        // Sanitize player name to prevent SQL injection and XSS
        public static string SanitizePlayerName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Anonymous";
            
            // Remove dangerous characters, keep only alphanumeric, underscore, dash, space
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9_\-\s]", "");
            
            // Limit length
            if (sanitized.Length > 50)
                sanitized = sanitized.Substring(0, 50);
            
            // Ensure not empty after sanitization
            return string.IsNullOrWhiteSpace(sanitized) ? "Anonymous" : sanitized.Trim();
        }
        
        // Validate and sanitize biome name
        public static string SanitizeBiomeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Unknown";
            
            // Keep only safe characters
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9_\-\s]", "");
            
            // Limit length
            if (sanitized.Length > 100)
                sanitized = sanitized.Substring(0, 100);
            
            return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized.Trim();
        }
        
        // Validate level ID format
        public static bool IsValidLevelId(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
                return false;
            
            // Level IDs should be alphanumeric with some allowed special characters
            return Regex.IsMatch(levelId, @"^[a-zA-Z0-9_\-\.]{1,100}$");
        }
        
        // Validate peak code format (8 characters, alphanumeric)
        public static bool IsValidPeakCode(string peakCode)
        {
            if (string.IsNullOrWhiteSpace(peakCode))
                return false;
            
            return Regex.IsMatch(peakCode, @"^[A-Z0-9]{8}$");
        }
        
        // Sanitize peak code
        public static string SanitizePeakCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";
            
            // Remove non-alphanumeric, convert to uppercase, limit to 8 chars
            string sanitized = Regex.Replace(input.ToUpper(), @"[^A-Z0-9]", "");
            
            return sanitized.Length == 8 ? sanitized : "";
        }
        
        // Validate numerical ranges
        public static float ClampDuration(float duration)
        {
            // Duration should be between 1 second and 1 hour
            return Math.Max(1f, Math.Min(3600f, duration));
        }
        
        // Validate point count
        public static bool IsValidPointCount(int pointCount)
        {
            // Reasonable limits for climb points
            return pointCount >= 2 && pointCount <= 10000;
        }
        
        // Validate and clamp ascent level
        public static int ClampAscentLevel(int ascentLevel)
        {
            // Ascent levels range from -1 (not started) to 8+ (maximum difficulty)
            return Math.Max(-1, Math.Min(8, ascentLevel));
        }
        
        // Validate ascent level range
        public static bool IsValidAscentLevel(int ascentLevel)
        {
            return ascentLevel >= -1 && ascentLevel <= 8;
        }
    }
}