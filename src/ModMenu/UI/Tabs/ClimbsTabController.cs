using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using FollowMePeak.Services;
using FollowMePeak.Models;
using FollowMePeak.Managers;
using FollowMePeak.ModMenu.UI.Helpers;
using FollowMePeak.ModMenu.UI.Tabs.Components;

namespace FollowMePeak.ModMenu.UI.Tabs
{
    public class ClimbsTabController
    {
        // UI Elements
        private GameObject _climbsPage;
        private Toggle _beachToggle;
        private Toggle _tropicsToggle;
        private Toggle _alpineMesaToggle;
        private Toggle _calderaToggle;
        private TMP_InputField _climbCodeInput;
        private Button _climbCodeSearchButton;
        private Transform _scrollViewport;
        private ScrollRect _scrollRect;
        
        // Components
        private ClimbListItemManager _itemManager;
        private ClimbFilterManager _filterManager;
        private ClimbSearchManager _searchManager;
        private ClimbServerLoader _serverLoader;
        
        // State
        private HashSet<string> _visibleClimbIds = new HashSet<string>();
        private bool _showOnlyLocalClimbs = false;
        
        // Services
        private ClimbDataService _climbDataService;
        private ClimbVisualizationManager _visualizationManager;
        
        public GameObject ClimbsPage => _climbsPage;
        
        public void Initialize(GameObject root, VPSApiService apiService, 
            ClimbDataService climbDataService, ClimbVisualizationManager visualizationManager)
        {
            _climbDataService = climbDataService;
            _visualizationManager = visualizationManager;
            
            FindUIElements(root);
            InitializeComponents(apiService);
            SetupUI();
        }
        
        private void InitializeComponents(VPSApiService apiService)
        {
            _filterManager = new ClimbFilterManager();
            _searchManager = new ClimbSearchManager(apiService, _climbDataService);
            _serverLoader = new ClimbServerLoader(apiService, _climbDataService);
            
            // Subscribe to search events
            _searchManager.OnClimbFound += OnClimbFoundFromSearch;
            
            // Subscribe to server loader events
            _serverLoader.OnServerClimbsLoaded += OnServerClimbsLoaded;
            _serverLoader.OnLoadError += OnServerLoadError;
            _serverLoader.OnPaginationUpdated += OnPaginationUpdated;
        }
        
        private void FindUIElements(GameObject root)
        {
            Debug.Log("[ClimbsTab] Finding UI elements");
            
            // Find Climbs Page
            Transform pages = root.transform.Find("MyModMenuPanel/Pages");
            _climbsPage = pages?.Find("ClimbsPage")?.gameObject;
            
            if (_climbsPage == null) return;
            
            // Find Toggles
            Transform toggleGroup = _climbsPage.transform.Find("ToggleGroupContainer");
            if (toggleGroup != null)
            {
                _beachToggle = UIElementFinder.FindComponent<Toggle>(toggleGroup, "BeachToggle");
                _tropicsToggle = UIElementFinder.FindComponent<Toggle>(toggleGroup, "TropicsToggle");
                _alpineMesaToggle = UIElementFinder.FindComponent<Toggle>(toggleGroup, "AlpineMesaToggle");
                _calderaToggle = UIElementFinder.FindComponent<Toggle>(toggleGroup, "CalderaToggle");
            }
            
            // Find Search Elements
            Transform climbCodeSearch = _climbsPage.transform.Find("ClimbCodeSearch");
            if (climbCodeSearch != null)
            {
                _climbCodeInput = UIElementFinder.FindComponent<TMP_InputField>(climbCodeSearch, "ClimbCodeEnter");
                _climbCodeSearchButton = UIElementFinder.FindComponent<Button>(climbCodeSearch, "ClimbCodeSearchButton");
            }
            
            // Find ScrollView elements
            FindScrollViewElements();
        }
        
        private void FindScrollViewElements()
        {
            Transform scrollView = UIElementFinder.FindTransform(_climbsPage.transform, "ClimbsScrollView");
            if (scrollView == null)
            {
                Debug.LogError("[ClimbsTab] ScrollView not found!");
                return;
            }
            
            _scrollViewport = scrollView.Find("ClimbsScrollViewport");
            if (_scrollViewport == null)
            {
                Debug.LogError("[ClimbsTab] Viewport not found!");
                return;
            }
            
            // Find template
            Transform template = _scrollViewport.Find("ClimbsScrollContent");
            if (template == null)
                template = _scrollViewport.Find("ClimbScrollContent");
            
            if (template != null)
            {
                template.gameObject.SetActive(false);
                _itemManager = new ClimbListItemManager(template.gameObject, _scrollViewport);
                Debug.Log("[ClimbsTab] Template found and item manager initialized");
            }
            else
            {
                Debug.LogError("[ClimbsTab] Template not found in viewport!");
            }
            
            SetupScrollRect(scrollView);
        }
        
