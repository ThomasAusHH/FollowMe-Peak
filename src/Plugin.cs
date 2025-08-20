using BepInEx;
using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;
using FollowMePeak.Services;
using FollowMePeak.Managers;
using FollowMePeak.Models;
using FollowMePeak.UI;
using FollowMePeak.Patches;

namespace FollowMePeak
{
    [BepInPlugin("com.thomasaushh.followmepeak", "FollowMe-Peak", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        // Existing services
        private ClimbDataService _climbDataService;
        private ClimbRecordingManager _recordingManager;
        private ClimbVisualizationManager _visualizationManager;
        private FollowMeUI _ui;
        
        // Cloud sync services
        private ServerConfigService _serverConfigService;
        private VPSApiService _vpsApiService;
        private ClimbUploadService _climbUploadService;
        private ClimbDownloadService _climbDownloadService;
        
        // Public access for services (needed by other components)
        public ClimbDataService ClimbDataService => _climbDataService;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"Plugin {Info.Metadata.GUID} loaded!");
            
            InitializeServices();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            Harmony.CreateAndPatchAll(typeof(PluginPatches));
            Logger.LogInfo("Harmony Patches applied.");
        }

        private void InitializeServices()
        {
            // Initialize core services
            _climbDataService = new ClimbDataService(Logger);
            _recordingManager = new ClimbRecordingManager(_climbDataService, Logger, this);
            _visualizationManager = new ClimbVisualizationManager(_climbDataService);
            
            // Initialize cloud sync services
            _serverConfigService = new ServerConfigService(Logger);
            _vpsApiService = new VPSApiService(Logger, _serverConfigService.Config, this);
            _climbUploadService = new ClimbUploadService(Logger, _vpsApiService, _serverConfigService);
            _climbDownloadService = new ClimbDownloadService(Logger, _vpsApiService, _serverConfigService, _climbDataService);
            
            // Initialize UI with cloud services
            _ui = new FollowMeUI(_climbDataService, _visualizationManager, _serverConfigService, 
                _vpsApiService, _climbUploadService, _climbDownloadService);
                
            Logger.LogInfo("All services initialized successfully");
            
            // Initial server health check if cloud sync is enabled
            if (_serverConfigService.Config.EnableCloudSync)
            {
                _vpsApiService.CheckServerHealth((isHealthy) =>
                {
                    Logger.LogInfo($"Initial server health check: {(isHealthy ? "Connected" : "Failed")}");
                });
            }
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _ui.ShowMenu = !_ui.ShowMenu;
            }
        }
        
        private void OnGUI()
        {
            _ui.OnGUI();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.StartsWith("Level_"))
            {
                StartCoroutine(InitializePathSystem(scene));
                _recordingManager.StartRecording();
            }
            else
            {
                _climbDataService.CurrentLevelID = "";
                _visualizationManager.ClearVisuals();
            }
        }

        private IEnumerator InitializePathSystem(Scene scene)
        {
            yield return new WaitForSeconds(0.5f);
            var nextLevelService = GameHandler.GetService<NextLevelService>();
            if (nextLevelService != null && nextLevelService.Data.IsSome)
            {
                int levelIndex = nextLevelService.Data.Value.CurrentLevelIndex;
                _climbDataService.CurrentLevelID = $"{scene.name}_{levelIndex}";
                Logger.LogInfo($"Level erkannt: {_climbDataService.CurrentLevelID}");
                
                // Load local paths first
                _climbDataService.LoadClimbsFromFile();
                
                // Download and merge cloud climbs if enabled
                if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoDownload)
                {
                    _climbDownloadService.DownloadAndMergeClimbs(_climbDataService.CurrentLevelID, (count, error) =>
                    {
                        if (error == null && count > 0)
                        {
                            Logger.LogInfo($"Downloaded {count} new climbs from cloud for level {_climbDataService.CurrentLevelID}");
                            // Refresh visualization after download
                            _visualizationManager.InitializeClimbVisibility();
                        }
                        else if (error != null && error != "Cloud sync disabled")
                        {
                            Logger.LogWarning($"Cloud download failed: {error}");
                        }
                    });
                }
                
                _visualizationManager.InitializeClimbVisibility();
            }
            else
            {
                Logger.LogError("NextLevelService or its data could not be found!");
                _climbDataService.CurrentLevelID = scene.name + "_unknown";
            }
        }

        public void OnCampfireLit(string biomeName)
        {
            _recordingManager.SaveCurrentClimb(biomeName);
            _recordingManager.StartRecording();
        }
        
        // Method to show tag selection for newly created climbs - DISABLED FOR NOW
        public void ShowTagSelectionForNewClimb(ClimbData climbData)
        {
            // Skip tag selection for now and directly upload if auto-upload is enabled
            if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoUpload)
            {
                _climbUploadService.QueueForUpload(climbData, _climbDataService.CurrentLevelID);
                Logger.LogInfo($"Queued climb for upload: {climbData.Id}");
            }
            
            /* ORIGINAL CODE - DISABLED FOR NOW
            // Show the tag selection UI
            _ui.ShowTagSelectionForClimb(climbData, (selectedClimb) =>
            {
                // Callback when tags are selected - now upload to cloud
                if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoUpload)
                {
                    _climbUploadService.QueueForUpload(selectedClimb, _climbDataService.CurrentLevelID);
                    // Logger.LogInfo($"Queued climb with tags for upload: {selectedClimb.Id} - Tags: {selectedClimb.GetTagsDisplay()}");
                }
            });
            */
        }
    }
}