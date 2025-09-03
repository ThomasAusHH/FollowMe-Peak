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
using FollowMePeak.Patches;
using FollowMePeak.ModMenu;

namespace FollowMePeak
{
    [BepInPlugin("com.thomasaushh.followmepeak", "FollowMe-Peak", "1.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public const string MOD_VERSION = "1.0.2";

        // Controls Configuration
        public static BepInEx.Configuration.ConfigEntry<KeyCode> ModMenuToggleKey;
        
        // Gameplay Configuration  
        public static BepInEx.Configuration.ConfigEntry<bool> SaveDeathClimbs;

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
        
        // Public access for services (needed by other components)
        public ClimbDataService ClimbDataService => _climbDataService;
        public ClimbRecordingManager GetRecordingManager() => _recordingManager;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"Plugin {Info.Metadata.GUID} loaded!");
            
            // Initialize Controls Configuration
            InitializeControlsConfig();
            
            // Initialize Fly Detection (always enabled with fixed values)
            if (Detection.FlyDetectionConfig.IsEnabled)
            {
                Logger.LogInfo("[FlyDetection] System initialized with fixed configuration");
                Logger.LogInfo($"[FlyDetection] Threshold: {Detection.FlyDetectionConfig.DetectionThreshold}, CheckInterval: {Detection.FlyDetectionConfig.DetectionCheckInterval}");
            }
            
            InitializeServices();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            // Create Harmony instance with plugin GUID
            _harmony = new Harmony(Info.Metadata.GUID);
            _harmony.PatchAll(typeof(PluginPatches));
            
            // Apply death detection patches
            PlayerDeathPatch.ApplyPatch(_harmony);
            
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
            
            // No need to initialize FlyDetectionConfig anymore - it uses constants
            Detection.FlyDetectionConfig.ValidateConfig();
        }
        
        private void OnDestroy()
        {
            Logger.LogInfo($"Plugin {Info.Metadata.GUID} unloading...");
            
            // Unsubscribe from scene events
            SceneManager.sceneLoaded -= OnSceneLoaded;
            
            // Unpatch all Harmony patches
            _harmony?.UnpatchSelf();
            Logger.LogInfo("Harmony patches removed.");
            
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
            
            Logger.LogInfo($"Plugin {Info.Metadata.GUID} unloaded!");
        }

        private void Update()
        {
            if (Input.GetKeyDown(ModMenuToggleKey.Value))
            {
                Logger.LogInfo($"[Plugin] {ModMenuToggleKey.Value} pressed - Toggling Mod Menu");
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
                StartCoroutine(InitializePathSystem(scene));
                _recordingManager.StartRecording();
            }
            else
            {
                _climbDataService.CurrentLevelID = "";
                _visualizationManager.ClearVisuals();
            }
        }

        private IEnumerator LoadModUIAssetBundle()
        {
            Logger.LogInfo("[AssetBundle] Starting AssetBundle load coroutine...");
            Logger.LogInfo($"[AssetBundle] Current Directory: {System.IO.Directory.GetCurrentDirectory()}");
            Logger.LogInfo($"[AssetBundle] BepInEx Plugin Path: {BepInEx.Paths.PluginPath}");
            Logger.LogInfo($"[AssetBundle] Application.dataPath: {Application.dataPath}");
            
            bool loadComplete = false;
            bool loadSuccess = false;
            
            Logger.LogInfo("[AssetBundle] Calling AssetBundleService.Instance.LoadModUIBundle...");
            
            yield return AssetBundleService.Instance.LoadModUIBundle((success) =>
            {
                Logger.LogInfo($"[AssetBundle] LoadModUIBundle callback received: success={success}");
                loadComplete = true;
                loadSuccess = success;
            });
            
            // Add timeout check
            float timeout = 5f;
            float elapsed = 0f;
            
            Logger.LogInfo($"[AssetBundle] Waiting for load completion (max {timeout} seconds)...");
            
            while (!loadComplete && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (!loadComplete)
            {
                Logger.LogError($"[AssetBundle] Timeout waiting for AssetBundle load after {timeout} seconds");
                Logger.LogError($"[AssetBundle] Service instance exists: {AssetBundleService.Instance != null}");
                Logger.LogError($"[AssetBundle] Service IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            }
            else if (loadSuccess)
            {
                Logger.LogInfo("[AssetBundle] Successfully loaded, notifying ModMenuManager");
                Logger.LogInfo($"[AssetBundle] ModMenuManager exists: {_modMenuManager != null}");
                _modMenuManager?.OnAssetBundleLoaded();
                Logger.LogInfo("[AssetBundle] OnAssetBundleLoaded called");
            }
            else
            {
                Logger.LogError("[AssetBundle] AssetBundle loading failed - check AssetBundleService logs for details");
            }
            
            Logger.LogInfo($"[AssetBundle] LoadModUIAssetBundle coroutine finished - Success: {loadSuccess}");
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
                
                // For server-side pagination, we don't pre-load data at startup
                // The UI will load data on-demand per page
                if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoDownload)
                {
                    Logger.LogInfo("Server-side pagination enabled - data will be loaded per page in UI");
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
                Logger.LogInfo($"[Death] Death climb {climbData.Id} will not be uploaded to cloud");
                return;
            }
            
            if (_serverConfigService.Config.EnableCloudSync && _serverConfigService.Config.AutoUpload)
            {
                _climbUploadService.QueueForUpload(climbData, _climbDataService.CurrentLevelID);
                Logger.LogInfo($"Queued climb for upload: {climbData.Id}");
                // Logger.LogInfo($"Queued climb with tags for upload: {climbData.Id} - Tags: {climbData.GetTagsDisplay()}");
            }
        }
    }
}