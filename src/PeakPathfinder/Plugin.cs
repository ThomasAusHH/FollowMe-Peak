using BepInEx;
using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;
using PeakPathfinder.Services;
using PeakPathfinder.Managers;
using PeakPathfinder.Models;
using PeakPathfinder.UI;
using PeakPathfinder.Patches;

namespace PeakPathfinder
{
    [BepInPlugin("com.thomasaushh.peakpathfinder", "Peak Pathfinder", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        // Existing services
        private PathDataService _pathDataService;
        private PathRecordingManager _recordingManager;
        private PathVisualizationManager _visualizationManager;
        private PathfinderUI _ui;
        
        // Cloud sync services
        private ServerConfigService _serverConfigService;
        private VPSApiService _vpsApiService;
        private PathUploadService _pathUploadService;
        private PathDownloadService _pathDownloadService;
        
        // Public access for services (needed by other components)
        public PathDataService PathDataService => _pathDataService;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"Plugin {Info.Metadata.GUID} wurde geladen!");
            
            InitializeServices();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            Harmony.CreateAndPatchAll(typeof(PluginPatches));
            Logger.LogInfo("Harmony Patches wurden angewendet.");
        }

        private void InitializeServices()
        {
            // Initialize core services
            _pathDataService = new PathDataService(Logger);
            _recordingManager = new PathRecordingManager(_pathDataService, Logger, this);
            _visualizationManager = new PathVisualizationManager(_pathDataService);
            
            // Initialize cloud sync services
            _serverConfigService = new ServerConfigService(Logger);
            _vpsApiService = new VPSApiService(Logger, _serverConfigService.Config, this);
            _pathUploadService = new PathUploadService(Logger, _vpsApiService, _serverConfigService);
            _pathDownloadService = new PathDownloadService(Logger, _vpsApiService, _serverConfigService, _pathDataService);
            
            // Initialize UI with cloud services
            _ui = new PathfinderUI(_pathDataService, _visualizationManager, _serverConfigService, 
                _vpsApiService, _pathUploadService, _pathDownloadService);
                
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
                _pathDataService.CurrentLevelID = "";
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
                _pathDataService.CurrentLevelID = $"{scene.name}_{levelIndex}";
                Logger.LogInfo($"Level erkannt: {_pathDataService.CurrentLevelID}");
                
                // Load local paths first
                _pathDataService.LoadPathsFromFile();
                
                // Download and merge cloud paths if enabled
                if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoDownload)
                {
                    _pathDownloadService.DownloadAndMergePaths(_pathDataService.CurrentLevelID, (count, error) =>
                    {
                        if (error == null && count > 0)
                        {
                            Logger.LogInfo($"Downloaded {count} new paths from cloud for level {_pathDataService.CurrentLevelID}");
                            // Refresh visualization after download
                            _visualizationManager.InitializePathVisibility();
                        }
                        else if (error != null && error != "Cloud sync disabled")
                        {
                            Logger.LogWarning($"Cloud download failed: {error}");
                        }
                    });
                }
                
                _visualizationManager.InitializePathVisibility();
            }
            else
            {
                Logger.LogError("NextLevelService oder dessen Daten konnten nicht gefunden werden!");
                _pathDataService.CurrentLevelID = scene.name + "_unknown";
            }
        }

        public void OnCampfireLit(string biomeName)
        {
            _recordingManager.SaveCurrentPath(biomeName);
            _recordingManager.StartRecording();
        }
        
        // Method to show tag selection for newly created paths
        public void ShowTagSelectionForNewPath(PathData pathData)
        {
            // Show the tag selection UI
            _ui.ShowTagSelectionForPath(pathData, (selectedPath) =>
            {
                // Callback when tags are selected - now upload to cloud
                if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoUpload)
                {
                    _pathUploadService.QueueForUpload(selectedPath, _pathDataService.CurrentLevelID);
                    Logger.LogInfo($"Queued path with tags for upload: {selectedPath.Id} - Tags: {selectedPath.GetTagsDisplay()}");
                }
            });
        }
    }
}