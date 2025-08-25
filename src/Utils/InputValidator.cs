using System;
using System.Text.RegularExpressions;

namespace FollowMePeak.Utils
{
    public static class InputValidator
    {
        private static readonly Regex LevelIdPattern = new(@"^[a-zA-Z0-9_\-\.]{1,100}$");
        private static readonly Regex SpecialCharacterPattern = new(@"[^a-zA-Z0-9_\-\s]");
        private static readonly Regex ValidPeakCodePattern = new(@"^[A-Z0-9]{8}$");
        private static readonly Regex InvalidPeakCodeCharacters = new(@"[^A-Z0-9]");

        // Sanitize player name to prevent SQL injection and XSS
        public static string SanitizePlayerName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Anonymous";
            
            // Remove dangerous characters, keep only alphanumeric, underscore, dash, space
            string sanitized = SpecialCharacterPattern.Replace(input, "");

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
            string sanitized = SpecialCharacterPattern.Replace(input, "");

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
            return LevelIdPattern.IsMatch(levelId);
        }
        
        // Validate peak code format (8 characters, alphanumeric)
        public static bool IsValidPeakCode(string peakCode)
        {
            if (string.IsNullOrWhiteSpace(peakCode))
                return false;

            return ValidPeakCodePattern.IsMatch(peakCode);
        }
        
        // Sanitize peak code
        public static string SanitizePeakCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";
            
            // Remove non-alphanumeric, convert to uppercase, limit to 8 chars
            string sanitized = InvalidPeakCodeCharacters.Replace(input.ToUpper(), "");

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
    }
}