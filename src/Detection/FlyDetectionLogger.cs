using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using BepInEx.Logging;
using FollowMePeak.Utils;

namespace FollowMePeak.Detection
{
    public static class FlyDetectionLogger
    {
        private static ManualLogSource logger;
        private static string logFilePath;
        private static bool fileLoggingEnabled = false;
        
        static FlyDetectionLogger()
        {
            logger = BepInEx.Logging.Logger.CreateLogSource("FlyDetection");
            
            // Set up log file path
            var logsDir = Path.Combine(BepInEx.Paths.GameRootPath, "FlyDetectionLogs");
            if (!Directory.Exists(logsDir))
            {
                try
                {
                    Directory.CreateDirectory(logsDir);
                }
                catch { }
            }
            
            logFilePath = Path.Combine(logsDir, $"FlyDetection_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        }

        /// <summary>
        /// Logs a fly detection event with detailed information
        /// </summary>
        public static void LogDetectionEvent(float score, List<string> flags, Vector3 position, Vector3 velocity, string playerName = "Unknown")
        {
            if (ModLogger.CurrentLevel < FollowMePeak.Utils.LogLevel.Warning)
                return;
                
            var message = FormatDetectionMessage(score, flags, position, velocity, playerName);
            
            // Log to console
            logger.LogWarning(message);
            
            // Log to file if enabled
            if (fileLoggingEnabled)
            {
                WriteToFile(message);
            }
        }

        /// <summary>
        /// Logs when a climb is flagged due to fly detection
        /// </summary>
        public static void LogClimbFlagged(string climbName, float score, string playerName = "Unknown")
        {
            if (ModLogger.CurrentLevel < FollowMePeak.Utils.LogLevel.Warning)
                return;
                
            var message = new StringBuilder();
            message.AppendLine("[FlyDetection] === CLIMB FLAGGED ===");
            message.AppendLine($"[FlyDetection] Climb: {climbName}");
            message.AppendLine($"[FlyDetection] Player: {playerName}");
            message.AppendLine($"[FlyDetection] Detection Score: {score}/100");
            message.AppendLine($"[FlyDetection] Action: Forced to private");
            message.AppendLine($"[FlyDetection] Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            message.AppendLine("[FlyDetection] ===================");
            
            var messageStr = message.ToString();
            logger.LogWarning(messageStr);
            
            if (fileLoggingEnabled)
            {
                WriteToFile(messageStr);
            }
        }

        /// <summary>
        /// Logs a simple informational message
        /// </summary>
        public static void LogInfo(string message)
        {
            if (ModLogger.CurrentLevel < FollowMePeak.Utils.LogLevel.Info)
                return;
                
            logger.LogInfo($"[FlyDetection] {message}");
            
            if (fileLoggingEnabled)
            {
                WriteToFile($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
            }
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            if (ModLogger.CurrentLevel < FollowMePeak.Utils.LogLevel.Warning)
                return;
                
            logger.LogWarning($"[FlyDetection] {message}");
            
            if (fileLoggingEnabled)
            {
                WriteToFile($"[WARNING] {DateTime.Now:HH:mm:ss} - {message}");
            }
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void LogError(string message)
        {
            if (ModLogger.CurrentLevel < FollowMePeak.Utils.LogLevel.Error)
                return;
                
            logger.LogError($"[FlyDetection] {message}");
            
            if (fileLoggingEnabled)
            {
                WriteToFile($"[ERROR] {DateTime.Now:HH:mm:ss} - {message}");
            }
        }

        /// <summary>
        /// Formats a detection message with all relevant information
        /// </summary>
        private static string FormatDetectionMessage(float score, List<string> flags, Vector3 position, Vector3 velocity, string playerName)
        {
            var message = new StringBuilder();
            message.AppendLine("[FlyDetection] === FLY MOD DETECTED ===");
            message.AppendLine($"[FlyDetection] Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            message.AppendLine($"[FlyDetection] Player: {playerName}");
            message.AppendLine($"[FlyDetection] Score: {score}/100");
            
            if (flags != null && flags.Count > 0)
            {
                message.AppendLine("[FlyDetection] Detection Flags:");
                foreach (var flag in flags)
                {
                    message.AppendLine($"[FlyDetection]   • {flag}");
                }
            }
            
            message.AppendLine($"[FlyDetection] Position: X={position.x:F2}, Y={position.y:F2}, Z={position.z:F2}");
            message.AppendLine($"[FlyDetection] Velocity: X={velocity.x:F2}, Y={velocity.y:F2}, Z={velocity.z:F2} (Magnitude: {velocity.magnitude:F2})");
            message.AppendLine("[FlyDetection] =======================");
            
            return message.ToString();
        }

        /// <summary>
        /// Writes a message to the log file
        /// </summary>
        private static void WriteToFile(string message)
        {
            try
            {
                File.AppendAllText(logFilePath, message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Silently fail file logging to avoid disrupting gameplay
                logger.LogError($"Failed to write to log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Enables or disables file logging
        /// </summary>
        public static void SetFileLogging(bool enabled)
        {
            fileLoggingEnabled = enabled;
            
            if (enabled)
            {
                LogInfo($"File logging enabled. Logs will be saved to: {logFilePath}");
            }
            else
            {
                LogInfo("File logging disabled");
            }
        }

        /// <summary>
        /// Gets a summary of detections for the current session
        /// </summary>
        public static string GetSessionSummary(int detectionCount, float averageScore, TimeSpan sessionDuration)
        {
            var summary = new StringBuilder();
            summary.AppendLine("[FlyDetection] === SESSION SUMMARY ===");
            summary.AppendLine($"[FlyDetection] Session Duration: {sessionDuration.TotalMinutes:F1} minutes");
            summary.AppendLine($"[FlyDetection] Total Detections: {detectionCount}");
            summary.AppendLine($"[FlyDetection] Average Score: {averageScore:F1}/100");
            summary.AppendLine($"[FlyDetection] Log File: {logFilePath}");
            summary.AppendLine("[FlyDetection] ======================");
            
            return summary.ToString();
        }
    }
}