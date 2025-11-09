using BepInEx;
using BepInEx.Bootstrap;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;
using FollowMePeak.Services;
using FollowMePeak.Managers;
using FollowMePeak.Models;
using FollowMePeak.Patches;
using FollowMePeak.ModMenu;
using FollowMePeak.Utils;

namespace FollowMePeak
{
    [BepInPlugin("com.thomasaushh.followmepeak", "FollowMe-Peak", "1.0.3")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public const string MOD_VERSION = "1.0.3";

        // Controls Configuration
        public static BepInEx.Configuration.ConfigEntry<KeyCode> ModMenuToggleKey;
        
        // Gameplay Configuration  
        public static BepInEx.Configuration.ConfigEntry<bool> SaveDeathClimbs;
        
        // Logging Configuration
        public static BepInEx.Configuration.ConfigEntry<LogLevel> LoggingLevel;

        // Logger
        private ModLogger _modLogger;
        
        // Existing services
        private ClimbDataService _climbDataService;
        private ClimbRecordingManager _recordingManager;
        private ClimbVisualizationManager _visualizationManager;
        
        // Cloud sync services
        private ServerConfigService _serverConfigService;
        private VPSApiService _vpsApiService;
        private ClimbUploadService _climbUploadService;
        private ClimbDownloadService _climbDownloadService;
        
        // Mod Menu
        private ModMenuManager _modMenuManager;
        
        // Harmony instance for proper cleanup
        private Harmony _harmony;
        
        // Game state tracking
        private bool _gameEndedThisSession = false;
        
        // Mod activity tracking
        private Dictionary<string, DateTime> _lastModActivity = new Dictionary<string, DateTime>();
        private Dictionary<string, int> _modUsageCount = new Dictionary<string, int>();
        
        // Public access for services (needed by other components)
        public ClimbDataService ClimbDataService => _climbDataService;
        public ClimbRecordingManager GetRecordingManager() => _recordingManager;

        private void Awake()
        {
            Instance = this;
            _modLogger = new ModLogger(Logger);
            ModLogger.Instance = _modLogger;  // Set global instance
            _modLogger.Info($"Plugin {Info.Metadata.GUID} loaded!");
            
            // Initialize Controls Configuration
            InitializeControlsConfig();
            
            // Initialize Fly Detection (always enabled with fixed values)
            if (Detection.FlyDetectionConfig.IsEnabled)
            {
                _modLogger.Info("[FlyDetection] System initialized with fixed configuration");
                _modLogger.Info($"[FlyDetection] Threshold: {Detection.FlyDetectionConfig.DetectionThreshold}, CheckInterval: {Detection.FlyDetectionConfig.DetectionCheckInterval}");
            }
            
            InitializeServices();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            // Create Harmony instance with plugin GUID
            _harmony = new Harmony(Info.Metadata.GUID);
            _harmony.PatchAll(typeof(PluginPatches));
            
            // Apply death detection patches
            PlayerDeathPatch.ApplyPatch(_harmony);
            
            // Apply RunManager patch for run start detection
            RunManagerPatch.ApplyPatch(_harmony);
            
            // Apply EndGame patch for helicopter ending
            EndGamePatch.ApplyPatch(_harmony);
            
            _modLogger.Info("Harmony Patches applied. TEST");
            
            // Log all installed BepInEx mods (delayed to ensure all plugins are loaded)
            _modLogger.Info("[Plugin] About to start DelayedLogInstalledMods coroutine...");
            StartCoroutine(DelayedLogInstalledMods());
            _modLogger.Info("[Plugin] DelayedLogInstalledMods coroutine started!");
        }

        private void LogInstalledMods()
        {
            _modLogger.Info("=== Installed BepInEx Mods ===");
            
            var installedPlugins = Chainloader.PluginInfos;
            _modLogger.Info($"Total installed plugins: {installedPlugins.Count}");
            
            foreach (var plugin in installedPlugins.Values)
            {
                var metadata = plugin.Metadata;
                _modLogger.Info($"Plugin: {metadata.Name} v{metadata.Version} (GUID: {metadata.GUID})");
            }
            
            _modLogger.Info("==============================");
        }

        private IEnumerator DelayedLogInstalledMods()
        {
            _modLogger.Info("[Plugin] Starting delayed mod listing coroutine...");
            // Wait a few seconds to ensure all BepInEx plugins are loaded
            yield return new WaitForSeconds(3f);
            _modLogger.Info("[Plugin] 3 seconds elapsed, logging installed mods...");
            LogInstalledMods();
        }





        private void InitializeServices()
        {
            // Initialize core services
            _climbDataService = new ClimbDataService(_modLogger);
            _recordingManager = new ClimbRecordingManager(_climbDataService, _modLogger, this);
            _visualizationManager = new ClimbVisualizationManager(_climbDataService);
            
            // Initialize cloud sync services
            _serverConfigService = new ServerConfigService(_modLogger);
            _vpsApiService = new VPSApiService(_modLogger, _serverConfigService.Config, this);
            _climbUploadService = new ClimbUploadService(_modLogger, _vpsApiService, _serverConfigService);
            _climbDownloadService = new ClimbDownloadService(_modLogger, _vpsApiService, _serverConfigService, _climbDataService);
            
            // Initialize Mod Menu with services
            ModMenuManager.ServerConfig = _serverConfigService;
            ModMenuManager.ApiService = _vpsApiService;
            ModMenuManager.UploadService = _climbUploadService;
            ModMenuManager.DownloadService = _climbDownloadService;
            ModMenuManager.ClimbDataService = _climbDataService;
            ModMenuManager.VisualizationManager = _visualizationManager;
            _modMenuManager = new ModMenuManager();
            
            // Load AssetBundle for Mod Menu
            StartCoroutine(LoadModUIAssetBundle());
                
            _modLogger.Info("All services initialized successfully");
            
            // Initial server health check if cloud sync is enabled
            if (_serverConfigService.Config.EnableCloudSync)
            {
                _vpsApiService.CheckServerHealth((isHealthy) =>
                {
                    _modLogger.Info($"Initial server health check: {(isHealthy ? "Connected" : "Failed")}");
                });
            }
        }
        
        private void InitializeControlsConfig()
        {
            // Controls Configuration
            ModMenuToggleKey = Config.Bind(
                "Controls", 
                "ModMenuToggleKey", 
                KeyCode.F1, 
                "Key to toggle the mod menu"
            );
            
            // Gameplay Configuration
            SaveDeathClimbs = Config.Bind(
                "Gameplay",
                "SaveDeathClimbs", 
                false,
                "Save climbs where the player died (these will not be uploaded to cloud)"
            );
            
            // Logging Configuration
            LoggingLevel = Config.Bind(
                "Logging",
                "LogLevel",
                LogLevel.Error,
                "Logging level: None=0, Error=1, Warning=2, Info=3, Debug=4, Verbose=5"
            );
            
            // Initialize ModLogger with config
            ModLogger.CurrentLevel = LoggingLevel.Value;
            
            // Watch for config changes
            LoggingLevel.SettingChanged += (sender, args) =>
            {
                ModLogger.CurrentLevel = LoggingLevel.Value;
                _modLogger.Info($"[Config] LogLevel changed to: {LoggingLevel.Value}");
            };
            
            // No need to initialize FlyDetectionConfig anymore - it uses constants
            Detection.FlyDetectionConfig.ValidateConfig();
        }
        
        private void OnDestroy()
        {
            _modLogger.Info($"Plugin {Info.Metadata.GUID} unloading...");
            
            // Unsubscribe from scene events
            SceneManager.sceneLoaded -= OnSceneLoaded;
            
            // Unpatch all Harmony patches
            _harmony?.UnpatchSelf();
            _modLogger.Info("Harmony patches removed.");
            
            // Clean up Mod Menu
            _modMenuManager?.Cleanup();
            _modMenuManager = null;
            
            // Clean up managers
            _visualizationManager?.ClearVisuals();
            _visualizationManager = null;
            
            _recordingManager?.StopRecording();
            _recordingManager = null;
            
            // Clean up services
            _climbUploadService = null;
            _climbDownloadService = null;
            _vpsApiService = null;
            _serverConfigService = null;
            _climbDataService = null;
            
            // Clear static references
            ModMenuManager.ServerConfig = null;
            ModMenuManager.ApiService = null;
            ModMenuManager.UploadService = null;
            ModMenuManager.DownloadService = null;
            ModMenuManager.ClimbDataService = null;
            ModMenuManager.VisualizationManager = null;
            
            // Unload AssetBundle
            AssetBundleService.Instance.Unload();
            
            // Clear singleton instance
            Instance = null;
            
            _modLogger.Info($"Plugin {Info.Metadata.GUID} unloaded!");
        }

        private void Update()
        {
            if (Input.GetKeyDown(ModMenuToggleKey.Value))
            {
                _modLogger.Info($"[Plugin] {ModMenuToggleKey.Value} pressed - Toggling Mod Menu");
                _modMenuManager?.ToggleAssetBundleMenu();
            }
            
            // Update ModMenuManager for key recording
            _modMenuManager?.Update();
            
            // Perform fly detection checks (always enabled)
            if (Detection.FlyDetectionConfig.IsEnabled)
            {
                Detection.SimpleFlyDetector.PerformDetection();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Notify fly detector of scene change
            Detection.SimpleFlyDetector.OnSceneChanged(scene.name);
            
            if (scene.name.StartsWith("Level_"))
            {
                // Reset game ended flag when loading a new level
                if (_gameEndedThisSession)
                {
                    _gameEndedThisSession = false;
                    _modLogger.Info("[Level] Resetting game ended flag - new level started");
                }
                
                // Stop any existing recording from previous level
                if (_recordingManager != null && _recordingManager.IsRecording)
                {
                    _modLogger.Info($"Stopping previous recording due to scene change to {scene.name}");
                    _recordingManager.StopRecording();
                }
                
                StartCoroutine(InitializePathSystem(scene));
                // Recording now starts via RunManager event, not here
                _modLogger.Info($"Level {scene.name} loaded - waiting for RUN STARTED event");
            }
            else
            {
                // Stop recording when leaving to non-level scenes (Menu, Airport, etc.)
                if (_recordingManager != null && _recordingManager.IsRecording)
                {
                    _modLogger.Info($"Stopping recording due to leaving level (new scene: {scene.name}");
                    _recordingManager.StopRecording();
                }
                
                _climbDataService.CurrentLevelID = "";
                _visualizationManager.ClearVisuals();
            }
        }

        private IEnumerator LoadModUIAssetBundle()
        {
            _modLogger.Info("[AssetBundle] Starting AssetBundle load coroutine...");
            _modLogger.Info($"[AssetBundle] Current Directory: {System.IO.Directory.GetCurrentDirectory()}");
            _modLogger.Info($"[AssetBundle] BepInEx Plugin Path: {BepInEx.Paths.PluginPath}");
            _modLogger.Info($"[AssetBundle] Application.dataPath: {Application.dataPath}");
            
            bool loadComplete = false;
            bool loadSuccess = false;
            
            _modLogger.Info("[AssetBundle] Calling AssetBundleService.Instance.LoadModUIBundle...");
            
            yield return AssetBundleService.Instance.LoadModUIBundle((success) =>
            {
                _modLogger.Info($"[AssetBundle] LoadModUIBundle callback received: success={success}");
                loadComplete = true;
                loadSuccess = success;
            });
            
            // Add timeout check
            float timeout = 5f;
            float elapsed = 0f;
            
            _modLogger.Info($"[AssetBundle] Waiting for load completion (max {timeout} seconds)...");
            
            while (!loadComplete && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (!loadComplete)
            {
                _modLogger.Error($"[AssetBundle] Timeout waiting for AssetBundle load after {timeout} seconds");
                _modLogger.Error($"[AssetBundle] Service instance exists: {AssetBundleService.Instance != null}");
                _modLogger.Error($"[AssetBundle] Service IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            }
            else if (loadSuccess)
            {
                _modLogger.Info("[AssetBundle] Successfully loaded, notifying ModMenuManager");
                _modLogger.Info($"[AssetBundle] ModMenuManager exists: {_modMenuManager != null}");
                _modMenuManager?.OnAssetBundleLoaded();
                _modLogger.Info("[AssetBundle] OnAssetBundleLoaded called");
            }
            else
            {
                _modLogger.Error("[AssetBundle] AssetBundle loading failed - check AssetBundleService logs for details");
            }
            
            _modLogger.Info($"[AssetBundle] LoadModUIAssetBundle coroutine finished - Success: {loadSuccess}");
        }
        
        private IEnumerator InitializePathSystem(Scene scene)
        {
            yield return new WaitForSeconds(0.5f);
            var nextLevelService = GameHandler.GetService<NextLevelService>();
            if (nextLevelService != null && nextLevelService.Data.IsSome)
            {
                int levelIndex = nextLevelService.Data.Value.CurrentLevelIndex;
                _climbDataService.CurrentLevelID = $"{scene.name}_{levelIndex}";
                _modLogger.Info($"Level erkannt: {_climbDataService.CurrentLevelID}");
                
                // Load local paths first
                _climbDataService.LoadClimbsFromFile();
                
                // For server-side pagination, we don't pre-load data at startup
                // The UI will load data on-demand per page
                if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoDownload)
                {
                    _modLogger.Info("Server-side pagination enabled - data will be loaded per page in UI");
                }
                
                _visualizationManager.InitializeClimbVisibility();
            }
            else
            {
                _modLogger.Error("NextLevelService or its data could not be found!");
                _climbDataService.CurrentLevelID = scene.name + "_unknown";
            }
        }

        // Public method called by RunManagerPatch when a run starts
        public void OnRunStartedFromPatch()
        {
            // Check if game ended in this session (helicopter ending)
            if (_gameEndedThisSession)
            {
                _modLogger.Info("[RunManager] Ignoring RUN STARTED after helicopter ending");
                return;
            }
            
            _modLogger.Info("[RunManager] RUN STARTED - Activating fly detection and climb recording");
            
            // Start fly detection
            Detection.SimpleFlyDetector.OnRunStarted();
            
            // Start climb recording
            if (_recordingManager != null)
            {
                _recordingManager.StartRecording();
            }
            else
            {
                _modLogger.Error("RecordingManager is null when trying to start recording!");
            }
        }
        
        // Public method called by EndGamePatch when helicopter ending triggers
        public void OnHelicopterEnding()
        {
            // Additional safety check - should already be caught in EndGamePatch
            if (Managers.ClimbRecordingManager.PlayerDiedThisSession)
            {
                _modLogger.Info("[Helicopter] Ignoring helicopter ending - player already died this session");
                return;
            }
            
            _modLogger.Info("[Helicopter] Game ending detected - saving Peak climb");
            
            // Check if we're actually in the right scene/level
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            _modLogger.Info($"[Helicopter] Current scene: {currentScene}");
            
            // Save the final climb with Peak biome
            if (_recordingManager != null && _recordingManager.IsRecording)
            {
                // Force save even if recording is short
                _modLogger.Info($"[Helicopter] Recording active, saving as Peak");
                _recordingManager.SaveCurrentClimb("Peak");
                _modLogger.Info("[Helicopter] Final Peak climb saved");
            }
            else if (_recordingManager != null)
            {
                _modLogger.Warning("[Helicopter] No active recording to save for Peak");
            }
            else
            {
                _modLogger.Error("[Helicopter] RecordingManager is null!");
            }
            
            // Stop fly detection
            Detection.SimpleFlyDetector.OnSceneChanged("GameEnded");
            
            // Set flag to prevent new recordings until next level
            _gameEndedThisSession = true;
            _modLogger.Info("[Helicopter] Recording disabled until next level load");
        }
        
        public void OnCampfireLit(string biomeName)
        {
            _recordingManager.SaveCurrentClimb(biomeName);
            
            // Reset Fly Detection for new recording
            Detection.SimpleFlyDetector.ResetForNewRecording();
            
            _recordingManager.StartRecording();
        }
        
        public void ShowTagSelectionForNewClimb(ClimbData climbData)
        {
            // Directly upload if auto-upload is enabled
            UploadIfAutoUploadEnabled(climbData);
        }

        private void UploadIfAutoUploadEnabled(ClimbData climbData)
        {
            // Don't upload death climbs
            if (climbData.WasDeathClimb)
            {
                _modLogger.Info($"[Death] Death climb {climbData.Id} will not be uploaded to cloud");
                return;
            }
            
            if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoUpload)
            {
                _climbUploadService.QueueForUpload(climbData, _climbDataService.CurrentLevelID);
                _modLogger.Info($"Queued climb for upload: {climbData.Id}");
                // _modLogger.Info($"Queued climb with tags for upload: {climbData.Id} - Tags: {climbData.GetTagsDisplay()}");
            }
        }
    }
}