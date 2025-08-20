using UnityEngine;
using FollowMePeak.Services;

namespace FollowMePeak.UI
{
    public class CloudSyncUI
    {
        private readonly ServerConfigService _configService;
        private readonly VPSApiService _apiService;
        private readonly ClimbUploadService _uploadService;
        private readonly ClimbDownloadService _downloadService;
        
        private string _playerNameInput = "";

        public CloudSyncUI(ServerConfigService configService, VPSApiService apiService,
            ClimbUploadService uploadService, ClimbDownloadService downloadService)
        {
            _configService = configService;
            _apiService = apiService;
            _uploadService = uploadService;
            _downloadService = downloadService;
            
            // Initialize input fields with current config
            var config = _configService.Config;
            _playerNameInput = config.PlayerName;
        }

        public void OnGUI()
        {
            var config = _configService.Config;
            
            GUILayout.BeginVertical("box");
            
            // Simple header
            GUILayout.Label("Share Climbs Online", GUI.skin.label);
            GUILayout.Space(5);
            
            // Main Cloud Sync Toggle - BIG and prominent
            GUI.skin.toggle.fontSize = 14;
            bool enableCloudSync = GUILayout.Toggle(config.EnableCloudSync, "  Automatically share my climbs with others", GUILayout.Height(30));
            GUI.skin.toggle.fontSize = 12;
            
            if (enableCloudSync != config.EnableCloudSync)
            {
                _configService.SetCloudSyncEnabled(enableCloudSync);
                
                // Auto upload/download are now hardcoded as true - no need to set them
            }

            GUILayout.Space(10);

            if (config.EnableCloudSync)
            {
                DrawSimpleCloudStatus();
            }
            else
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("Info: When enabled:");
                GUILayout.Label("- Your successful climbs will be automatically shared");
                GUILayout.Label("- You will receive climbs from other players");
                GUILayout.Label("- Everything happens automatically in the background");
                GUILayout.EndVertical();
            }

            // Player name setting (simplified)
            if (config.EnableCloudSync)
            {
                GUILayout.Space(10);
                DrawPlayerNameSetting();
            }

            GUILayout.EndVertical();
        }


        private void DrawPlayerNameSetting()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Player name for shared climbs:");
            
            GUILayout.BeginHorizontal();
            _playerNameInput = GUILayout.TextField(_playerNameInput, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Change", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_playerNameInput))
                {
                    _configService.SetPlayerName(_playerNameInput);
                }
            }
            GUILayout.EndHorizontal();
            
            var config = _configService.Config;
            GUILayout.Label($"Current: {config.PlayerName}");
            
            GUILayout.EndVertical();
        }

        private void DrawSimpleCloudStatus()
        {
            GUILayout.BeginVertical("box");
            
            // Connection status
            GUILayout.BeginHorizontal();
            string statusText = _apiService.IsServerReachable ? "● Connected" : "● Offline";
            GUILayout.Label($"Status: {statusText}");
            
            if (GUILayout.Button("Test Connection", GUILayout.Width(120)))
            {
                _apiService.CheckServerHealth();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Upload/Download stats
            GUILayout.Label($"Upload Status: {_uploadService.GetQueueStatus()}");
            GUILayout.Label($"Download Status: {_downloadService.GetDownloadStats()}");
            
            GUILayout.Space(5);
            
            // Manual action buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Upload Now"))
            {
                _uploadService.ProcessQueue();
            }
            
            if (GUILayout.Button("Load New Climbs"))
            {
                var currentLevelId = Plugin.Instance.ClimbDataService.CurrentLevelID;
                if (!string.IsNullOrEmpty(currentLevelId))
                {
                    _downloadService.DownloadAndMergeClimbs(currentLevelId);
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
        }
    }
}