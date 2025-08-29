using UnityEngine;
using FollowMePeak.Services;
using FollowMePeak.Managers;
using FollowMePeak.ModMenu.UI;
using FollowMePeak.ModMenu.UI.Tabs;

namespace FollowMePeak.ModMenu
{
    /// <summary>
    /// Main UI controller that coordinates all UI components
    /// </summary>
    public class ModMenuUIController
    {
        // Tab Controllers
        private TabManager _tabManager;
        private ClimbsTabController _climbsTab;
        private CloudSyncTabController _cloudSyncTab;
        
        // Services
        private ServerConfigService _serverConfig;
        private VPSApiService _apiService;
        private ClimbUploadService _uploadService;
        private ClimbDownloadService _downloadService;
        private ClimbDataService _climbDataService;
        private ClimbVisualizationManager _visualizationManager;
        
        public void Initialize(GameObject menuRoot)
        {
            Debug.Log("[ModMenuUI] Initializing UI Controller");
            
            // Get services from ModMenuManager
            InitializeServices();
            
            // Initialize tab manager
            _tabManager = new TabManager();
            _tabManager.Initialize(menuRoot);
            _tabManager.OnTabChanged += OnTabChanged;
            
            // Initialize tab controllers
            InitializeTabControllers(menuRoot);
            
            // Set initial tab
            _tabManager.SetActiveTab("Climbs");
            
            Debug.Log("[ModMenuUI] UI Controller initialized successfully");
        }
        
        private void InitializeServices()
        {
            _serverConfig = ModMenuManager.ServerConfig;
            _apiService = ModMenuManager.ApiService;
            _uploadService = ModMenuManager.UploadService;
            _downloadService = ModMenuManager.DownloadService;
            _climbDataService = ModMenuManager.ClimbDataService;
            _visualizationManager = ModMenuManager.VisualizationManager;
        }
        
        private void InitializeTabControllers(GameObject menuRoot)
        {
            // Initialize Climbs tab
            _climbsTab = new ClimbsTabController();
            _climbsTab.Initialize(menuRoot, _apiService, _climbDataService, _visualizationManager);
            
            // Initialize Cloud Sync tab
            _cloudSyncTab = new CloudSyncTabController();
            _cloudSyncTab.Initialize(menuRoot, _serverConfig, _apiService);
        }
        
        private void OnTabChanged(string tabName)
        {
            Debug.Log($"[ModMenuUI] Tab changed to: {tabName}");
            
            switch (tabName)
            {
                case "Climbs":
                    _climbsTab?.OnShow();  // This will check for server data and refresh
                    break;
                    
                case "CloudSync":
                    _cloudSyncTab?.LoadSettings();
                    break;
            }
        }
        
        // Called when the menu is opened
        public void OnMenuOpened()
        {
            Debug.Log("[ModMenuUI] Menu opened - triggering OnShow for active tab");
            
            // Get the current active tab from TabManager and trigger OnShow
            string activeTab = _tabManager?.GetActiveTab();
            if (!string.IsNullOrEmpty(activeTab))
            {
                OnTabChanged(activeTab);
            }
        }
        
        public void Cleanup()
        {
            Debug.Log("[ModMenuUI] Cleaning up UI Controller");
            
            // Cleanup tab controllers
            _climbsTab?.Cleanup();
            _cloudSyncTab?.Cleanup();
            
            // Cleanup tab manager
            _tabManager?.Cleanup();
            
            // Clear event handlers
            if (_tabManager != null)
            {
                _tabManager.OnTabChanged -= OnTabChanged;
            }
        }
    }
}