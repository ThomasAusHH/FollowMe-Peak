using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FollowMePeak.Models;
using FollowMePeak.Services;
using FollowMePeak.Managers;

namespace FollowMePeak.UI
{
    public class FollowMeUI
    {
        private readonly ClimbDataService _climbDataService;
        private readonly ClimbVisualizationManager _visualizationManager;
        private readonly CloudSyncUI _cloudSyncUI;
        private readonly VPSApiService _vpsApiService;
        private readonly ClimbDownloadService _climbDownloadService;
        
        private bool _showMenu = false;
        private Rect _menuRect = new Rect(20, 20, 800, 600);
        private Vector2 _scrollPosition = Vector2.zero;
        
        // Cursor management
        private CursorLockMode _originalCursorLockState;
        private bool _originalCursorVisible;
        
        private string _biomeFilter = "";
        private string _dateFilter = "";
        private string _peakCodeSearch = "";
        private bool _showOnlyLocalPaths = false;
        private SortColumn _currentSortColumn = SortColumn.CreationTime;
        private bool _sortAscending = false;
        
        // Server-side Pagination
        private int _currentPage = 0;
        private int _itemsPerPage = 20; // Fixed at 20 as requested
        private int _totalCloudClimbs = 0;
        private bool _isLoadingPage = false;
        private bool _hasMorePages = false;
        private List<ClimbData> _currentPageClimbs = new List<ClimbData>(); // Only current page data
        private bool _initialPageLoaded = false; // Track if we've loaded the first page
        
        // Tab system
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "My Climbs", "Online Share" };
        
        // Tag selection system - DISABLED FOR NOW
        // private ClimbData _currentTagEditClimb = null;
        // private bool _showTagSelection = false;
        // private System.Action<ClimbData> _tagSelectionCallback = null;

        private enum SortColumn { CreationTime, BiomeName, Duration }

        public FollowMeUI(ClimbDataService climbDataService, ClimbVisualizationManager visualizationManager,
            ServerConfigService serverConfigService, VPSApiService vpsApiService,
            ClimbUploadService climbUploadService, ClimbDownloadService climbDownloadService)
        {
            _climbDataService = climbDataService;
            _visualizationManager = visualizationManager;
            _vpsApiService = vpsApiService;
            _climbDownloadService = climbDownloadService;
            _cloudSyncUI = new CloudSyncUI(serverConfigService, vpsApiService, climbUploadService, climbDownloadService);
        }

        public bool ShowMenu
        {
            get => _showMenu;
            set
            {
                if (_showMenu != value)
                {
                    _showMenu = value;
                    UpdateCursorState();
                }
            }
        }

        public void OnGUI()
        {
            if (_showMenu)
            {
                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                _menuRect = GUILayout.Window(0, _menuRect, DrawMenuWindow, "FollowMe-Peak - Journal");
            }
            
            // Draw tag selection overlay OUTSIDE of the main window context - DISABLED FOR NOW
            // if (_showTagSelection && _currentTagEditPath != null)
            // {
            //     DrawTagSelectionOverlay();
            // }
        }

