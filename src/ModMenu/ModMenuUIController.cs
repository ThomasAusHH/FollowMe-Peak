using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using FollowMePeak.Services;
using FollowMePeak.Models;
using FollowMePeak.Managers;
using System;

namespace FollowMePeak.ModMenu
{
    public class ModMenuUIController
    {
        // Tab Buttons
        private Button _climbsButton;
        private Button _cloudSyncButton;
        
        // Pages
        private GameObject _climbsPage;
        private GameObject _cloudSyncPage;
        
        // Climbs Page Elements
        private Toggle _beachToggle;
        private Toggle _tropicsToggle;
        private Toggle _alpineMesaToggle;
        private Toggle _calderaToggle;
        private TMP_InputField _climbCodeInput;
        private Button _climbCodeSearchButton;
        private Transform _climbsScrollViewport;
        private GameObject _climbItemTemplate;
        
        // Icons are now handled directly in the prefab, no sprite references needed
        
        // List of instantiated climb items
        private List<GameObject> _climbListItems = new List<GameObject>();
        
        // Current filter
        private string _currentLevelFilter = "All";
        
        // Cloud Sync Page Elements
        private Toggle _cloudSyncToggle;
        private TMP_InputField _cloudSyncNameInput;
        private TextMeshProUGUI _cloudSyncActualName;
        private Button _cloudSyncNameSaveButton;
        
        // Services
        private ServerConfigService _serverConfig;
        private VPSApiService _apiService;
        private ClimbUploadService _uploadService;
        private ClimbDownloadService _downloadService;
        private ClimbDataService _climbDataService;
        private ClimbVisualizationManager _visualizationManager;
        
        // Button visual states
        private Color _activeTabColor = new Color(1f, 1f, 1f, 1f);
        private Color _inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        private Color _activeTextColor = new Color(0.196f, 0.196f, 0.196f, 1f);
        private Color _inactiveTextColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        public void Initialize(GameObject menuRoot)
        {
            Debug.Log("[ModMenuUI] Initializing UI Controller");
            
            // Get services from ModMenuManager
            _serverConfig = ModMenuManager.ServerConfig;
            _apiService = ModMenuManager.ApiService;
            _uploadService = ModMenuManager.UploadService;
            _downloadService = ModMenuManager.DownloadService;
            _climbDataService = ModMenuManager.ClimbDataService;
            _visualizationManager = ModMenuManager.VisualizationManager;
            
            // Find all UI elements
            FindUIElements(menuRoot);
            
            // Setup button listeners
            SetupButtonListeners();
            
            // Setup toggles
            SetupToggles();
            
            // Initialize with Climbs tab active
            ShowClimbsPage();
            
            // Load saved settings
            LoadSettings();
            
            Debug.Log("[ModMenuUI] UI Controller initialized successfully");
        }
        