        private void SetupScrollRect(Transform scrollView)
        {
            _scrollRect = scrollView.GetComponent<ScrollRect>();
            if (_scrollRect == null)
                _scrollRect = scrollView.gameObject.AddComponent<ScrollRect>();
            
            // Configure ScrollRect
            _scrollRect.content = _scrollViewport.GetComponent<RectTransform>();
            _scrollRect.viewport = _scrollViewport.GetComponent<RectTransform>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.scrollSensitivity = 30f;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            
            // Setup layout
            var layoutGroup = _scrollViewport.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
                layoutGroup = _scrollViewport.gameObject.AddComponent<VerticalLayoutGroup>();
            
            layoutGroup.spacing = 10f;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.padding = new RectOffset(10, 10, 10, 10);
            
            // Setup ContentSizeFitter
            var sizeFitter = _scrollViewport.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
                sizeFitter = _scrollViewport.gameObject.AddComponent<ContentSizeFitter>();
            
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            
            // Add clipping
            var rectMask = scrollView.GetComponent<RectMask2D>();
            if (rectMask == null)
                scrollView.gameObject.AddComponent<RectMask2D>();
        }
        
        private void SetupUI()
        {
            SetupToggles();
            SetupSearchButton();
        }
        
        private void SetupToggles()
        {
            // Setup toggle group
            ToggleGroup toggleGroup = null;
            if (_beachToggle != null)
            {
                Transform container = _beachToggle.transform.parent;
                toggleGroup = container?.GetComponent<ToggleGroup>();
                if (toggleGroup == null && container != null)
                {
                    toggleGroup = container.gameObject.AddComponent<ToggleGroup>();
                    toggleGroup.allowSwitchOff = true;
                }
            }
            
            // Setup individual toggles
            SetupToggle(_beachToggle, toggleGroup, ClimbFilterManager.BiomeFilter.Beach);
            SetupToggle(_tropicsToggle, toggleGroup, ClimbFilterManager.BiomeFilter.Tropics);
            SetupToggle(_alpineMesaToggle, toggleGroup, ClimbFilterManager.BiomeFilter.AlpineMesa);
            SetupToggle(_calderaToggle, toggleGroup, ClimbFilterManager.BiomeFilter.Caldera);
        }
        
        private void SetupToggle(Toggle toggle, ToggleGroup group, ClimbFilterManager.BiomeFilter filter)
        {
            if (toggle == null) return;
            
            toggle.group = group;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener((bool value) => {
                if (value)
                {
                    Debug.Log($"[ClimbsTab] Filter activated: {filter}");
                    _filterManager.SetFilter(filter);
                    RefreshClimbsList();
                }
                else if (!IsAnyFilterActive())
                {
                    Debug.Log("[ClimbsTab] All filters deactivated, showing all climbs");
                    _filterManager.SetFilter(ClimbFilterManager.BiomeFilter.All);
                    RefreshClimbsList();
                }
            });
        }
        
        private void SetupSearchButton()
        {
            if (_climbCodeSearchButton != null)
            {
                _climbCodeSearchButton.onClick.RemoveAllListeners();
                _climbCodeSearchButton.onClick.AddListener(() => {
                    _searchManager.SearchByCode(_climbCodeInput?.text);
                });
            }
        }
        
        public void RefreshClimbsList()
        {
            Debug.Log("[ClimbsTab] Refreshing climbs list");
            
            if (_itemManager == null || _climbDataService == null)
            {
                Debug.LogError("[ClimbsTab] Cannot refresh - missing components");
                return;
            }
            
            _itemManager.ClearAllItems();
            
            // Get climbs based on mode
            var climbsToDisplay = GetClimbsToDisplay();
            
            Debug.Log($"[ClimbsTab] Showing {climbsToDisplay.Count} climbs");
            
            foreach (var climb in climbsToDisplay)
            {
                bool isVisible = _visibleClimbIds.Contains(climb.Id.ToString());
                _itemManager.CreateClimbItem(climb, OnClimbVisibilityToggled, isVisible);
            }
            
            // Force layout rebuild
            if (_scrollViewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollViewport.GetComponent<RectTransform>());
            }
        }
        