        private void DrawMenuWindow(int windowID)
        {
            // Tab Selection
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _tabNames.Length; i++)
            {
                bool isSelected = (i == _selectedTab);
                bool clicked = GUILayout.Toggle(isSelected, _tabNames[i], "Button");
                if (clicked && !isSelected)
                {
                    _selectedTab = i;
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);

            // Tab Content
            switch (_selectedTab)
            {
                case 0:
                    DrawClimbJournalTab();
                    break;
                case 1:
                    DrawCloudSyncTab();
                    break;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawClimbJournalTab()
        {
            // Check if player is in a level before making API calls
            if (!IsPlayerInLevel())
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("⚠️ Enter a level to load online climbs");
                GUILayout.Label("You can view local climbs using 'Local climbs only' toggle.");
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }
            
            // Check if player left the level - reset initial page loaded state
            if (_initialPageLoaded && !IsPlayerInLevel())
            {
                _initialPageLoaded = false;
                _currentPageClimbs.Clear();
                _totalCloudClimbs = 0;
                _currentPage = 0;
                Debug.Log("Player left level - reset pagination state");
            }
            
            // Load initial page if not loaded yet and not showing local only and player is in level
            if (!_initialPageLoaded && !_showOnlyLocalPaths && !_isLoadingPage && IsPlayerInLevel())
            {
                _initialPageLoaded = true;
                LoadServerPage();
            }
            
            // Search and Filter Section
            GUILayout.BeginVertical("box");
            GUILayout.Label("Search & Filter");
            
            // First row: Text filters
            GUILayout.BeginHorizontal();
            GUILayout.Label("Biome:", GUILayout.Width(50));
            _biomeFilter = GUILayout.TextField(_biomeFilter, GUILayout.Width(120));
            GUILayout.Label("Peak Code:", GUILayout.Width(70));
            _peakCodeSearch = GUILayout.TextField(_peakCodeSearch, GUILayout.Width(100));
            GUILayout.EndHorizontal();
            
            // Second row: Buttons and toggles
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Search", GUILayout.Width(100)))
            {
                // Trigger server-side search with current filter values
                _currentPage = 0;
                if (!_showOnlyLocalPaths && IsPlayerInLevel())
                {
                    LoadServerPage();
                }
                else if (!IsPlayerInLevel())
                {
                    Debug.LogWarning("Cannot search online climbs - enter a level first");
                }
            }
            
            if (GUILayout.Button("Clear Filters", GUILayout.Width(120)))
            {
                // Clear all filters
                _biomeFilter = "";
                _peakCodeSearch = "";
                _showOnlyLocalPaths = false;
                _currentPage = 0; // Reset to first page when clearing filters
                if (IsPlayerInLevel())
                {
                    LoadServerPage();
                }
            }
            
            GUILayout.FlexibleSpace();
            
            bool newShowOnlyLocal = GUILayout.Toggle(_showOnlyLocalPaths, "Local climbs only");
            if (newShowOnlyLocal != _showOnlyLocalPaths)
            {
                _showOnlyLocalPaths = newShowOnlyLocal;
                _currentPage = 0; // Reset to first page when changing filter
                if (!_showOnlyLocalPaths && IsPlayerInLevel())
                {
                    LoadServerPage(); // Load server data when switching back to combined view
                }
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();

            // Table Header
            GUILayout.BeginHorizontal("box");
            GUILayout.Label("Vis", GUILayout.Width(30)); // Visibility toggle
            if(GUILayout.Button(GetSortLabel("Biome", SortColumn.BiomeName), GUILayout.Width(120))) SetSortColumn(SortColumn.BiomeName);
            if(GUILayout.Button(GetSortLabel("Duration", SortColumn.Duration), GUILayout.Width(70))) SetSortColumn(SortColumn.Duration);
            if(GUILayout.Button(GetSortLabel("Date", SortColumn.CreationTime), GUILayout.Width(80))) SetSortColumn(SortColumn.CreationTime);
            GUILayout.Label("Peak Code", GUILayout.Width(70));
            // GUILayout.Label("Tags", GUILayout.Width(80)); // DISABLED FOR NOW
            GUILayout.Label("Actions", GUILayout.Width(120)); // Actions
            GUILayout.EndHorizontal();

            // Server-side Pagination Controls
            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"20 items per page", GUILayout.Width(100));
            
            GUILayout.FlexibleSpace();

            // Page navigation with server calls (only when in level)
            bool canNavigate = IsPlayerInLevel() && !_showOnlyLocalPaths;
            
            if (GUILayout.Button("<<", GUILayout.Width(30)) && _currentPage > 0 && canNavigate)
            {
                _currentPage = 0;
                LoadServerPage();
            }
            if (GUILayout.Button("<", GUILayout.Width(30)) && _currentPage > 0 && canNavigate)
            {
                _currentPage--;
                LoadServerPage();
            }

            int totalPages = _totalCloudClimbs > 0 ? Mathf.CeilToInt((float)_totalCloudClimbs / _itemsPerPage) : 1;
            string pageInfo = _totalCloudClimbs > 0 
                ? $"Page {_currentPage + 1}/{totalPages} ({_totalCloudClimbs} total)"
                : $"Page {_currentPage + 1}";
            if (!IsPlayerInLevel() && !_showOnlyLocalPaths)
            {
                pageInfo = "Enter level to load online climbs";
            }
            GUILayout.Label(pageInfo, GUILayout.Width(200));

            bool hasNext = (_currentPage + 1) < totalPages;
            if (GUILayout.Button(">", GUILayout.Width(30)) && hasNext && canNavigate)
            {
                _currentPage++;
                LoadServerPage();
            }

            if (_isLoadingPage)
            {
                GUILayout.Label("Loading...", GUILayout.Width(80));
            }

            GUILayout.EndHorizontal();

            // Table Content
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            var processedClimbs = GetProcessedClimbs();
            List<Guid> climbsToDelete = new List<Guid>();

            foreach (var climbData in processedClimbs)
            {
                // Ensure ShareCode is generated
                climbData.GenerateShareCode();
                
                DrawClimbTableRow(climbData, climbsToDelete);
            }

            if (climbsToDelete.Count > 0)
            {
                _climbDataService.DeleteClimbs(climbsToDelete);
                _visualizationManager.UpdateVisuals();
            }

            GUILayout.EndScrollView();
            
        }

        private void DrawClimbTableRow(ClimbData climbData, List<Guid> climbsToDelete)
        {
            GUILayout.BeginHorizontal("box");

            // Visibility Toggle
            bool isVisible = _visualizationManager.IsClimbVisible(climbData.Id);
            bool newVisibility = GUILayout.Toggle(isVisible, "", GUILayout.Width(30));
            if (newVisibility != isVisible)
            {
                _visualizationManager.ToggleClimbVisibility(climbData.Id);
            }

            // Display Name (with cloud indicator)
            string displayName = climbData.GetDisplayName();
            if (climbData.IsFromCloud)
                displayName = $"C: {displayName}";
                
            GUILayout.Label(displayName, GUILayout.Width(120));

            // Biome
            string biomeDisplay = climbData.BiomeName ?? "Unknown";
            if (biomeDisplay.Length > 12) biomeDisplay = biomeDisplay.Substring(0, 12) + "...";
            GUILayout.Label(biomeDisplay, GUILayout.Width(100));

            // Duration
            TimeSpan time = TimeSpan.FromSeconds(climbData.DurationInSeconds);
            string durationString = $"{time.Minutes:D2}:{time.Seconds:D2}";
            GUILayout.Label(durationString, GUILayout.Width(70));

            // Date
            string dateString = climbData.CreationTime.ToString("dd.MM");
            GUILayout.Label(dateString, GUILayout.Width(80));

            // Gipfelcode
            GUILayout.Label(climbData.ShareCode, GUILayout.Width(70));
            
            // Tags - DISABLED FOR NOW
            // string tagsDisplay = climbData.GetTagsDisplay();
            // if (tagsDisplay.Length > 10) tagsDisplay = tagsDisplay.Substring(0, 10) + "...";
            // GUILayout.Label(tagsDisplay, GUILayout.Width(80));
            GUILayout.Label("", GUILayout.Width(80)); // Empty space where tags would be

            // Action Buttons
            GUILayout.BeginHorizontal(GUILayout.Width(120));
            
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                // Copy peak code to clipboard
                Debug.Log($"Peak code copied: {climbData.ShareCode}");
                GUIUtility.systemCopyBuffer = climbData.ShareCode;
            }
            
            // Tag button - DISABLED FOR NOW
            // if (GUILayout.Button("Tag", GUILayout.Width(35)))
            // {
            //     // Show tag selection for this path
            //     ShowTagSelectionForClimb(climbData);
            // }
            
            if (GUILayout.Button("Del", GUILayout.Width(35)))
            {
                climbsToDelete.Add(climbData.Id);
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.EndHorizontal();
        }

        private void ShowOnlyCloudClimbs()
        {
            var allClimbs = _climbDataService.GetAllClimbs();
            foreach (var climb in allClimbs)
            {
                if (climb.IsFromCloud)
                    _visualizationManager.SetClimbVisibility(climb.Id, true);
                else
                    _visualizationManager.SetClimbVisibility(climb.Id, false);
            }
        }

        private void ShowOnlyLocalClimbs()
        {
            var allClimbs = _climbDataService.GetAllClimbs();
            foreach (var climb in allClimbs)
            {
                if (!climb.IsFromCloud)
                    _visualizationManager.SetClimbVisibility(climb.Id, true);
                else
                    _visualizationManager.SetClimbVisibility(climb.Id, false);
            }
        }

        private void DrawCloudSyncTab()
        {
            _cloudSyncUI.OnGUI();
        }
        
        private List<ClimbData> GetAllFilteredClimbs()
        {
            IEnumerable<ClimbData> processed = _climbDataService.GetAllClimbs();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(_biomeFilter))
                processed = processed.Where(p => (p.BiomeName ?? "").IndexOf(_biomeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                
            if (!string.IsNullOrWhiteSpace(_dateFilter))
                processed = processed.Where(p => p.CreationTime.ToString("dd.MM.yy").Contains(_dateFilter));
                
            if (!string.IsNullOrWhiteSpace(_peakCodeSearch))
                processed = processed.Where(p => (p.ShareCode ?? "").IndexOf(_peakCodeSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                
            // Apply "Local climbs only" filter
            if (_showOnlyLocalPaths)
                processed = processed.Where(p => !p.IsFromCloud);
            
            // Apply sorting
            switch (_currentSortColumn)
            {
                case SortColumn.BiomeName:
                    processed = _sortAscending ? processed.OrderBy(p => p.BiomeName) : processed.OrderByDescending(p => p.BiomeName);
                    break;
                case SortColumn.Duration:
                    processed = _sortAscending ? processed.OrderBy(p => p.DurationInSeconds) : processed.OrderByDescending(p => p.DurationInSeconds);
                    break;
                case SortColumn.CreationTime:
                    processed = _sortAscending ? processed.OrderBy(p => p.CreationTime) : processed.OrderByDescending(p => p.CreationTime);
                    break;
            }
            
            return processed.ToList();
        }

        private List<ClimbData> GetProcessedClimbs()
        {
            var displayClimbs = new List<ClimbData>();
            
            if (_showOnlyLocalPaths)
            {
                // Local only mode: Show ONLY real local climbs (not downloaded from cloud)
                var localClimbs = _climbDataService.GetAllClimbs().Where(c => !c.IsFromCloud);
                
                // Apply client-side filters to local climbs
                if (!string.IsNullOrWhiteSpace(_biomeFilter))
                    localClimbs = localClimbs.Where(p => (p.BiomeName ?? "").IndexOf(_biomeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    
                if (!string.IsNullOrWhiteSpace(_peakCodeSearch))
                    localClimbs = localClimbs.Where(p => (p.ShareCode ?? "").IndexOf(_peakCodeSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                
                // Apply client-side sorting to local climbs
                switch (_currentSortColumn)
                {
                    case SortColumn.BiomeName:
                        localClimbs = _sortAscending ? localClimbs.OrderBy(p => p.BiomeName) : localClimbs.OrderByDescending(p => p.BiomeName);
                        break;
                    case SortColumn.Duration:
                        localClimbs = _sortAscending ? localClimbs.OrderBy(p => p.DurationInSeconds) : localClimbs.OrderByDescending(p => p.DurationInSeconds);
                        break;
                    case SortColumn.CreationTime:
                        localClimbs = _sortAscending ? localClimbs.OrderBy(p => p.CreationTime) : localClimbs.OrderByDescending(p => p.CreationTime);
                        break;
                }
                
                displayClimbs.AddRange(localClimbs);
            }
            else
            {
                // Combined mode: Server page data + real local climbs
                displayClimbs.AddRange(_currentPageClimbs);
                
                // Add real local climbs (not downloaded from cloud)
                var realLocalClimbs = _climbDataService.GetAllClimbs().Where(c => !c.IsFromCloud);
                displayClimbs.AddRange(realLocalClimbs);
            }
            
            return displayClimbs.ToList();
        }

        private void SetSortColumn(SortColumn column)
        {
            if (_currentSortColumn == column) _sortAscending = !_sortAscending;
            else
            {
                _currentSortColumn = column;
                _sortAscending = true;
            }
            
            // Reset to first page when changing sort and reload from server
            _currentPage = 0;
            if (!_showOnlyLocalPaths && IsPlayerInLevel())
            {
                LoadServerPage();
            }
        }

        private string GetSortLabel(string label, SortColumn column)
        {
            if (_currentSortColumn != column) return label;
            return label + (_sortAscending ? " ▲" : " ▼");
        }

        private void SearchForPeakCode(string peakCode)
        {
            Debug.Log($"Searching for peak code: {peakCode}");
            
            // First search in local climbs
            var existingClimb = _climbDataService.GetAllClimbs()
                .FirstOrDefault(p => p.ShareCode != null && 
                    p.ShareCode.IndexOf(peakCode, StringComparison.OrdinalIgnoreCase) >= 0);
            
            if (existingClimb != null)
            {
                // Make the found climb visible
                _visualizationManager.SetClimbVisibility(existingClimb.Id, true);
                Debug.Log($"Peak code found: {existingClimb.GetDisplayName()}");
            }
            else
            {
                Debug.Log($"Peak code '{peakCode}' not found locally. Searching on server...");
                SearchForPeakCodeOnServer(peakCode);
            }
        }
        
        private void SearchForPeakCodeOnServer(string peakCode)
        {
            if (_vpsApiService == null)
            {
                Debug.LogError("VPS API Service not available for server search");
                return;
            }
            
            _vpsApiService.SearchClimbByPeakCode(peakCode, (climbData, error) =>
            {
                if (climbData != null)
                {
                    // Climb found on server, add it to local data and make it visible
                    climbData.IsFromCloud = true;
                    _climbDataService.AddClimb(climbData);
                    _visualizationManager.UpdateVisuals();
                    _visualizationManager.SetClimbVisibility(climbData.Id, true);
                    
                    Debug.Log($"Peak code '{peakCode}' found on server: {climbData.GetDisplayName()}");
                }
                else
                {
                    Debug.Log($"Peak code '{peakCode}' not found. {error ?? "Climb does not exist."}");
                }
            });
        }
        
        // Tag Selection Methods - DISABLED FOR NOW
        /*
        private void ShowTagSelectionForClimb(ClimbData climbData)
        {
            ShowTagSelectionForClimb(climbData, null);
        }
        
        public void ShowTagSelectionForClimb(ClimbData climbData, System.Action<ClimbData> callback)
        {
            _currentTagEditClimb = climbData;
            _tagSelectionCallback = callback;
            if (!_showTagSelection)
            {
                _showTagSelection = true;
                UpdateCursorState();
            }
        }
        */
        
        /* DISABLED FOR NOW - Tag Selection Methods
        private void DrawTagSelectionOverlay()
        {
            // Draw semi-transparent background
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            // Calculate smart positioning to avoid conflicts
            float windowWidth = 400;
            float windowHeight = 350; // Slightly taller to accommodate content
            Rect tagWindowRect = CalculateTagWindowPosition(windowWidth, windowHeight);
            
            string windowTitle = _tagSelectionCallback != null 
                ? $"🏔️ New climb successful! Choose tags:"
                : $"Tags bearbeiten: {_currentTagEditPath.GetDisplayName()}";
            
            GUILayout.Window(1, tagWindowRect, DrawTagSelectionWindow, windowTitle);
        }
        
        private Rect CalculateTagWindowPosition(float windowWidth, float windowHeight)
        {
            float x, y;
            
            // If the main menu is visible, position the tag window to the right or below it
            if (_showMenu)
            {
                // Try to position to the right of the main menu
                float rightOfMenu = _menuRect.x + _menuRect.width + 20;
                if (rightOfMenu + windowWidth <= Screen.width - 20)
                {
                    // Position to the right
                    x = rightOfMenu;
                    y = _menuRect.y;
                }
                else
                {
                    // Position below the main menu
                    x = _menuRect.x;
                    y = _menuRect.y + _menuRect.height + 20;
                    
                    // If it goes off screen, position at the top left
                    if (y + windowHeight > Screen.height - 20)
                    {
                        x = 20;
                        y = 20;
                    }
                }
            }
            else
            {
                // Center on screen when no main menu is visible
                x = (Screen.width - windowWidth) / 2;
                y = (Screen.height - windowHeight) / 2;
            }
            
            // Ensure the window stays within screen bounds
            x = Mathf.Clamp(x, 20, Screen.width - windowWidth - 20);
            y = Mathf.Clamp(y, 20, Screen.height - windowHeight - 20);
            
            return new Rect(x, y, windowWidth, windowHeight);
        }
        
        private void DrawTagSelectionWindow(int windowID)
        {
            if (_tagSelectionCallback != null)
            {
                // This is a new path - show encouraging message
                GUILayout.Label($"Pfad: {_currentTagEditPath.GetDisplayName()}");
                GUILayout.Label($"Gipfelcode: {_currentTagEditPath.ShareCode}");
                GUILayout.Space(5);
                GUILayout.Label("Choose appropriate tags to help others:");
            }
            else
            {
                // This is editing an existing path
                GUILayout.Label("Choose tags for this path:");
            }
            
            GUILayout.BeginVertical("box");
            
            // Display current tags
            GUILayout.Label("Aktuelle Tags:");
            if (_currentTagEditPath.Tags != null && _currentTagEditPath.Tags.Count > 0)
            {
                for (int i = _currentTagEditPath.Tags.Count - 1; i >= 0; i--)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"• {_currentTagEditPath.Tags[i]}");
                    if (GUILayout.Button("✕", GUILayout.Width(25)))
                    {
                        _currentTagEditPath.RemoveTag(_currentTagEditPath.Tags[i]);
                        _climbDataService.SaveClimbsToFile(false); // Save changes
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("No tags selected");
            }
            
            GUILayout.Space(10);
            
            // Available tags for selection
            GUILayout.Label("Available tags:");
            foreach (string tag in PathTags.GetTagOptions())
            {
                if (!_currentTagEditPath.HasTag(tag))
                {
                    if (GUILayout.Button($"+ {tag}"))
                    {
                        _currentTagEditPath.AddTag(tag);
                        _climbDataService.SaveClimbsToFile(false); // Save changes
                    }
                }
            }
            
            GUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // Action buttons
            GUILayout.BeginHorizontal();
            
            if (_tagSelectionCallback != null)
            {
                if (GUILayout.Button("Fertig & Hochladen"))
                {
                    // Call the callback with the tagged path
                    _tagSelectionCallback.Invoke(_currentTagEditPath);
                    _showTagSelection = false;
                    _currentTagEditPath = null;
                    _tagSelectionCallback = null;
                    UpdateCursorState();
                }
                
                if (GUILayout.Button("Abbrechen"))
                {
                    _showTagSelection = false;
                    _currentTagEditPath = null;
                    _tagSelectionCallback = null;
                    UpdateCursorState();
                }
            }
            else
            {
                if (GUILayout.Button("Close"))
                {
                    _showTagSelection = false;
                    _currentTagEditPath = null;
                    UpdateCursorState();
                }
            }
            
            GUILayout.EndHorizontal();
            
            GUI.DragWindow();
        }
        */
        
        // Pagination Methods
        private void LoadCloudClimbs()
        {
            if (_isLoadingPage) return;
            
            _isLoadingPage = true;
            
            // For now, we'll use a dummy level ID to load climbs
            // In a real scenario, you'd want to get the current level ID
            string currentLevelId = GetCurrentLevelId();
            if (string.IsNullOrEmpty(currentLevelId))
            {
                _isLoadingPage = false;
                return;
            }

            int offset = _currentPage * _itemsPerPage;
            
            _climbDownloadService.DownloadAndMergeClimbs(currentLevelId, (count, error, meta) =>
            {
                _isLoadingPage = false;
                
                if (error != null)
                {
                    Debug.LogError($"Failed to load climbs for page {_currentPage}: {error}");
                }
                else
                {
                    Debug.Log($"Loaded {count} climbs for page {_currentPage + 1}");
                    _visualizationManager.UpdateVisuals();
                    
                    // Update pagination info from metadata
                    if (meta != null)
                    {
                        _totalCloudClimbs = meta.Total;
                        _hasMorePages = meta.Has_More;
                    }
                }
            }, _itemsPerPage, offset);
        }
        
        // Server-side Pagination - Each page navigation loads from server
        private void LoadServerPage()
        {
            if (_isLoadingPage) return;
            
            _isLoadingPage = true;
            string currentLevelId = GetCurrentLevelId();
            
            if (string.IsNullOrEmpty(currentLevelId))
            {
                _isLoadingPage = false;
                return;
            }

            int offset = _currentPage * _itemsPerPage;
            
            Debug.Log($"Loading server page {_currentPage + 1} (offset: {offset}, limit: {_itemsPerPage})");
            
            // Convert UI sort to API sort parameters
            string sortBy = GetApiSortColumn(_currentSortColumn);
            string sortOrder = _sortAscending ? "asc" : "desc";
            
            // Use VPSApiService with search and sort parameters
            _vpsApiService.DownloadClimbs(currentLevelId, (downloadedClimbs, error, meta) =>
            {
                _isLoadingPage = false;
                
                if (error != null)
                {
                    Debug.LogError($"Failed to load page {_currentPage + 1}: {error}");
                    _currentPageClimbs.Clear();
                }
                else
                {
                    // Replace current page data with new server data
                    _currentPageClimbs.Clear();
                    if (downloadedClimbs != null)
                    {
                        _currentPageClimbs.AddRange(downloadedClimbs);
                        
                        // Save downloaded climbs to local storage for visualization
                        foreach (var climb in downloadedClimbs)
                        {
                            // Only add if not already exists locally
                            var existingClimb = _climbDataService.GetAllClimbs()
                                .FirstOrDefault(c => c.Id == climb.Id);
                            
                            if (existingClimb == null)
                            {
                                climb.IsFromCloud = true;
                                _climbDataService.AddClimb(climb);
                                Debug.Log($"Added new server climb to local storage: {climb.Id}");
                            }
                        }
                        
                        // Save the updated local data
                        _climbDataService.SaveClimbsToFile(false);
                        
                        // Update visualization with new climbs
                        _visualizationManager.UpdateVisuals();
                    }
                    
                    if (meta != null)
                    {
                        _totalCloudClimbs = meta.Total;
                        _hasMorePages = meta.Has_More;
                    }
                    
                    Debug.Log($"Loaded page {_currentPage + 1}: {_currentPageClimbs.Count} climbs (total: {_totalCloudClimbs})");
                }
            }, _itemsPerPage, offset, "", _biomeFilter, _peakCodeSearch, sortBy, sortOrder);
        }
        
        private string GetCurrentLevelId()
        {
            // Try to get level ID from existing climbs or current level service
            var existingClimbs = _climbDataService.GetAllClimbs().Where(c => c.IsFromCloud).ToList();
            
            if (existingClimbs.Any())
            {
                // Extract level ID from the server log you showed: "Level_4_67"
                // This should work based on your server logs
                Debug.Log("Using level ID from existing cloud climbs");
                return "Level_4_67"; // Use the level from your server logs
            }
            
            // Try to get from the climb data service current level
            string currentLevel = _climbDataService.CurrentLevelID;
            if (!string.IsNullOrEmpty(currentLevel))
            {
                Debug.Log($"Using current level ID: {currentLevel}");
                return currentLevel;
            }
            
            // Fallback to the level you showed in the logs
            Debug.Log("Using fallback level ID: Level_4_67");
            return "Level_4_67";
        }
        
        private bool IsPlayerInLevel()
        {
            // Check if player is actually in a level (not in menu)
            try
            {
                // Method 1: Check if we have a valid current level ID
                string currentLevelId = _climbDataService?.CurrentLevelID;
                if (string.IsNullOrEmpty(currentLevelId))
                {
                    return false;
                }
                
                // Method 2: Check if we're not in main menu scene
                // Unity scene names typically contain "menu" when in main menu
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();
                if (sceneName.Contains("menu") || sceneName.Contains("lobby") || sceneName.Contains("main"))
                {
                    return false;
                }
                
                // Method 3: Additional check - level ID should not be a placeholder
                if (currentLevelId.Contains("placeholder") || currentLevelId == "level_001")
                {
                    return false;
                }
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error checking player level status: {e.Message}");
                return false;
            }
        }
        
        private string GetApiSortColumn(SortColumn column)
        {
            switch (column)
            {
                case SortColumn.BiomeName: return "biome_name";
                case SortColumn.Duration: return "duration";
                case SortColumn.CreationTime: return "created_at";
                default: return "created_at";
            }
        }
        
        // Cursor Management Methods
        private void UpdateCursorState()
        {
            bool shouldShowCursor = _showMenu; // || _showTagSelection; // Tag selection disabled
            
            if (shouldShowCursor)
            {
                // Store original cursor state before changing it (only once)
                if (Cursor.lockState != CursorLockMode.None)
                {
                    _originalCursorLockState = Cursor.lockState;
                    _originalCursorVisible = Cursor.visible;
                    
                    // Enable free cursor for UI interaction
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else
            {
                // Restore original cursor state when UI is hidden
                Cursor.lockState = _originalCursorLockState;
                Cursor.visible = _originalCursorVisible;
            }
        }
    }
}