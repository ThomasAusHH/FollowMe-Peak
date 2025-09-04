using System;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using FollowMePeak.Models;
using FollowMePeak.Utils;

namespace FollowMePeak.Services
{
    public class ServerConfigService
    {
        private readonly ModLogger _logger;
        private readonly string _configPath;
        private ServerConfig _config;

        public ServerConfigService(ModLogger logger)
        {
            _logger = logger;
            _configPath = Path.Combine(Paths.PluginPath, "FollowMePeak_Data", "server_config.json");
        }

        public ServerConfig Config => _config ?? LoadConfig();

        public ServerConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<ServerConfig>(json, CommonJsonSettings.Default);
                    _logger.Info("Server configuration loaded successfully");
                }
                else
                {
                    _config = CreateDefaultConfig();
                    SaveConfig();
                    _logger.Info("Created default server configuration");
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Failed to load server config: {e.Message}");
                _config = CreateDefaultConfig();
            }

            return _config;
        }

        public void SaveConfig()
        {
            FileUtils.WriteJsonFileInBackground(_logger, _configPath, _config);
        }

        private ServerConfig CreateDefaultConfig()
        {
            return new ServerConfig
            {
                EnableCloudSync = true, // Enabled for testing
                PlayerName = GenerateRandomPlayerName(),
                LastUploadReset = DateTime.Now,
                UploadsThisHour = 0
            };
        }

        private string GenerateRandomPlayerName()
        {
            string[] adjectives = { 
                // Speed & Agility
                "Quick", "Swift", "Fast", "Rapid", "Lightning", "Turbo", "Blazing", "Speedy", "Nimble", "Agile",
                // Mountain-related
                "Rocky", "Steep", "Alpine", "Icy", "Snowy", "Windy", "High", "Rugged", "Vertical", "Frozen", 
                "Summit", "Peak", "Ridge", "Cliff", "Stone", "Granite", "Crystal", "Misty", "Foggy", "Stormy",
                // Personality traits  
                "Brave", "Bold", "Fearless", "Daring", "Wild", "Crazy", "Mad", "Epic", "Legendary", "Mighty",
                "Strong", "Tough", "Hardcore", "Extreme", "Silent", "Clever", "Smart", "Wise", "Sharp", "Keen",
                // Quirky & Funny
                "Dizzy", "Wobbly", "Clumsy", "Sleepy", "Hungry", "Thirsty", "Lost", "Confused", "Lucky", "Unlucky",
                "Weird", "Strange", "Funky", "Silly", "Goofy", "Bouncy", "Fuzzy", "Sparkly", "Shiny", "Glowing"
            };
            
            string[] nouns = { 
                // Climbing & Mountaineering
                "Climber", "Mountaineer", "Alpinist", "Scrambler", "Boulderer", "Summiteer", "Peakbagger", "Ridgewalker",
                "FollowMe", "Trailblazer", "Explorer", "Navigator", "Scout", "Wanderer", "Seeker", "Hiker", "Trekker",
                "Adventurer", "Voyager", "Pioneer", "Ranger", "Guide", "Sherpa", "Basecamp", "Expedition",
                // Mountain Animals
                "Goat", "Yak", "Eagle", "Hawk", "Raven", "Bear", "Wolf", "Fox", "Lynx", "Marmot", "Ibex", "Chamois",
                "Falcon", "Condor", "Vulture", "SnowLeopard", "Bighorn", "Pika", "Ptarmigan", "Wolverine",
                // Funny & Creative
                "Potato", "Pretzel", "Pickle", "Pancake", "Waffle", "Burrito", "Taco", "Pizza", "Bagel", "Donut",
                "Penguin", "Llama", "Walrus", "Hedgehog", "Squirrel", "Narwhal", "Unicorn", "Dragon", "Phoenix", "Yeti"
            };
            
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
            _logger.Info($"Cloud sync {(enabled ? "enabled" : "disabled")}");
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
            _logger.Info($"Player name updated to: {_config.PlayerName}");
        }

        // Auto upload/download settings are now hardcoded - methods removed

        // Reset upload rate limiting (for testing or admin override)
        public void ResetUploadRateLimit()
        {
            _config.UploadsThisHour = 0;
            _config.LastUploadReset = DateTime.Now;
            SaveConfig();
            _logger.Info("Upload rate limit reset");
        }

        // Validate configuration
        public bool ValidateConfig()
        {
            // BaseUrl, TimeoutSeconds, MaxUploadsPerHour are now hardcoded
            // Only validate user-configurable settings
            return true;
        }
    }
}