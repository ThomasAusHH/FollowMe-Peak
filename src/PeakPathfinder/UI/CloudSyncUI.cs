using UnityEngine;
using PeakPathfinder.Services;

namespace PeakPathfinder.UI
{
    public class CloudSyncUI
    {
        private readonly ServerConfigService _configService;
        private readonly VPSApiService _apiService;
        private readonly PathUploadService _uploadService;
        private readonly PathDownloadService _downloadService;
        
        private string _playerNameInput = "";

        public CloudSyncUI(ServerConfigService configService, VPSApiService apiService,
            PathUploadService uploadService, PathDownloadService downloadService)
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
            GUILayout.Label("Pfade Online Teilen", GUI.skin.label);
            GUILayout.Space(5);
            
            // Main Cloud Sync Toggle - BIG and prominent
            GUI.skin.toggle.fontSize = 14;
            bool enableCloudSync = GUILayout.Toggle(config.EnableCloudSync, "  Meine Pfade automatisch mit anderen teilen", GUILayout.Height(30));
            GUI.skin.toggle.fontSize = 12;
            
            if (enableCloudSync != config.EnableCloudSync)
            {
                _configService.SetCloudSyncEnabled(enableCloudSync);
                
                // Automatically enable upload and download when cloud sync is enabled
                if (enableCloudSync)
                {
                    _configService.SetAutoUpload(true);
                    _configService.SetAutoDownload(true);
                }
            }

            GUILayout.Space(10);

            if (config.EnableCloudSync)
            {
                DrawSimpleCloudStatus();
            }
            else
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("Info: Wenn aktiviert:");
                GUILayout.Label("- Deine erfolgreichen Pfade werden automatisch geteilt");
                GUILayout.Label("- Du bekommst Pfade von anderen Spielern");
                GUILayout.Label("- Alles passiert automatisch im Hintergrund");
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
            GUILayout.Label("Spielername für geteilte Pfade:");
            
            GUILayout.BeginHorizontal();
            _playerNameInput = GUILayout.TextField(_playerNameInput, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Ändern", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_playerNameInput))
                {
                    _configService.SetPlayerName(_playerNameInput);
                }
            }
            GUILayout.EndHorizontal();
            
            var config = _configService.Config;
            GUILayout.Label($"Aktuell: {config.PlayerName}");
            
            GUILayout.EndVertical();
        }

        private void DrawSimpleCloudStatus()
        {
            GUILayout.BeginVertical("box");
            
            // Connection status
            GUILayout.BeginHorizontal();
            string statusText = _apiService.IsServerReachable ? "🟢 Verbunden" : "🔴 Offline";
            GUILayout.Label($"Status: {statusText}");
            
            if (GUILayout.Button("Verbindung testen", GUILayout.Width(120)))
            {
                _apiService.CheckServerHealth();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Upload/Download stats
            GUILayout.Label($"Upload-Status: {_uploadService.GetQueueStatus()}");
            GUILayout.Label($"Download-Status: {_downloadService.GetDownloadStats()}");
            
            GUILayout.Space(5);
            
            // Manual action buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Jetzt hochladen"))
            {
                _uploadService.ProcessQueue();
            }
            
            if (GUILayout.Button("Neue Pfade laden"))
            {
                var currentLevelId = Plugin.Instance.PathDataService.CurrentLevelID;
                if (!string.IsNullOrEmpty(currentLevelId))
                {
                    _downloadService.DownloadAndMergePaths(currentLevelId);
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
        }
    }
}