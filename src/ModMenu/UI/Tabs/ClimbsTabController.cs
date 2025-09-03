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
        private Toggle _peakToggle;
        private TMP_InputField _climbCodeInput;
        private TMP_InputField _ascentInput;
        private Button _climbCodeSearchButton;
        private Button _climbVisibilityAllOffButton;
        private Toggle _durationSortingToggle;
        private Transform _scrollViewport;
        private ScrollRect _scrollRect;
        private Transform _contentParent; // Das Content-Objekt der ScrollView
        private GameObject _climbContentPrefab; // Das neue Prefab
        private GameObject _infoBox;
        private GameObject _notOnIslandNotification;
        private GameObject _noClimbAvailableNotification;
        private GameObject _notOnIslandNotificationBackgroundImage;
        private GameObject _noClimbAvailableNotificationBackgroundImage;
        
        // Update Message UI
        private GameObject _updateMessageBox;
        private TextMeshProUGUI _updateMessageText;
        private Button _updateMessageCloseButton;
        private Image _updateMessageBackground;
        private UpdateMessage _currentUpdateMessage;
        private bool _updateMessageDismissed = false;
        
        // Components (ClimbListItemManager removed - now using prefab system)
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
                _peakToggle = UIElementFinder.FindComponent<Toggle>(toggleGroup, "PeakToggle");
            }
            
            // Find Search Elements
            Transform climbCodeSearch = _climbsPage.transform.Find("ClimbCodeSearch");
            if (climbCodeSearch != null)
            {
                _climbCodeInput = UIElementFinder.FindComponent<TMP_InputField>(climbCodeSearch, "ClimbCodeEnter");
                _ascentInput = UIElementFinder.FindComponent<TMP_InputField>(climbCodeSearch, "AscentEnter");
                _climbCodeSearchButton = UIElementFinder.FindComponent<Button>(climbCodeSearch, "ClimbCodeSearchButton");
                _durationSortingToggle = UIElementFinder.FindComponent<Toggle>(climbCodeSearch, "DurationSortingToggle");
                _climbVisibilityAllOffButton = UIElementFinder.FindComponent<Button>(climbCodeSearch, "ClimbVisibilityAllOffButton");
            }
            
            // Find ScrollView elements
            FindScrollViewElements();
            
            // Find notification elements
            FindNotificationElements();
            
            // Find Update Message Box (located under ClimbsScrollView, same level as InfoBox)
            Transform scrollView = _climbsPage.transform.Find("ClimbsScrollView");
            if (scrollView != null)
            {
                _updateMessageBox = scrollView.Find("UpdateMessageBox")?.gameObject;
                if (_updateMessageBox != null)
                {
                    // Find Message text element
                    Transform messageTransform = _updateMessageBox.transform.Find("Message");
                    if (messageTransform != null)
                    {
                        _updateMessageText = messageTransform.GetComponent<TextMeshProUGUI>();
                    }
                    
                    // Find Close button
                    Transform closeTransform = _updateMessageBox.transform.Find("Close");
                    if (closeTransform != null)
                    {
                        _updateMessageCloseButton = closeTransform.GetComponent<Button>();
                        if (_updateMessageCloseButton != null)
                        {
                            _updateMessageCloseButton.onClick.RemoveAllListeners();
                            _updateMessageCloseButton.onClick.AddListener(DismissUpdateMessage);
                        }
                    }
                    
                    // Find Background for color changes
                    Transform backgroundTransform = _updateMessageBox.transform.Find("Background");
                    if (backgroundTransform != null)
                    {
                        _updateMessageBackground = backgroundTransform.GetComponent<Image>();
                    }
                    
                    // Initially hidden
                    _updateMessageBox.SetActive(false);
                    
                    Debug.Log($"[ClimbsTab] UpdateMessageBox found at ClimbsScrollView - Message: {_updateMessageText != null}, Close: {_updateMessageCloseButton != null}, Background: {_updateMessageBackground != null}");
                }
                else
                {
                    Debug.LogWarning("[ClimbsTab] UpdateMessageBox not found under ClimbsScrollView");
                }
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] ClimbsScrollView not found - cannot search for UpdateMessageBox");
            }
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
                else if (t.gameObject.name == "NotOnIslandNotificationBackgroundImage")
                {
                    _notOnIslandNotificationBackgroundImage = t.gameObject;
                    Debug.Log($"[ClimbsTab] Found NotOnIslandNotificationBackgroundImage at: {GetPath(t)}");
                }
                else if (t.gameObject.name == "NoClimbAvailableNotificationBackgroundImage")
                {
                    _noClimbAvailableNotificationBackgroundImage = t.gameObject;
                    Debug.Log($"[ClimbsTab] Found NoClimbAvailableNotificationBackgroundImage at: {GetPath(t)}");
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
            
            Debug.Log($"[ClimbsTab] Notification search complete - NotOnIsland: {_notOnIslandNotification != null}, NoClimbs: {_noClimbAvailableNotification != null}, NotOnIslandBG: {_notOnIslandNotificationBackgroundImage != null}, NoClimbsBG: {_noClimbAvailableNotificationBackgroundImage != null}, InfoBox: {_infoBox != null}");
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
            
            // Find ClimbScrollContent object (where items will be added)
            _contentParent = _scrollViewport.Find("ClimbScrollContent");
            if (_contentParent == null)
            {
                Debug.LogError("[ClimbsTab] ClimbScrollContent object not found in viewport!");
                return;
            }
            
            // Load the climb content prefab from libs
            LoadClimbContentPrefab();
            
            SetupScrollRect(scrollView);
        }
        
        private void LoadClimbContentPrefab()
        {
            try
            {
                // Load the prefab from the libs folder
                var assetBundleService = AssetBundleService.Instance;
                if (assetBundleService != null && assetBundleService.IsLoaded)
                {
                    _climbContentPrefab = assetBundleService.GetPrefab("climbscrollcontent");
                    if (_climbContentPrefab != null)
                    {
                        Debug.Log("[ClimbsTab] ClimbScrollContent prefab loaded successfully");
                    }
                    else
                    {
                        Debug.LogError("[ClimbsTab] ClimbScrollContent prefab not found in AssetBundle");
                    }
                }
                else
                {
                    Debug.LogError("[ClimbsTab] AssetBundleService not available");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ClimbsTab] Error loading ClimbScrollContent prefab: {ex.Message}");
            }
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
            {
                Debug.LogError("[ClimbsTab] ScrollRect component missing on ScrollView!");
                return;
            }
            
            // Ensure ScrollRect points to our ClimbScrollContent object
            if (_contentParent != null)
            {
                _scrollRect.content = _contentParent.GetComponent<RectTransform>();
                Debug.Log("[ClimbsTab] ScrollRect content set to ClimbScrollContent object");
            }
            
            // Set scroll sensitivity for faster scrolling
            _scrollRect.scrollSensitivity = 50f; // Default is 1, higher = faster
            Debug.Log($"[ClimbsTab] ScrollRect sensitivity set to: {_scrollRect.scrollSensitivity}");
            
            // Setup layout on the ClimbScrollContent object (where items will be added)
            if (_contentParent != null)
            {
                var layoutGroup = _contentParent.GetComponent<VerticalLayoutGroup>();
                if (layoutGroup == null)
                {
                    layoutGroup = _contentParent.gameObject.AddComponent<VerticalLayoutGroup>();
                    layoutGroup.spacing = 10f;
                    layoutGroup.childControlHeight = false;
                    layoutGroup.childControlWidth = true;
                    layoutGroup.childForceExpandHeight = false;
                    layoutGroup.childForceExpandWidth = true;
                    layoutGroup.padding = new RectOffset(10, 10, 10, 10);
                    Debug.Log("[ClimbsTab] VerticalLayoutGroup added to Content");
                }
                
                var sizeFitter = _contentParent.GetComponent<ContentSizeFitter>();
                if (sizeFitter == null)
                {
                    sizeFitter = _contentParent.gameObject.AddComponent<ContentSizeFitter>();
                    sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    Debug.Log("[ClimbsTab] ContentSizeFitter added to ClimbScrollContent");
                }
                
                // Make ClimbScrollContent background transparent (both Image and RawImage)
                var contentImage = _contentParent.GetComponent<UnityEngine.UI.Image>();
                if (contentImage != null)
                {
                    var color = contentImage.color;
                    color.a = 0f; // Set alpha to 0 (transparent)
                    contentImage.color = color;
                    Debug.Log($"[ClimbsTab] ClimbScrollContent Image background set to transparent (alpha: {color.a})");
                }
                
                var contentRawImage = _contentParent.GetComponent<UnityEngine.UI.RawImage>();
                if (contentRawImage != null)
                {
                    var color = contentRawImage.color;
                    color.a = 0f; // Set alpha to 0 (transparent)
                    contentRawImage.color = color;
                    Debug.Log($"[ClimbsTab] ClimbScrollContent RawImage background set to transparent (alpha: {color.a})");
                }
                
                if (contentImage == null && contentRawImage == null)
                {
                    Debug.LogWarning("[ClimbsTab] ClimbScrollContent has no Image or RawImage component - cannot set transparency");
                }
            }
            
            // Make ScrollView background transparent
            var scrollViewImage = scrollView.GetComponent<UnityEngine.UI.Image>();
            if (scrollViewImage != null)
            {
                var color = scrollViewImage.color;
                color.a = 0f; // Set alpha to 0 (transparent)
                scrollViewImage.color = color;
                Debug.Log($"[ClimbsTab] ScrollView background set to transparent (alpha: {color.a})");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] ScrollView Image component not found - cannot set transparency");
            }
        }
        
        private void SetupUI()
        {
            SetupToggles();
            SetupSearchFilters();
            SetupAscentFilter();
            SetupDurationSorting();
            SetupVisibilityAllOffButton();
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
            SetupToggle(_peakToggle, toggleGroup, ClimbFilterManager.BiomeFilter.Peak);
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
                case ClimbFilterManager.BiomeFilter.Peak:
                    return "Peak";
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
        
        private void SetupVisibilityAllOffButton()
        {
            if (_climbVisibilityAllOffButton != null)
            {
                _climbVisibilityAllOffButton.onClick.RemoveAllListeners();
                _climbVisibilityAllOffButton.onClick.AddListener(() => {
                    Debug.Log("[ClimbsTab] Visibility All Off - Hiding all climbs");
                    
                    // Clear all visible climb IDs
                    var previousVisibleIds = new List<string>(_visibleClimbIds);
                    _visibleClimbIds.Clear();
                    
                    // Update visualization manager for all previously visible climbs
                    if (_visualizationManager != null)
                    {
                        foreach (var climbId in previousVisibleIds)
                        {
                            if (System.Guid.TryParse(climbId, out System.Guid guid))
                            {
                                _visualizationManager.SetClimbVisibility(guid, false);
                            }
                        }
                    }
                    
                    // Refresh the list to update all toggles
                    RefreshClimbsList();
                    
                    Debug.Log($"[ClimbsTab] All {previousVisibleIds.Count} climbs hidden");
                });
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
            
            if (_notOnIslandNotificationBackgroundImage != null)
            {
                _notOnIslandNotificationBackgroundImage.SetActive(false);
                Debug.Log("[ClimbsTab] NotOnIslandNotificationBackgroundImage hidden");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] NotOnIslandNotificationBackgroundImage is null!");
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
            
            if (_noClimbAvailableNotificationBackgroundImage != null)
            {
                _noClimbAvailableNotificationBackgroundImage.SetActive(false);
                Debug.Log("[ClimbsTab] NoClimbAvailableNotificationBackgroundImage hidden");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] NoClimbAvailableNotificationBackgroundImage is null!");
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
            Debug.Log("[ClimbsTab] Refreshing climbs list using prefab system");
            
            if (_climbDataService == null)
            {
                Debug.LogError("[ClimbsTab] Cannot refresh - ClimbDataService is null");
                return;
            }
            
            if (_contentParent == null)
            {
                Debug.LogError("[ClimbsTab] Cannot refresh - Content parent is null");
                return;
            }
            
            if (_climbContentPrefab == null)
            {
                Debug.LogError("[ClimbsTab] Cannot refresh - ClimbContent prefab is null");
                return;
            }
            
            bool isInLevel = IsPlayerInValidLevel();
            
            // Clear existing items from content parent
            ClearContentItems();
            
            // Get climbs based on mode
            var climbsToDisplay = GetClimbsToDisplay();
            
            // Apply sorting: Visible climbs always on top, then apply duration sorting within groups
            if (_isDurationSortingActive)
            {
                // Split into visible and non-visible groups
                var visibleClimbs = climbsToDisplay.Where(c => _visibleClimbIds.Contains(c.Id.ToString())).ToList();
                var nonVisibleClimbs = climbsToDisplay.Where(c => !_visibleClimbIds.Contains(c.Id.ToString())).ToList();
                
                // Sort ALL visible climbs together by duration (server + local mixed)
                if (visibleClimbs.Any())
                {
                    visibleClimbs = _sortByDurationAscending 
                        ? visibleClimbs.OrderBy(c => c.DurationInSeconds).ToList()
                        : visibleClimbs.OrderByDescending(c => c.DurationInSeconds).ToList();
                }
                
                // Sort ALL non-visible climbs together by duration (server + local mixed)
                if (nonVisibleClimbs.Any())
                {
                    nonVisibleClimbs = _sortByDurationAscending 
                        ? nonVisibleClimbs.OrderBy(c => c.DurationInSeconds).ToList()
                        : nonVisibleClimbs.OrderByDescending(c => c.DurationInSeconds).ToList();
                }
                
                // Combine: visible climbs first, then non-visible
                climbsToDisplay = visibleClimbs.Concat(nonVisibleClimbs).ToList();
            }
            else
            {
                // Without duration sorting: just sort by visibility
                var visibleClimbs = climbsToDisplay.Where(c => _visibleClimbIds.Contains(c.Id.ToString())).ToList();
                var nonVisibleClimbs = climbsToDisplay.Where(c => !_visibleClimbIds.Contains(c.Id.ToString())).ToList();
                climbsToDisplay = visibleClimbs.Concat(nonVisibleClimbs).ToList();
            }
            
            // Update info box notifications
            UpdateInfoBoxNotifications(isInLevel, climbsToDisplay.Count);
            
            Debug.Log($"[ClimbsTab] Creating {climbsToDisplay.Count} climb items using prefab system");
            
            // Create prefab instances for each climb
            foreach (var climb in climbsToDisplay)
            {
                CreateClimbPrefabItem(climb);
            }
            
            // Force layout rebuild
            if (_contentParent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentParent.GetComponent<RectTransform>());
                Debug.Log("[ClimbsTab] Layout rebuild completed");
            }
        }
        
        private void ClearContentItems()
        {
            if (_contentParent == null) return;
            
            // Destroy all child objects in the content parent
            for (int i = _contentParent.childCount - 1; i >= 0; i--)
            {
                var child = _contentParent.GetChild(i);
                if (child != null)
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }
            
            Debug.Log("[ClimbsTab] Cleared all content items");
        }
        
        private void CreateClimbPrefabItem(ClimbData climb)
        {
            try
            {
                // Instantiate the prefab
                GameObject climbItem = GameObject.Instantiate(_climbContentPrefab, _contentParent);
                
                if (climbItem == null)
                {
                    Debug.LogError("[ClimbsTab] Failed to instantiate climb prefab");
                    return;
                }
                
                // Set climb data on the prefab
                // Since ClimbListItemController doesn't exist, use basic data population
                PopulateBasicClimbData(climbItem, climb);
                
                Debug.Log($"[ClimbsTab] Created prefab item for climb {climb.Id}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ClimbsTab] Error creating prefab item for climb {climb.Id}: {ex.Message}");
            }
        }
        
        private void PopulateBasicClimbData(GameObject climbItem, ClimbData climb)
        {
            // Use the same logic as ClimbListItemManager to populate the prefab correctly
            SetBiomeIcon(climbItem, climb.BiomeName);
            SetClimbInfo(climbItem, climb);
            SetupVisibilityToggle(climbItem, climb);
            SetupCopyButton(climbItem, climb);
            SetOfflineIndicator(climbItem, climb);
            SetDeathClimbIndicator(climbItem, climb);
            
            Debug.Log($"[ClimbsTab] Populated prefab data for climb {climb.Id} - Biome: {climb.BiomeName}, ShareCode: {climb.ShareCode}, Duration: {climb.DurationInSeconds}s, IsOffline: {!climb.IsFromCloud}");
        }
        
        private void SetBiomeIcon(GameObject item, string biomeName)
        {
            Transform biomeIconArea = item.transform.Find("BiomeIconArea");
            if (biomeIconArea == null) return;
            
            // Hide all icons
            foreach (Transform child in biomeIconArea)
            {
                child.gameObject.SetActive(false);
            }
            
            // Show correct icon
            string normalizedBiome = biomeName?.ToLower() ?? "";
            Transform iconToShow = null;
            
            if (normalizedBiome.Contains("beach"))
                iconToShow = biomeIconArea.Find("BeachIcon");
            else if (normalizedBiome.Contains("tropic"))
                iconToShow = biomeIconArea.Find("TropicsIcon");
            else if (normalizedBiome.Contains("alpine") || normalizedBiome.Contains("mesa"))
                iconToShow = biomeIconArea.Find("AlpineMesaIcon");
            else if (normalizedBiome.Contains("caldera"))
                iconToShow = biomeIconArea.Find("CalderaIcon");
            else if (normalizedBiome.Contains("peak") || normalizedBiome.Contains("kiln"))
                iconToShow = biomeIconArea.Find("PeakIcon");
            else
                iconToShow = biomeIconArea.Find("BeachIcon"); // Default
            
            if (iconToShow != null)
                iconToShow.gameObject.SetActive(true);
        }
        
        private void SetClimbInfo(GameObject item, ClimbData climb)
        {
            // Set date
            var dateText = item.transform.Find("ClimbDate")?.GetComponent<TextMeshProUGUI>();
            if (dateText != null)
                dateText.text = climb.CreationTime.ToString("dd.MM.yyyy HH:mm");
            
            // Set duration - KORREKTE FORMATIERUNG: mm:ss nicht hh:mm:ss
            var durationText = item.transform.Find("ClimbDuration")?.GetComponent<TextMeshProUGUI>();
            if (durationText != null)
            {
                int minutes = Mathf.FloorToInt(climb.DurationInSeconds / 60f);
                int seconds = Mathf.FloorToInt(climb.DurationInSeconds % 60f);
                durationText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // Set ascent level
            var ascentText = item.transform.Find("ClimbAscent")?.GetComponent<TextMeshProUGUI>();
            if (ascentText != null)
                ascentText.text = climb.AscentLevel.ToString();
            
            // Set share code
            var shareCodeText = item.transform.Find("ClimbShareCode")?.GetComponent<TextMeshProUGUI>();
            if (shareCodeText != null)
            {
                // Ensure share code is generated
                if (string.IsNullOrEmpty(climb.ShareCode))
                    climb.GenerateShareCode();
                shareCodeText.text = climb.ShareCode ?? "";
            }
        }
        
        private void SetupVisibilityToggle(GameObject item, ClimbData climb)
        {
            Transform visToggle = item.transform.Find("ClimbVisibilityToggle");
            if (visToggle == null) return;
            
            Toggle toggle = visToggle.GetComponent<Toggle>();
            if (toggle != null)
            {
                bool isVisible = _visibleClimbIds.Contains(climb.Id.ToString());
                toggle.isOn = isVisible;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((bool value) => {
                    OnClimbVisibilityToggled(climb, value);
                });
            }
        }
        
        private void SetupCopyButton(GameObject item, ClimbData climb)
        {
            var copyButton = item.transform.Find("ClimbShareCodeCopyButton")?.GetComponent<Button>();
            var shareCodeText = item.transform.Find("ClimbShareCode");
            
            // Hide share code elements for death climbs
            if (climb.WasDeathClimb)
            {
                if (copyButton != null)
                    copyButton.gameObject.SetActive(false);
                if (shareCodeText != null)
                    shareCodeText.gameObject.SetActive(false);
                return;
            }
            
            // Show share code text for normal climbs
            if (shareCodeText != null)
            {
                shareCodeText.gameObject.SetActive(true);
                var textComponent = shareCodeText.GetComponent<TextMeshProUGUI>();
                if (textComponent != null && !string.IsNullOrEmpty(climb.ShareCode))
                {
                    textComponent.text = climb.ShareCode;
                }
            }
            
            if (copyButton != null)
            {
                // Ensure share code is generated
                if (string.IsNullOrEmpty(climb.ShareCode))
                    climb.GenerateShareCode();
                
                if (!string.IsNullOrEmpty(climb.ShareCode))
                {
                    copyButton.onClick.RemoveAllListeners();
                    copyButton.onClick.AddListener(() => {
                        GUIUtility.systemCopyBuffer = climb.ShareCode;
                        Debug.Log($"[ClimbsTab] Copied share code: {climb.ShareCode}");
                    });
                    copyButton.gameObject.SetActive(true);
                }
                else
                {
                    copyButton.gameObject.SetActive(false);
                }
            }
        }
        
        private void SetOfflineIndicator(GameObject item, ClimbData climb)
        {
            // Find the OfflineImage in the prefab item
            Transform offlineImage = item.transform.Find("OfflineImage");
            
            if (offlineImage != null)
            {
                // Show image only for local climbs (not from cloud)
                bool isOffline = !climb.IsFromCloud;
                offlineImage.gameObject.SetActive(isOffline);
                
                if (isOffline)
                {
                    Debug.Log($"[ClimbsTab] Showing offline indicator for local climb {climb.Id}");
                }
            }
            else
            {
                Debug.LogWarning($"[ClimbsTab] OfflineImage not found in climb item for climb {climb.Id}");
            }
        }
        
        private void SetDeathClimbIndicator(GameObject item, ClimbData climb)
        {
            // Find the DeathImage in the prefab item
            Transform deathImage = item.transform.Find("DeathImage");
            
            if (deathImage != null)
            {
                // Show image only for death climbs
                bool isDeathClimb = climb.WasDeathClimb;
                deathImage.gameObject.SetActive(isDeathClimb);
                
                if (isDeathClimb)
                {
                    Debug.Log($"[ClimbsTab] Showing death indicator for death climb {climb.Id}");
                }
            }
            else
            {
                Debug.LogWarning($"[ClimbsTab] DeathImage not found in climb item for climb {climb.Id}");
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
                // Apply filter to server climbs as well to handle race conditions
                // (e.g., when filter changes quickly and wrong data comes back from server)
                var filteredServerClimbs = _filterManager.FilterClimbs(_serverLoader.CurrentPageClimbs);
                if (_serverLoader.CurrentPageClimbs.Count != filteredServerClimbs.Count)
                {
                    Debug.Log($"[ClimbsTab] Filtered server climbs: {_serverLoader.CurrentPageClimbs.Count} -> {filteredServerClimbs.Count} (Filter: {_filterManager.CurrentFilter})");
                }
                displayClimbs.AddRange(filteredServerClimbs);
                
                // Add local climbs that are not from cloud (apply filter to local climbs)
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
            
            // Refresh list to re-sort with updated visibility
            RefreshClimbsList();
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
            
            if (_notOnIslandNotificationBackgroundImage != null)
            {
                _notOnIslandNotificationBackgroundImage.SetActive(showNotOnIsland);
                Debug.Log($"[ClimbsTab] NotOnIslandNotificationBackgroundImage set to: {showNotOnIsland}");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] Cannot update NotOnIslandNotificationBackgroundImage - reference is null!");
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
            
            if (_noClimbAvailableNotificationBackgroundImage != null)
            {
                _noClimbAvailableNotificationBackgroundImage.SetActive(showNoClimbs);
                Debug.Log($"[ClimbsTab] NoClimbAvailableNotificationBackgroundImage set to: {showNoClimbs}");
            }
            else
            {
                Debug.LogWarning("[ClimbsTab] Cannot update NoClimbAvailableNotificationBackgroundImage - reference is null!");
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
            Debug.Log("[ClimbsTab] OnShow - Force reloading data");
            
            // Check if player is in valid level first
            bool isInLevel = IsPlayerInValidLevel();
            
            if (isInLevel)
            {
                // Always force reload when switching to Climbs tab
                // This ensures:
                // - Fresh data when CloudSync is enabled
                // - Cleared data when CloudSync is disabled
                string currentBiome = GetCurrentBiomeFilter();
                string climbCode = _climbCodeInput?.text?.Trim() ?? "";
                
                Debug.Log("[ClimbsTab] Forcing reload with current filters");
                _serverLoader?.ForceReload(currentBiome, _ascentFilter, climbCode);
            }
            else
            {
                // Clear server climbs if not in level
                _serverLoader?.Reset();
                
                // Refresh to show "not on island" message
                RefreshClimbsList();
            }
            
            // Note: RefreshClimbsList will be called by OnServerClimbsLoaded event
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
        
        public void ShowUpdateMessage(UpdateMessage message)
        {
            Debug.Log($"[ClimbsTab] ShowUpdateMessage called - HasUpdate: {message?.HasUpdate}, Dismissed: {_updateMessageDismissed}");
            
            if (message == null || !message.HasUpdate || _updateMessageDismissed)
            {
                Debug.Log("[ClimbsTab] Not showing message (null, no update, or dismissed)");
                return;
            }
            
            _currentUpdateMessage = message;
            
            Debug.Log($"[ClimbsTab] UpdateMessageBox: {_updateMessageBox != null}, Text: {_updateMessageText != null}, Background: {_updateMessageBackground != null}");
            
            if (_updateMessageBox != null && _updateMessageText != null)
            {
                _updateMessageText.text = message.Message;
                Debug.Log($"[ClimbsTab] Set message text: {message.Message}");
                
                // Set background color based on type (use separate Background element)
                if (_updateMessageBackground != null)
                {
                    switch (message.Type)
                    {
                        case "warning":
                            _updateMessageBackground.color = new Color(1f, 0.8f, 0.3f, 0.9f); // Yellow
                            break;
                        case "critical":
                            _updateMessageBackground.color = new Color(1f, 0.3f, 0.3f, 0.9f); // Red
                            break;
                        default: // "info"
                            _updateMessageBackground.color = new Color(0.3f, 0.6f, 1f, 0.9f); // Blue
                            break;
                    }
                    Debug.Log($"[ClimbsTab] Set background color for type: {message.Type}");
                }
                
                _updateMessageBox.SetActive(true);
                Debug.Log($"[ClimbsTab] UpdateMessageBox activated - showing message");
            }
            else
            {
                Debug.LogError($"[ClimbsTab] Cannot show message - UpdateMessageBox or Text is null!");
            }
        }
        
        private void DismissUpdateMessage()
        {
            _updateMessageDismissed = true;
            if (_updateMessageBox != null)
            {
                _updateMessageBox.SetActive(false);
            }
            Debug.Log("[ClimbsTab] Update message dismissed by user");
        }
        
        public void ResetUpdateMessageState()
        {
            _updateMessageDismissed = false;
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
            if (_climbVisibilityAllOffButton != null) _climbVisibilityAllOffButton.onClick.RemoveAllListeners();
            
            // Cleanup components
            if (_searchManager != null)
                _searchManager.OnClimbFound -= OnClimbFoundFromSearch;
            
            if (_serverLoader != null)
            {
                _serverLoader.OnServerClimbsLoaded -= OnServerClimbsLoaded;
                _serverLoader.OnLoadError -= OnServerLoadError;
                _serverLoader.OnPaginationUpdated -= OnPaginationUpdated;
            }
            
            // Clear content items using prefab system
            ClearContentItems();
        }
    }
}