        private void FindUIElements(GameObject root)
        {
            Debug.Log("[ModMenuUI] Finding UI elements");
            
            // Find Tab Buttons
            Transform tabMenu = root.transform.Find("MyModMenuPanel/TabMenu");
            if (tabMenu != null)
            {
                Transform climbsBtn = tabMenu.Find("ClimbsButton");
                if (climbsBtn != null) _climbsButton = climbsBtn.GetComponent<Button>();
                
                Transform cloudSyncBtn = tabMenu.Find("CloudSyncButton");
                if (cloudSyncBtn != null) _cloudSyncButton = cloudSyncBtn.GetComponent<Button>();
            }
            
            // Find Pages
            Transform pages = root.transform.Find("MyModMenuPanel/Pages");
            if (pages != null)
            {
                Transform climbsPage = pages.Find("ClimbsPage");
                if (climbsPage != null) _climbsPage = climbsPage.gameObject;
                
                Transform cloudSyncPage = pages.Find("CloudSyncPage");
                if (cloudSyncPage != null) _cloudSyncPage = cloudSyncPage.gameObject;
            }
            
            // Find Climbs Page Elements
            if (_climbsPage != null)
            {
                Transform toggleGroup = _climbsPage.transform.Find("ToggleGroupContainer");
                if (toggleGroup != null)
                {
                    Transform beach = toggleGroup.Find("BeachToggle");
                    if (beach != null) _beachToggle = beach.GetComponent<Toggle>();
                    
                    Transform tropics = toggleGroup.Find("TropicsToggle");
                    if (tropics != null) _tropicsToggle = tropics.GetComponent<Toggle>();
                    
                    Transform alpine = toggleGroup.Find("AlpineMesaToggle");
                    if (alpine != null) _alpineMesaToggle = alpine.GetComponent<Toggle>();
                    
                    Transform caldera = toggleGroup.Find("CalderaToggle");
                    if (caldera != null) _calderaToggle = caldera.GetComponent<Toggle>();
                }
                
                Transform climbCodeSearch = _climbsPage.transform.Find("ClimbCodeSearch");
                if (climbCodeSearch != null)
                {
                    Transform codeEnter = climbCodeSearch.Find("ClimbCodeEnter");
                    if (codeEnter != null) _climbCodeInput = codeEnter.GetComponent<TMP_InputField>();
                    
                    Transform searchBtn = climbCodeSearch.Find("ClimbCodeSearchButton");
                    if (searchBtn != null) _climbCodeSearchButton = searchBtn.GetComponent<Button>();
                }
                
                Transform scrollViewport = _climbsPage.transform.Find("ClimbsScrollView/ClimbsScrollViewport");
                if (scrollViewport != null) 
                {
                    _climbsScrollViewport = scrollViewport;
                    
                    // Find the template item - it should be a direct child of viewport
                    Transform template = scrollViewport.Find("ClimbScrollContent");
                    if (template != null)
                    {
                        _climbItemTemplate = template.gameObject;
                        // Keep template but hide it
                        _climbItemTemplate.SetActive(false);
                        Debug.Log("[ModMenuUI] Found climb item template");
                        
                        // Ensure the viewport has proper layout
                        var layoutGroup = scrollViewport.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                        if (layoutGroup == null)
                        {
                            layoutGroup = scrollViewport.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                            layoutGroup.spacing = 10f; // 10 pixels spacing between items
                            layoutGroup.childControlHeight = false;
                            layoutGroup.childControlWidth = true;
                            layoutGroup.childForceExpandHeight = false;
                            layoutGroup.childForceExpandWidth = true;
                            layoutGroup.childAlignment = TextAnchor.UpperCenter;
                            layoutGroup.padding = new RectOffset(10, 10, 10, 10);
                            Debug.Log("[ModMenuUI] Added VerticalLayoutGroup to viewport");
                        }
                        
                        // Ensure the viewport has ContentSizeFitter for proper scrolling
                        var sizeFitter = scrollViewport.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                        if (sizeFitter == null)
                        {
                            sizeFitter = scrollViewport.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                            sizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                            Debug.Log("[ModMenuUI] Added ContentSizeFitter to viewport");
                        }
                    }
                }
                
                // Icons are now handled directly in the ClimbScrollContent prefab
                // No need to extract them from toggles anymore
            }
            
            // Find Cloud Sync Page Elements
            if (_cloudSyncPage != null)
            {
                Transform activeArea = _cloudSyncPage.transform.Find("CloudSyncActiveArea");
                if (activeArea != null)
                {
                    Transform toggle = activeArea.Find("CloudSyncToggle");
                    if (toggle != null) _cloudSyncToggle = toggle.GetComponent<Toggle>();
                }
                
                Transform nameArea = _cloudSyncPage.transform.Find("CloudSyncNameArea");
                if (nameArea != null)
                {
                    Transform actualName = nameArea.Find("CloudSyncName/CloudSyncActualName");
                    if (actualName != null) _cloudSyncActualName = actualName.GetComponent<TextMeshProUGUI>();
                    
                    Transform nameEnter = nameArea.Find("CloudSyncName/CloudSyncNameEnter");
                    if (nameEnter != null) _cloudSyncNameInput = nameEnter.GetComponent<TMP_InputField>();
                    
                    Transform saveBtn = nameArea.Find("CloudSyncNameSaveButton");
                    if (saveBtn != null) _cloudSyncNameSaveButton = saveBtn.GetComponent<Button>();
                }
            }
            
            Debug.Log($"[ModMenuUI] Found elements - ClimbsButton: {_climbsButton != null}, CloudSyncButton: {_cloudSyncButton != null}");
            Debug.Log($"[ModMenuUI] Pages - Climbs: {_climbsPage != null}, CloudSync: {_cloudSyncPage != null}");
        }
        
