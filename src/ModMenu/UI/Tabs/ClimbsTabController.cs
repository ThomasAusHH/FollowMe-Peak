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
        private TMP_InputField _ascentInput;
        private Button _climbCodeSearchButton;
        private Toggle _durationSortingToggle;
        private Transform _scrollViewport;
        private ScrollRect _scrollRect;
        private GameObject _infoBox;
        private GameObject _notOnIslandNotification;
        private GameObject _noClimbAvailableNotification;
        
        // Components
        private ClimbListItemManager _itemManager;
        private ClimbFilterManager _filterManager;
        private ClimbSearchManager _searchManager;
        private ClimbServerLoader _serverLoader;
        
        // State
        private HashSet<string> _visibleClimbIds = new HashSet<string>();
        private bool _showOnlyLocalClimbs = false;
        private int? _ascentFilter = null;
        private bool _sortByDurationAscending = true;
        private bool _isDurationSortingActive = false;  // Track if duration sorting is active
        
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
                _ascentInput = UIElementFinder.FindComponent<TMP_InputField>(climbCodeSearch, "AscentEnter");
                _climbCodeSearchButton = UIElementFinder.FindComponent<Button>(climbCodeSearch, "ClimbCodeSearchButton");
                _durationSortingToggle = UIElementFinder.FindComponent<Toggle>(climbCodeSearch, "DurationSortingToggle");
            }
            
            // Find ScrollView elements
            FindScrollViewElements();
            
            // Find notification elements
            FindNotificationElements();
        }
        
        private void FindNotificationElements()
        {
            Debug.Log("[ClimbsTab] Searching for notification elements");
            
            // Find all GameObjects in the entire page
            Transform[] allTransforms = _climbsPage.GetComponentsInChildren<Transform>(true);
            
            foreach (Transform t in allTransforms)
            {
                if (t.gameObject.name == "NotOnIslandNotification")
                {
                    _notOnIslandNotification = t.gameObject;
                    Debug.Log($"[ClimbsTab] Found NotOnIslandNotification at: {GetPath(t)}");
                }
                else if (t.gameObject.name == "NoClimbAvailableNotification")
                {
                    _noClimbAvailableNotification = t.gameObject;
                    Debug.Log($"[ClimbsTab] Found NoClimbAvailableNotification at: {GetPath(t)}");
                }
                else if (t.gameObject.name == "InfoBox")
                {
                    _infoBox = t.gameObject;
                    Debug.Log($"[ClimbsTab] Found InfoBox at: {GetPath(t)}");
                }
            }
            
            // Check if notifications share a common parent (might be the InfoBox)
            if (_notOnIslandNotification != null && _noClimbAvailableNotification != null && _infoBox == null)
            {
                Transform parent1 = _notOnIslandNotification.transform.parent;
                Transform parent2 = _noClimbAvailableNotification.transform.parent;
                
                if (parent1 == parent2 && parent1 != null)
                {
                    _infoBox = parent1.gameObject;
                    Debug.Log($"[ClimbsTab] InfoBox (common parent) found at: {GetPath(parent1)}");
                }
            }
            
            Debug.Log($"[ClimbsTab] Notification search complete - NotOnIsland: {_notOnIslandNotification != null}, NoClimbs: {_noClimbAvailableNotification != null}, InfoBox: {_infoBox != null}");
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
        
        // Helper method to get full path of a transform
        private string GetPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
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
            SetupSearchFilters();
            SetupAscentFilter();
            SetupDurationSorting();
            SetupInfoBox();
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
                    
                    // Convert filter to biome name for API call
                    string biomeFilterName = GetBiomeFilterName(filter);
                    
                    // Reload from server with all filters
                    string climbCode = _climbCodeInput?.text?.Trim() ?? "";
                    _serverLoader?.ReloadWithAllFilters(biomeFilterName, _ascentFilter, climbCode);
                    // Note: RefreshClimbsList will be called by OnServerClimbsLoaded event
                }
                else if (!IsAnyFilterActive())
                {
                    Debug.Log("[ClimbsTab] All filters deactivated, showing all climbs");
                    _filterManager.SetFilter(ClimbFilterManager.BiomeFilter.All);
                    
                    // Reload from server without biome filter but keep other filters
                    string climbCode = _climbCodeInput?.text?.Trim() ?? "";
                    _serverLoader?.ReloadWithAllFilters("", _ascentFilter, climbCode);
                    // Note: RefreshClimbsList will be called by OnServerClimbsLoaded event
                }
            });
        }
        
        private string GetBiomeFilterName(ClimbFilterManager.BiomeFilter filter)
        {
            switch (filter)
            {
                case ClimbFilterManager.BiomeFilter.Beach:
                    return "Beach";
                case ClimbFilterManager.BiomeFilter.Tropics:
                    return "Tropics";
                case ClimbFilterManager.BiomeFilter.AlpineMesa:
                    return "Alpine"; // Or "Mesa" - API should handle both
                case ClimbFilterManager.BiomeFilter.Caldera:
                    return "Caldera";
                default:
                    return "";
            }
        }
        
        private void SetupSearchFilters()
        {
            if (_climbCodeSearchButton != null)
            {
                _climbCodeSearchButton.onClick.RemoveAllListeners();
                _climbCodeSearchButton.onClick.AddListener(() => {
                    string climbCode = _climbCodeInput?.text?.Trim() ?? "";
                    
                    // Check if BOTH climb code AND ascent filter are empty
                    if (string.IsNullOrEmpty(climbCode) && _ascentFilter == null)
                    {
                        Debug.Log("[ClimbsTab] Both climb code and ascent empty - refreshing with biome filter and default sort");
                        
                        // Keep biome filter, clear ascent, reset duration sort to default
                        RefreshWithDefaults();
                    }
                    else if (!string.IsNullOrEmpty(climbCode))
                    {
                        Debug.Log($"[ClimbsTab] Searching with peak_code filter: {climbCode}");
                        
                        // Clear all filters for peak code search to ensure climb is found
                        ClearAllFiltersForSearch();
                        
                        // Reload from server with ONLY peak code filter
                        _serverLoader?.ReloadWithAllFilters("", null, climbCode);
                    }
                    else if (_ascentFilter != null)
                    {
                        Debug.Log($"[ClimbsTab] Searching with ascent filter: {_ascentFilter}");
                        
                        // Keep current filters and search with ascent
                        string currentBiome = GetCurrentBiomeFilter();
                        _serverLoader?.ReloadWithAllFilters(currentBiome, _ascentFilter, "");
                    }
                });
            }
            
            // Also setup climb code input to search on enter
            if (_climbCodeInput != null)
            {
                _climbCodeInput.onEndEdit.RemoveAllListeners();
                _climbCodeInput.onEndEdit.AddListener((string value) => {
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        _climbCodeSearchButton?.onClick?.Invoke();
                    }
                });
            }
        }
        
        private void ClearAllFiltersForSearch()
        {
            Debug.Log("[ClimbsTab] Clearing all filters for peak code search");
            
            // Clear biome filter toggles
            if (_beachToggle != null) _beachToggle.isOn = false;
            if (_tropicsToggle != null) _tropicsToggle.isOn = false;
            if (_alpineMesaToggle != null) _alpineMesaToggle.isOn = false;
            if (_calderaToggle != null) _calderaToggle.isOn = false;
            
            // Clear ascent filter
            _ascentFilter = null;
            if (_ascentInput != null) 
            {
                _ascentInput.text = "";
            }
            
            // Reset filter manager
            _filterManager?.SetFilter(ClimbFilterManager.BiomeFilter.All);
            
            // Note: Duration sorting stays active if user wants it
            // Duration sorting is independent and can stay
        }
        
        private void RefreshWithDefaults()
        {
            Debug.Log("[ClimbsTab] Refreshing with defaults - keeping biome filter, resetting sort");
            
            // Keep current biome filter
            string currentBiome = GetCurrentBiomeFilter();
            
            // Clear ascent filter
            _ascentFilter = null;
            if (_ascentInput != null)
            {
                _ascentInput.text = "";
            }
            
            // Clear climb code input
            if (_climbCodeInput != null)
            {
                _climbCodeInput.text = "";
            }
            
            // Reset duration sorting to default (OFF state = not sorted by duration)
            _isDurationSortingActive = false;
            if (_durationSortingToggle != null)
            {
                _durationSortingToggle.isOn = false;
            }
            
            // Reset to default sorting (created_at desc) on server
            _serverLoader?.ResetToDefaultSorting();
            
            // This will trigger the reload with default sorting
            // Note: No need to call ReloadWithAllFilters since ResetToDefaultSorting handles it
        }
        
        private void SetupAscentFilter()
        {
            if (_ascentInput != null)
            {
                _ascentInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                _ascentInput.onEndEdit.RemoveAllListeners();
                _ascentInput.onEndEdit.AddListener((string value) => {
                    if (string.IsNullOrEmpty(value))
                    {
                        _ascentFilter = null;
                        Debug.Log("[ClimbsTab] Ascent filter cleared");
                    }
                    else if (int.TryParse(value, out int ascent))
                    {
                        _ascentFilter = ascent;
                        Debug.Log($"[ClimbsTab] Ascent filter set to: {ascent}");
                    }
                    
                    // Reload with ALL current filters
                    string currentBiome = GetCurrentBiomeFilter();
                    string climbCode = _climbCodeInput?.text?.Trim() ?? "";
                    _serverLoader?.ReloadWithAllFilters(currentBiome, _ascentFilter, climbCode);
                    // Note: RefreshClimbsList will be called by OnServerClimbsLoaded event
                });
            }
        }
        
        private void SetupDurationSorting()
        {
            if (_durationSortingToggle != null)
            {
                _durationSortingToggle.onValueChanged.RemoveAllListeners();
                _durationSortingToggle.onValueChanged.AddListener((bool value) => {
                    // Toggle OFF (value=false) = Ascending (Up arrow visible)
                    // Toggle ON (value=true) = Descending (Down arrow visible)
                    _sortByDurationAscending = !value;
                    
                    // Mark duration sorting as active
                    _isDurationSortingActive = true;
                    
                    // Apply duration sorting on server
                    _serverLoader?.ApplyDurationSorting(_sortByDurationAscending);
                    
                    Debug.Log($"[ClimbsTab] Duration sorting activated: {(_sortByDurationAscending ? "Ascending" : "Descending")}");
                    // Note: RefreshClimbsList will be called by OnServerClimbsLoaded event
                });
                
                // Set initial state (OFF = not sorted by duration)
                _durationSortingToggle.isOn = false;
                _isDurationSortingActive = false;
            }
        }
        
        private void SetupInfoBox()
        {
            Debug.Log("[ClimbsTab] SetupInfoBox - Hiding all notifications initially");
            
            if (_notOnIslandNotification != null)
            {
                _notOnIslandNotification.SetActive(false);
                Debug.Log("[ClimbsTab] NotOnIslandNotification hidden");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] NotOnIslandNotification is null!");
            }
            
            if (_noClimbAvailableNotification != null)
            {
                _noClimbAvailableNotification.SetActive(false);
                Debug.Log("[ClimbsTab] NoClimbAvailableNotification hidden");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] NoClimbAvailableNotification is null!");
            }
            
            if (_infoBox != null)
            {
                _infoBox.SetActive(false);
                Debug.Log("[ClimbsTab] InfoBox hidden");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] InfoBox is null!");
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
            
            bool isInLevel = IsPlayerInValidLevel();
            
            _itemManager.ClearAllItems();
            
            // Get climbs based on mode
            var climbsToDisplay = GetClimbsToDisplay();
            
            // Note: Ascent filtering is now done on the server side
            
            // Server climbs are already sorted
            // Only sort local climbs if duration sorting is active
            if (_isDurationSortingActive)
            {
                var localClimbs = climbsToDisplay.Where(c => !c.IsFromCloud).ToList();
                var serverClimbs = climbsToDisplay.Where(c => c.IsFromCloud).ToList();
                
                if (localClimbs.Any())
                {
                    if (_sortByDurationAscending)
                        localClimbs = localClimbs.OrderBy(c => c.DurationInSeconds).ToList();
                    else
                        localClimbs = localClimbs.OrderByDescending(c => c.DurationInSeconds).ToList();
                    
                    // Combine with server climbs (server first, then local)
                    climbsToDisplay = serverClimbs.Concat(localClimbs).ToList();
                }
            }
            
            // Update info box notifications
            UpdateInfoBoxNotifications(isInLevel, climbsToDisplay.Count);
            
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
            
            // If not in a valid level, return empty list
            if (!IsPlayerInValidLevel())
            {
                Debug.Log("[ClimbsTab] Not in valid level, returning empty climb list");
                return displayClimbs;
            }
            
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
                // Server climbs are already filtered by the API call, so add them directly
                displayClimbs.AddRange(_serverLoader.CurrentPageClimbs);
                
                // Add local climbs that are not from cloud (apply filter to local climbs only)
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
        
        private string GetCurrentBiomeFilter()
        {
            if (_beachToggle?.isOn ?? false) return "Beach";
            if (_tropicsToggle?.isOn ?? false) return "Tropics";
            if (_alpineMesaToggle?.isOn ?? false) return "Alpine";
            if (_calderaToggle?.isOn ?? false) return "Caldera";
            return "";
        }
        
        private bool IsPlayerInValidLevel()
        {
            string levelId = _climbDataService?.CurrentLevelID;
            return !string.IsNullOrEmpty(levelId) && 
                   !levelId.Contains("_unknown") && 
                   !levelId.Contains("placeholder");
        }
        
        private void UpdateInfoBoxNotifications(bool isInLevel, int climbCount)
        {
            bool showNotOnIsland = !isInLevel;
            bool showNoClimbs = isInLevel && climbCount == 0;
            
            Debug.Log($"[ClimbsTab] UpdateInfoBox - InLevel: {isInLevel}, ClimbCount: {climbCount}, ShowNotOnIsland: {showNotOnIsland}, ShowNoClimbs: {showNoClimbs}");
            
            if (_notOnIslandNotification != null)
            {
                _notOnIslandNotification.SetActive(showNotOnIsland);
                Debug.Log($"[ClimbsTab] NotOnIslandNotification set to: {showNotOnIsland}");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] Cannot update NotOnIslandNotification - reference is null!");
            }
            
            if (_noClimbAvailableNotification != null)
            {
                _noClimbAvailableNotification.SetActive(showNoClimbs);
                Debug.Log($"[ClimbsTab] NoClimbAvailableNotification set to: {showNoClimbs}");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] Cannot update NoClimbAvailableNotification - reference is null!");
            }
            
            // InfoBox nur anzeigen wenn eine Notification aktiv ist
            if (_infoBox != null)
            {
                _infoBox.SetActive(showNotOnIsland || showNoClimbs);
                Debug.Log($"[ClimbsTab] InfoBox set to: {showNotOnIsland || showNoClimbs}");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] Cannot update InfoBox - reference is null!");
            }
        }
        
        // Called when the climbs page becomes visible
        public void OnShow()
        {
            Debug.Log("[ClimbsTab] OnShow - Checking for server data");
            
            // Check if player is in valid level first
            bool isInLevel = IsPlayerInValidLevel();
            
            if (isInLevel)
            {
                // Load server data if in valid level
                string currentBiome = GetCurrentBiomeFilter();
                string climbCode = _climbCodeInput?.text?.Trim() ?? "";
                _serverLoader?.CheckAndLoadInitialData(currentBiome, _ascentFilter, climbCode);
            }
            else
            {
                // Clear server climbs if not in level
                _serverLoader?.Reset();
            }
            
            // Always refresh to update notifications
            RefreshClimbsList();
        }
        
        // Server loader event handlers
        private void OnServerClimbsLoaded(List<ClimbData> serverClimbs)
        {
            Debug.Log($"[ClimbsTab] Server climbs loaded: {serverClimbs.Count} items");
            
            // Clear climb code input if we just did a peak code search
            string currentPeakCode = _climbCodeInput?.text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(currentPeakCode) && serverClimbs.Count > 0)
            {
                // Clear the input after successful peak code search
                if (_climbCodeInput != null)
                {
                    _climbCodeInput.text = "";
                    Debug.Log($"[ClimbsTab] Cleared climb code input after successful search");
                }
            }
            
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
            if (_ascentInput != null) _ascentInput.onEndEdit.RemoveAllListeners();
            if (_climbCodeInput != null) _climbCodeInput.onEndEdit.RemoveAllListeners();
            if (_durationSortingToggle != null) _durationSortingToggle.onValueChanged.RemoveAllListeners();
            
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