        private System.Collections.Generic.List<ClimbData> GetClimbsToDisplay()
        {
            var displayClimbs = new System.Collections.Generic.List<ClimbData>();
            
            if (_showOnlyLocalClimbs)
            {
                // Show only local climbs (not from cloud)
                var localClimbs = _climbDataService.GetAllClimbs()
                    .Where(c => !c.IsFromCloud);
                displayClimbs.AddRange(_filterManager.FilterClimbs(localClimbs.ToList()));
            }
            else
            {
                // Combined mode: server climbs + local climbs
                // Apply filter to server climbs as well
                var filteredServerClimbs = _filterManager.FilterClimbs(_serverLoader.CurrentPageClimbs);
                displayClimbs.AddRange(filteredServerClimbs);
                
                // Add local climbs that are not from cloud (also filtered)
                var localClimbs = _climbDataService.GetAllClimbs()
                    .Where(c => !c.IsFromCloud);
                var filteredLocalClimbs = _filterManager.FilterClimbs(localClimbs.ToList());
                displayClimbs.AddRange(filteredLocalClimbs);
            }
            
            return displayClimbs;
        }
        
        private void OnClimbVisibilityToggled(ClimbData climb, bool isVisible)
        {
            Debug.Log($"[ClimbsTab] Climb {climb.Id} visibility: {isVisible}");
            
            if (isVisible)
                _visibleClimbIds.Add(climb.Id.ToString());
            else
                _visibleClimbIds.Remove(climb.Id.ToString());
            
            _visualizationManager?.SetClimbVisibility(climb.Id, isVisible);
        }
        
        private void OnClimbFoundFromSearch(ClimbData climb)
        {
            _visibleClimbIds.Add(climb.Id.ToString());
            
            if (_visualizationManager != null)
            {
                _visualizationManager.UpdateVisuals();
                _visualizationManager.SetClimbVisibility(climb.Id, true);
            }
            
            if (_climbCodeInput != null)
                _climbCodeInput.text = "";
            
            RefreshClimbsList();
        }
        
        private bool IsAnyFilterActive()
        {
            return (_beachToggle?.isOn ?? false) ||
                   (_tropicsToggle?.isOn ?? false) ||
                   (_alpineMesaToggle?.isOn ?? false) ||
                   (_calderaToggle?.isOn ?? false);
        }
        
        // Called when the climbs page becomes visible
        public void OnShow()
        {
            Debug.Log("[ClimbsTab] OnShow - Checking for server data");
            
            // Check and load initial server data if player is in level
            _serverLoader?.CheckAndLoadInitialData();
            
            // Refresh the list to show current data
            RefreshClimbsList();
        }
        
        // Server loader event handlers
        private void OnServerClimbsLoaded(List<ClimbData> serverClimbs)
        {
            Debug.Log($"[ClimbsTab] Server climbs loaded: {serverClimbs.Count} items");
            
            // Update visualization manager with new climbs
            if (_visualizationManager != null)
            {
                _visualizationManager.UpdateVisuals();
            }
            
            // Refresh the display
            RefreshClimbsList();
        }
        
        private void OnServerLoadError(string error)
        {
            Debug.LogWarning($"[ClimbsTab] Server load error: {error}");
            // Could show this error in UI if needed
        }
        
        private void OnPaginationUpdated(int currentPage, int totalPages)
        {
            Debug.Log($"[ClimbsTab] Pagination updated: Page {currentPage + 1}/{totalPages}");
            // Could update pagination UI here if needed
        }
        
        public void Cleanup()
        {
            Debug.Log("[ClimbsTab] Cleaning up");
            
            // Cleanup UI listeners
            if (_climbCodeSearchButton != null)
                _climbCodeSearchButton.onClick.RemoveAllListeners();
            
            if (_beachToggle != null) _beachToggle.onValueChanged.RemoveAllListeners();
            if (_tropicsToggle != null) _tropicsToggle.onValueChanged.RemoveAllListeners();
            if (_alpineMesaToggle != null) _alpineMesaToggle.onValueChanged.RemoveAllListeners();
            if (_calderaToggle != null) _calderaToggle.onValueChanged.RemoveAllListeners();
            
            // Cleanup components
            if (_searchManager != null)
                _searchManager.OnClimbFound -= OnClimbFoundFromSearch;
            
            if (_serverLoader != null)
            {
                _serverLoader.OnServerClimbsLoaded -= OnServerClimbsLoaded;
                _serverLoader.OnLoadError -= OnServerLoadError;
                _serverLoader.OnPaginationUpdated -= OnPaginationUpdated;
            }
            
            _itemManager?.ClearAllItems();
        }
    }
}