        private void SetupButtonListeners()
        {
            Debug.Log("[ModMenuUI] Setting up button listeners");
            
            // Tab buttons
            if (_climbsButton != null)
            {
                _climbsButton.onClick.RemoveAllListeners();
                _climbsButton.onClick.AddListener(() => {
                    Debug.Log("[ModMenuUI] Climbs tab clicked");
                    ShowClimbsPage();
                });
            }
            
            if (_cloudSyncButton != null)
            {
                _cloudSyncButton.onClick.RemoveAllListeners();
                _cloudSyncButton.onClick.AddListener(() => {
                    Debug.Log("[ModMenuUI] Cloud Sync tab clicked");
                    ShowCloudSyncPage();
                });
            }
            
            // Climb code search button
            if (_climbCodeSearchButton != null)
            {
                _climbCodeSearchButton.onClick.RemoveAllListeners();
                _climbCodeSearchButton.onClick.AddListener(() => {
                    SearchClimbByCode();
                });
            }
            
            // Cloud sync save button
            if (_cloudSyncNameSaveButton != null)
            {
                _cloudSyncNameSaveButton.onClick.RemoveAllListeners();
                _cloudSyncNameSaveButton.onClick.AddListener(() => {
                    SaveCloudSyncName();
                });
            }
        }
        
        private void SetupToggles()
        {
            Debug.Log("[ModMenuUI] Setting up toggles");
            
            // Cloud sync toggle
            if (_cloudSyncToggle != null)
            {
                _cloudSyncToggle.onValueChanged.RemoveAllListeners();
                _cloudSyncToggle.onValueChanged.AddListener((bool value) => {
                    Debug.Log($"[ModMenuUI] Cloud Sync toggled: {value}");
                    if (_serverConfig != null)
                    {
                        _serverConfig.Config.EnableCloudSync = value;
                        _serverConfig.SaveConfig();
                    }
                });
            }
            
            // Setup toggle group for area filters
            ToggleGroup toggleGroup = null;
            
            // Try to find or create a toggle group
            if (_beachToggle != null)
            {
                Transform container = _beachToggle.transform.parent;
                if (container != null)
                {
                    toggleGroup = container.GetComponent<ToggleGroup>();
                    if (toggleGroup == null)
                    {
                        toggleGroup = container.gameObject.AddComponent<ToggleGroup>();
                        toggleGroup.allowSwitchOff = true; // Allow all toggles to be off
                        Debug.Log("[ModMenuUI] Added ToggleGroup to filter container");
                    }
                }
            }
            
            // Ascent level toggles
            if (_beachToggle != null)
            {
                _beachToggle.group = toggleGroup;
                _beachToggle.onValueChanged.RemoveAllListeners();
                _beachToggle.onValueChanged.AddListener((bool value) => {
                    if (value) FilterClimbsByLevel("Beach");
                    else if (!IsAnyFilterActive()) FilterClimbsByLevel("All");
                });
            }
            
            if (_tropicsToggle != null)
            {
                _tropicsToggle.group = toggleGroup;
                _tropicsToggle.onValueChanged.RemoveAllListeners();
                _tropicsToggle.onValueChanged.AddListener((bool value) => {
                    if (value) FilterClimbsByLevel("Tropics");
                    else if (!IsAnyFilterActive()) FilterClimbsByLevel("All");
                });
            }
            
            if (_alpineMesaToggle != null)
            {
                _alpineMesaToggle.group = toggleGroup;
                _alpineMesaToggle.onValueChanged.RemoveAllListeners();
                _alpineMesaToggle.onValueChanged.AddListener((bool value) => {
                    if (value) FilterClimbsByLevel("AlpineMesa");
                    else if (!IsAnyFilterActive()) FilterClimbsByLevel("All");
                });
            }
            
            if (_calderaToggle != null)
            {
                _calderaToggle.group = toggleGroup;
                _calderaToggle.onValueChanged.RemoveAllListeners();
                _calderaToggle.onValueChanged.AddListener((bool value) => {
                    if (value) FilterClimbsByLevel("Caldera");
                    else if (!IsAnyFilterActive()) FilterClimbsByLevel("All");
                });
            }
        }
        
