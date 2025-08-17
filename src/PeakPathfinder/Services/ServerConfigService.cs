using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using PeakPathfinder.Models;

namespace PeakPathfinder.Services
{
    public class ServerConfigService
    {
        private readonly ManualLogSource _logger;
        private readonly string _configPath;
        private ServerConfig _config;

        public ServerConfigService(ManualLogSource logger)
        {
            _logger = logger;
            _configPath = Path.Combine(Paths.PluginPath, "PeakPathfinder_Data", "server_config.json");
        }

        public ServerConfig Config => _config ?? LoadConfig();

        public ServerConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<ServerConfig>(json);
                    _logger.LogInfo("Server configuration loaded successfully");
                }
                else
                {
                    _config = CreateDefaultConfig();
                    SaveConfig();
                    _logger.LogInfo("Created default server configuration");
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to load server config: {e.Message}");
                _config = CreateDefaultConfig();
            }

            return _config;
        }

        public void SaveConfig()
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(_configPath);
                Directory.CreateDirectory(directory);

                string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                _logger.LogInfo("Server configuration saved");
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to save server config: {e.Message}");
            }
        }

        private ServerConfig CreateDefaultConfig()
        {
            return new ServerConfig
            {
                BaseUrl = "https://api.peakpathfinder.de", // Fixed server for all users
                EnableCloudSync = false, // Disabled by default for privacy
                AutoUpload = true,
                AutoDownload = true,
                TimeoutSeconds = 30,
                RetryAttempts = 3,
                PlayerName = GenerateRandomPlayerName(),
                MaxUploadsPerHour = 999999, // Effectively unlimited
                LastUploadReset = DateTime.Now,
                UploadsThisHour = 0
            };
        }

        private string GenerateRandomPlayerName()
        {
            string[] adjectives = { "Quick", "Silent", "Brave", "Swift", "Clever", "Bold", "Agile", "Smart" };
            string[] nouns = { "Explorer", "Pathfinder", "Runner", "Scout", "Climber", "Navigator", "Wanderer", "Seeker" };
            
            var random = new System.Random();
            string adjective = adjectives[random.Next(adjectives.Length)];
            string noun = nouns[random.Next(nouns.Length)];
            int number = random.Next(100, 999);
            
            return $"{adjective}{noun}_{number}";
        }

        // Configuration Update Methods
        // Server URL is fixed - no longer configurable
        // public void SetServerUrl(string url) - REMOVED

        public void SetCloudSyncEnabled(bool enabled)
        {
            _config.EnableCloudSync = enabled;
            SaveConfig();
            _logger.LogInfo($"Cloud sync {(enabled ? "enabled" : "disabled")}");
        }

        public void SetPlayerName(string playerName)
        {
            // Sanitize player name
            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = "Anonymous";
            }
            else
            {
                // Remove invalid characters
                playerName = System.Text.RegularExpressions.Regex.Replace(playerName, @"[^a-zA-Z0-9_-]", "");
                if (playerName.Length > 50) playerName = playerName.Substring(0, 50);
                if (string.IsNullOrWhiteSpace(playerName)) playerName = "Anonymous";
            }

            _config.PlayerName = playerName;
            SaveConfig();
            _logger.LogInfo($"Player name updated to: {_config.PlayerName}");
        }

        public void SetAutoUpload(bool enabled)
        {
            _config.AutoUpload = enabled;
            SaveConfig();
            _logger.LogInfo($"Auto upload {(enabled ? "enabled" : "disabled")}");
        }

        public void SetAutoDownload(bool enabled)
        {
            _config.AutoDownload = enabled;
            SaveConfig();
            _logger.LogInfo($"Auto download {(enabled ? "enabled" : "disabled")}");
        }

        // Reset upload rate limiting (for testing or admin override)
        public void ResetUploadRateLimit()
        {
            _config.UploadsThisHour = 0;
            _config.LastUploadReset = DateTime.Now;
            SaveConfig();
            _logger.LogInfo("Upload rate limit reset");
        }

        // Validate configuration
        public bool ValidateConfig()
        {
            if (string.IsNullOrWhiteSpace(_config.BaseUrl))
            {
                _logger.LogError("Server URL is required");
                return false;
            }

            if (!Uri.TryCreate(_config.BaseUrl, UriKind.Absolute, out Uri uri))
            {
                _logger.LogError("Invalid server URL format");
                return false;
            }

            if (_config.TimeoutSeconds < 5 || _config.TimeoutSeconds > 120)
            {
                _logger.LogWarning("Timeout should be between 5 and 120 seconds");
                _config.TimeoutSeconds = Math.Max(5, Math.Min(120, _config.TimeoutSeconds));
            }

            if (_config.MaxUploadsPerHour < 1 || _config.MaxUploadsPerHour > 100)
            {
                _logger.LogWarning("Max uploads per hour should be between 1 and 100");
                _config.MaxUploadsPerHour = Math.Max(1, Math.Min(100, _config.MaxUploadsPerHour));
            }

            return true;
        }
    }
}