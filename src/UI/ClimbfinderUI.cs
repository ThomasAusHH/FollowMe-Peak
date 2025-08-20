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
        
        private bool _showMenu = false;
        private Rect _menuRect = new Rect(20, 20, 800, 600);
        private Vector2 _scrollPosition = Vector2.zero;
        
        // Cursor management
        private CursorLockMode _originalCursorLockState;
        private bool _originalCursorVisible;
        
        private string _biomeFilter = "";
        private string _dateFilter = "";
        private string _nameFilter = "";
        private string _peakCodeSearch = "";
        private bool _showOnlyLocalPaths = false;
        private SortColumn _currentSortColumn = SortColumn.CreationTime;
        private bool _sortAscending = false;
        
        // Tab system
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "My Climbs", "Online Share" };
        
        // Tag selection system - DISABLED FOR NOW
        // private ClimbData _currentTagEditClimb = null;
        // private bool _showTagSelection = false;
        // private System.Action<ClimbData> _tagSelectionCallback = null;

        private enum SortColumn { CreationTime, BiomeName, Duration, PlayerName }

        public FollowMeUI(ClimbDataService climbDataService, ClimbVisualizationManager visualizationManager,
            ServerConfigService serverConfigService, VPSApiService vpsApiService,
            ClimbUploadService climbUploadService, ClimbDownloadService climbDownloadService)
        {
            _climbDataService = climbDataService;
            _visualizationManager = visualizationManager;
            _vpsApiService = vpsApiService;
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
            // Search and Filter Section
            GUILayout.BeginVertical("box");
            GUILayout.Label("Search & Filter");
            
            // First row: Text filters
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(50));
            _nameFilter = GUILayout.TextField(_nameFilter, GUILayout.Width(120));
            GUILayout.Label("Biome:", GUILayout.Width(50));
            _biomeFilter = GUILayout.TextField(_biomeFilter, GUILayout.Width(100));
            GUILayout.Label("Peak Code:", GUILayout.Width(70));
            _peakCodeSearch = GUILayout.TextField(_peakCodeSearch, GUILayout.Width(80));
            GUILayout.EndHorizontal();
            
            // Second row: Buttons and toggles
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Search", GUILayout.Width(100)))
            {
                // Manual search trigger
                if (!string.IsNullOrEmpty(_peakCodeSearch) && _peakCodeSearch.Length >= 4)
                {
                    SearchForPeakCode(_peakCodeSearch);
                }
            }
            
            if (GUILayout.Button("Clear Filters", GUILayout.Width(120)))
            {
                // Clear all filters
                _nameFilter = "";
                _biomeFilter = "";
                _peakCodeSearch = "";
                _showOnlyLocalPaths = false;
            }
            
            GUILayout.FlexibleSpace();
            
            bool newShowOnlyLocal = GUILayout.Toggle(_showOnlyLocalPaths, "Local climbs only");
            if (newShowOnlyLocal != _showOnlyLocalPaths)
            {
                _showOnlyLocalPaths = newShowOnlyLocal;
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();

            // Table Header
            GUILayout.BeginHorizontal("box");
            GUILayout.Label("Vis", GUILayout.Width(30)); // Visibility toggle
            if(GUILayout.Button(GetSortLabel("Name", SortColumn.PlayerName), GUILayout.Width(120))) SetSortColumn(SortColumn.PlayerName);
            if(GUILayout.Button(GetSortLabel("Biome", SortColumn.BiomeName), GUILayout.Width(100))) SetSortColumn(SortColumn.BiomeName);
            if(GUILayout.Button(GetSortLabel("Duration", SortColumn.Duration), GUILayout.Width(70))) SetSortColumn(SortColumn.Duration);
            if(GUILayout.Button(GetSortLabel("Date", SortColumn.CreationTime), GUILayout.Width(80))) SetSortColumn(SortColumn.CreationTime);
            GUILayout.Label("Peak Code", GUILayout.Width(70));
            // GUILayout.Label("Tags", GUILayout.Width(80)); // DISABLED FOR NOW
            GUILayout.Label("Actions", GUILayout.Width(120)); // Actions
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
        
        private List<ClimbData> GetProcessedClimbs()
        {
            IEnumerable<ClimbData> processed = _climbDataService.GetAllClimbs();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(_biomeFilter))
                processed = processed.Where(p => (p.BiomeName ?? "").IndexOf(_biomeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                
            if (!string.IsNullOrWhiteSpace(_dateFilter))
                processed = processed.Where(p => p.CreationTime.ToString("dd.MM.yy").Contains(_dateFilter));
                
            if (!string.IsNullOrWhiteSpace(_nameFilter))
                processed = processed.Where(p => p.GetDisplayName().IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                
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
                case SortColumn.PlayerName:
                    processed = _sortAscending ? processed.OrderBy(p => p.GetDisplayName()) : processed.OrderByDescending(p => p.GetDisplayName());
                    break;
            }
            return processed.ToList();
        }

        private void SetSortColumn(SortColumn column)
        {
            if (_currentSortColumn == column) _sortAscending = !_sortAscending;
            else
            {
                _currentSortColumn = column;
                _sortAscending = true;
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