        private void ShowClimbsPage()
        {
            Debug.Log("[ModMenuUI] Showing Climbs page");
            
            // Show/Hide pages
            if (_climbsPage != null) _climbsPage.SetActive(true);
            if (_cloudSyncPage != null) _cloudSyncPage.SetActive(false);
            
            // Update tab button visuals
            UpdateTabVisuals(_climbsButton, true);
            UpdateTabVisuals(_cloudSyncButton, false);
            
            // Load climbs data
            RefreshClimbsList();
        }
        
        private void ShowCloudSyncPage()
        {
            Debug.Log("[ModMenuUI] Showing Cloud Sync page");
            
            // Show/Hide pages
            if (_climbsPage != null) _climbsPage.SetActive(false);
            if (_cloudSyncPage != null) _cloudSyncPage.SetActive(true);
            
            // Update tab button visuals
            UpdateTabVisuals(_climbsButton, false);
            UpdateTabVisuals(_cloudSyncButton, true);
        }
        
        private void UpdateTabVisuals(Button button, bool isActive)
        {
            if (button == null) return;
            
            // Update button background color
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isActive ? _activeTabColor : _inactiveTabColor;
            }
            
            // Update text color
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.color = isActive ? _activeTextColor : _inactiveTextColor;
            }
        }
        
        private void LoadSettings()
        {
            Debug.Log("[ModMenuUI] Loading settings");
            
            if (_serverConfig != null && _serverConfig.Config != null)
            {
                // Set cloud sync toggle
                if (_cloudSyncToggle != null)
                {
                    _cloudSyncToggle.isOn = _serverConfig.Config.EnableCloudSync;
                }
                
                // Set player name
                if (_cloudSyncActualName != null)
                {
                    _cloudSyncActualName.text = string.IsNullOrEmpty(_serverConfig.Config.PlayerName) 
                        ? "Not set" 
                        : _serverConfig.Config.PlayerName;
                }
            }
        }
        
        private void SaveCloudSyncName()
        {
            if (_cloudSyncNameInput != null && _serverConfig != null)
            {
                string newName = _cloudSyncNameInput.text;
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    Debug.Log($"[ModMenuUI] Saving player name: {newName}");
                    _serverConfig.Config.PlayerName = newName;
                    _serverConfig.SaveConfig();
                    
                    // Update display
                    if (_cloudSyncActualName != null)
                    {
                        _cloudSyncActualName.text = newName;
                    }
                    
                    // Clear input field
                    _cloudSyncNameInput.text = "";
                }
            }
        }
        
        // Icons are now directly in the ClimbScrollContent prefab, no extraction needed
        
        private void FilterClimbsByLevel(string levelName)
        {
            Debug.Log($"[ModMenuUI] Filtering climbs by level: {levelName}");
            _currentLevelFilter = levelName;
            RefreshClimbsList();
        }
        
        private void RefreshClimbsList()
        {
            Debug.Log("[ModMenuUI] Refreshing climbs list");
            
            if (_climbItemTemplate == null || _climbsScrollViewport == null)
            {
                Debug.LogError("[ModMenuUI] Cannot refresh climbs - template or viewport is null");
                return;
            }
            
            // Clear existing items
            ClearClimbsList();
            
            // Get climbs from ClimbDataService
            if (_climbDataService == null)
            {
                Debug.LogWarning("[ModMenuUI] ClimbDataService is null");
                return;
            }
            
            var allClimbs = _climbDataService.GetAllClimbs();
            Debug.Log($"[ModMenuUI] Found {allClimbs.Count} total climbs");
            
            // Debug output for first few climbs
            int debugCount = 0;
            foreach (var debugClimb in allClimbs)
            {
                Debug.Log($"[ModMenuUI] Climb {debugCount}: AscentLevel={debugClimb.AscentLevel}, BiomeName='{debugClimb.BiomeName}', Duration={debugClimb.DurationInSeconds}s");
                debugCount++;
                if (debugCount >= 3) break; // Only log first 3 for debugging
            }
            
            // Filter climbs based on current filter
            List<ClimbData> filteredClimbs = new List<ClimbData>();
            
            if (_currentLevelFilter == "All")
            {
                filteredClimbs = allClimbs;
            }
            else
            {
                foreach (var climb in allClimbs)
                {
                    // Check if climb matches current filter based on BiomeName
                    if (DoesBiomeMatchFilter(climb.BiomeName, _currentLevelFilter))
                    {
                        filteredClimbs.Add(climb);
                    }
                }
            }
            
            Debug.Log($"[ModMenuUI] Showing {filteredClimbs.Count} filtered climbs");
            
            // Create UI items for each climb
            foreach (var climb in filteredClimbs)
            {
                CreateClimbListItem(climb);
            }
        }
        
        private bool DoesBiomeMatchFilter(string biomeName, string filter)
        {
            if (string.IsNullOrEmpty(biomeName)) return false;
            
            // Normalize biome name for comparison
            string normalizedBiome = biomeName.Replace(" ", "").ToLower();
            string normalizedFilter = filter.ToLower();
            
            switch (normalizedFilter)
            {
                case "beach":
                    return normalizedBiome.Contains("beach");
                case "tropics":
                    return normalizedBiome.Contains("tropic") || normalizedBiome.Contains("jungle");
                case "alpinemesa":
                    return normalizedBiome.Contains("alpine") || normalizedBiome.Contains("mesa") || normalizedBiome.Contains("mountain");
                case "caldera":
                    return normalizedBiome.Contains("caldera") || normalizedBiome.Contains("volcano") || normalizedBiome.Contains("summit");
                default:
                    return false;
            }
        }
        
        private void ClearClimbsList()
        {
            foreach (var item in _climbListItems)
            {
                if (item != null)
                {
                    UnityEngine.Object.Destroy(item);
                }
            }
            _climbListItems.Clear();
        }
        
        private void CreateClimbListItem(ClimbData climb)
        {
            if (_climbItemTemplate == null) return;
            
            // Instantiate new item from template as child of viewport
            GameObject newItem = UnityEngine.Object.Instantiate(_climbItemTemplate);
            newItem.transform.SetParent(_climbsScrollViewport, false);
            newItem.SetActive(true);
            newItem.name = $"Climb_{climb.Id}";
            
            // Debug: Log the hierarchy of the instantiated item
            Debug.Log($"[ModMenuUI] === Hierarchy of instantiated climb item '{newItem.name}' ===");
            LogTransformHierarchy(newItem.transform, 0);
            Debug.Log($"[ModMenuUI] === End of hierarchy ===");
            
            // Ensure the item has proper RectTransform settings
            RectTransform rectTransform = newItem.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Set anchors to stretch horizontally but not vertically
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.pivot = new Vector2(0.5f, 1);
                // Keep original height but stretch width
                float originalHeight = rectTransform.sizeDelta.y;
                rectTransform.sizeDelta = new Vector2(0, originalHeight);
            }
            
            // Set biome icon by showing/hiding the correct icon in BiomeIconArea
            Transform biomeIconArea = newItem.transform.Find("BiomeIconArea");
            
            if (biomeIconArea != null)
            {
                Debug.Log($"[ModMenuUI] Setting icon for BiomeName: '{climb.BiomeName}'");
                
                // Find all icon children
                Transform beachIcon = biomeIconArea.Find("BeachIcon");
                Transform tropicsIcon = biomeIconArea.Find("TropicsIcon");
                Transform alpineMesaIcon = biomeIconArea.Find("AlpineMesaIcon");
                Transform calderaIcon = biomeIconArea.Find("CalderaIcon");
                
                Debug.Log($"[ModMenuUI] Found icons - Beach: {beachIcon != null}, Tropics: {tropicsIcon != null}, Alpine: {alpineMesaIcon != null}, Caldera: {calderaIcon != null}");
                
                // Hide ALL icons first - IMPORTANT!
                if (beachIcon != null) beachIcon.gameObject.SetActive(false);
                if (tropicsIcon != null) tropicsIcon.gameObject.SetActive(false);
                if (alpineMesaIcon != null) alpineMesaIcon.gameObject.SetActive(false);
                if (calderaIcon != null) calderaIcon.gameObject.SetActive(false);
                
                // Show the correct icon based on BiomeName
                string normalizedBiome = climb.BiomeName?.ToLower() ?? "";
                Debug.Log($"[ModMenuUI] Normalized biome name: '{normalizedBiome}'");
                
                bool iconSet = false;
                
                if (normalizedBiome == "beach" || normalizedBiome.Contains("beach"))
                {
                    if (beachIcon != null) 
                    {
                        beachIcon.gameObject.SetActive(true);
                        Debug.Log($"[ModMenuUI] ✓ Activated Beach icon for biome: '{climb.BiomeName}'");
                        iconSet = true;
                    }
                }
                else if (normalizedBiome == "tropics" || normalizedBiome.Contains("tropic"))
                {
                    if (tropicsIcon != null) 
                    {
                        tropicsIcon.gameObject.SetActive(true);
                        Debug.Log($"[ModMenuUI] ✓ Activated Tropics icon for biome: '{climb.BiomeName}'");
                        iconSet = true;
                    }
                }
                else if (normalizedBiome == "alpine" || normalizedBiome == "mesa" || 
                         normalizedBiome.Contains("alpine") || normalizedBiome.Contains("mesa"))
                {
                    if (alpineMesaIcon != null) 
                    {
                        alpineMesaIcon.gameObject.SetActive(true);
                        Debug.Log($"[ModMenuUI] ✓ Activated AlpineMesa icon for biome: '{climb.BiomeName}'");
                        iconSet = true;
                    }
                }
                else if (normalizedBiome == "caldera" || normalizedBiome.Contains("caldera"))
                {
                    if (calderaIcon != null) 
                    {
                        calderaIcon.gameObject.SetActive(true);
                        Debug.Log($"[ModMenuUI] ✓ Activated Caldera icon for biome: '{climb.BiomeName}'");
                        iconSet = true;
                    }
                }
                
                // If no icon was set, use beach as fallback
                if (!iconSet)
                {
                    if (beachIcon != null) 
                    {
                        beachIcon.gameObject.SetActive(true);
                        Debug.LogWarning($"[ModMenuUI] ⚠ Unknown biome '{climb.BiomeName}' (normalized: '{normalizedBiome}'), using Beach icon as fallback");
                    }
                    else
                    {
                        Debug.LogError("[ModMenuUI] No Beach icon available for fallback!");
                    }
                }
            }
            else
            {
                // Fallback: Try to find AreaIconActive (from earlier log structure)
                Transform areaIconActive = newItem.transform.Find("AreaIconActive");
                if (areaIconActive != null)
                {
                    Debug.LogWarning("[ModMenuUI] BiomeIconArea not found, but found AreaIconActive - your prefab might have a different structure");
                }
                else
                {
                    Debug.LogError("[ModMenuUI] Neither BiomeIconArea nor AreaIconActive found in climb item!");
                }
            }
            
            // Set climb date
            Transform dateText = newItem.transform.Find("ClimbDate");
            if (dateText != null)
            {
                TextMeshProUGUI tmp = dateText.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = climb.CreationTime.ToString("dd.MM.yyyy HH:mm");
                }
            }
            
            // Set duration
            Transform durationText = newItem.transform.Find("ClimbDuration");
            if (durationText != null)
            {
                TextMeshProUGUI tmp = durationText.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    // Format duration as MM:SS
                    int minutes = Mathf.FloorToInt(climb.DurationInSeconds / 60f);
                    int seconds = Mathf.FloorToInt(climb.DurationInSeconds % 60f);
                    tmp.text = $"{minutes:00}:{seconds:00}";
                }
            }
            
            // Set ascent level
            Transform ascentText = newItem.transform.Find("ClimbAscent");
            if (ascentText != null)
            {
                TextMeshProUGUI tmp = ascentText.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    // Display the ascent level as a simple number (this is just a level/stage number, not related to biome)
                    tmp.text = climb.AscentLevel.ToString();
                    Debug.Log($"[ModMenuUI] Climb {climb.Id} - AscentLevel (stage): {climb.AscentLevel}, BiomeName (area): {climb.BiomeName}");
                }
            }
            
            // Set up visibility toggle
            Transform visToggle = newItem.transform.Find("ClimbVisibilityToggle");
            if (visToggle != null)
            {
                Toggle toggle = visToggle.GetComponent<Toggle>();
                if (toggle != null)
                {
                    // Set initial state - for now always off until visualization is implemented
                    toggle.isOn = false;
                    
                    // Add listener for toggle changes
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.onValueChanged.AddListener((bool value) => {
                        OnClimbVisibilityToggled(climb, value);
                    });
                }
            }
            
            // Add to list
            _climbListItems.Add(newItem);
        }
        
        // Icons are now handled by showing/hiding GameObjects, not by sprite swapping
        
        // Removed GetAscentLevelDisplay - AscentLevel is just a number, not related to biomes
        
        private void OnClimbVisibilityToggled(ClimbData climb, bool isVisible)
        {
            Debug.Log($"[ModMenuUI] Climb {climb.Id} visibility toggled to: {isVisible}");
            
            // Update the visualization using the ClimbVisualizationManager
            if (_visualizationManager != null)
            {
                _visualizationManager.SetClimbVisibility(climb.Id, isVisible);
                Debug.Log($"[ModMenuUI] Updated visualization for climb {climb.Id}");
            }
            else
            {
                Debug.LogWarning("[ModMenuUI] VisualizationManager not available");
            }
        }
        
        private void SearchClimbByCode()
        {
            if (_climbCodeInput == null) return;
            
            string searchCode = _climbCodeInput.text?.Trim();
            if (string.IsNullOrEmpty(searchCode))
            {
                Debug.Log("[ModMenuUI] No climb code entered");
                return;
            }
            
            Debug.Log($"[ModMenuUI] Searching for climb code: {searchCode}");
            
            // First search in local climbs
            var existingClimb = _climbDataService.GetAllClimbs()
                .FirstOrDefault(p => p.ShareCode != null && 
                    p.ShareCode.IndexOf(searchCode, StringComparison.OrdinalIgnoreCase) >= 0);
            
            if (existingClimb != null)
            {
                // Make the found climb visible
                if (_visualizationManager != null)
                {
                    _visualizationManager.SetClimbVisibility(existingClimb.Id, true);
                }
                Debug.Log($"[ModMenuUI] Climb found locally: {existingClimb.GetDisplayName()}");
                
                // Clear the input field
                _climbCodeInput.text = "";
                
                // Refresh the list to show the search result
                RefreshClimbsList();
            }
            else
            {
                Debug.Log($"[ModMenuUI] Climb code '{searchCode}' not found locally. Searching on server...");
                SearchClimbCodeOnServer(searchCode);
            }
        }
        
        private void SearchClimbCodeOnServer(string peakCode)
        {
            if (_apiService == null)
            {
                Debug.LogError("[ModMenuUI] API Service not available for server search");
                return;
            }
            
            _apiService.SearchClimbByPeakCode(peakCode, (climbData, error) =>
            {
                if (climbData != null)
                {
                    // Climb found on server, add it to local data and make it visible
                    climbData.IsFromCloud = true;
                    _climbDataService.AddClimb(climbData);
                    
                    if (_visualizationManager != null)
                    {
                        _visualizationManager.UpdateVisuals();
                        _visualizationManager.SetClimbVisibility(climbData.Id, true);
                    }
                    
                    Debug.Log($"[ModMenuUI] Climb code '{peakCode}' found on server: {climbData.GetDisplayName()}");
                    
                    // Clear the input field
                    if (_climbCodeInput != null)
                    {
                        _climbCodeInput.text = "";
                    }
                    
                    // Refresh the list to show the new climb
                    RefreshClimbsList();
                }
                else
                {
                    Debug.Log($"[ModMenuUI] Climb code '{peakCode}' not found. {error ?? "Climb does not exist."}");
                }
            });
        }
        
        public void Cleanup()
        {
            Debug.Log("[ModMenuUI] Cleaning up UI Controller");
            
            // Remove all listeners
            if (_climbsButton != null) _climbsButton.onClick.RemoveAllListeners();
            if (_cloudSyncButton != null) _cloudSyncButton.onClick.RemoveAllListeners();
            if (_climbCodeSearchButton != null) _climbCodeSearchButton.onClick.RemoveAllListeners();
            if (_cloudSyncNameSaveButton != null) _cloudSyncNameSaveButton.onClick.RemoveAllListeners();
            if (_cloudSyncToggle != null) _cloudSyncToggle.onValueChanged.RemoveAllListeners();
            if (_beachToggle != null) _beachToggle.onValueChanged.RemoveAllListeners();
            if (_tropicsToggle != null) _tropicsToggle.onValueChanged.RemoveAllListeners();
            if (_alpineMesaToggle != null) _alpineMesaToggle.onValueChanged.RemoveAllListeners();
            if (_calderaToggle != null) _calderaToggle.onValueChanged.RemoveAllListeners();
            
            // Clear climb list items
            ClearClimbsList();
        }
        
        private bool IsAnyFilterActive()
        {
            return (_beachToggle != null && _beachToggle.isOn) ||
                   (_tropicsToggle != null && _tropicsToggle.isOn) ||
                   (_alpineMesaToggle != null && _alpineMesaToggle.isOn) ||
                   (_calderaToggle != null && _calderaToggle.isOn);
        }
        
        private void LogTransformHierarchy(Transform transform, int depth)
        {
            string indent = new string(' ', depth * 2);
            
            // Log components on this transform
            Component[] components = transform.GetComponents<Component>();
            string componentList = string.Join(", ", components.Select(c => c?.GetType()?.Name ?? "null"));
            
            Debug.Log($"[ModMenuUI] {indent}└─ {transform.name} (Active: {transform.gameObject.activeSelf}) [Components: {componentList}]");
            
            // Recursively log children
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                LogTransformHierarchy(child, depth + 1);
            }
        }
        
        private Transform FindChildWithName(Transform parent, string nameContains)
        {
            // Check immediate children first
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.ToLower().Contains(nameContains.ToLower()))
                {
                    return child;
                }
            }
            
            // Then search recursively
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildWithName(parent.GetChild(i), nameContains);
                if (result != null) return result;
            }
            
            return null;
        }
        
        private string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }
        
        private Transform FindChildRecursive(Transform parent, string name)
        {
            // Check direct children first
            Transform result = parent.Find(name);
            if (result != null) return result;
            
            // Then check all descendants
            foreach (Transform child in parent)
            {
                result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            
            return null;
        }
    }
}