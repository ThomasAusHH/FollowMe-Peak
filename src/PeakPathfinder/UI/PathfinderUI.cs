using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PeakPathfinder.Models;
using PeakPathfinder.Services;
using PeakPathfinder.Managers;

namespace PeakPathfinder.UI
{
    public class PathfinderUI
    {
        private readonly PathDataService _pathDataService;
        private readonly PathVisualizationManager _visualizationManager;
        private readonly CloudSyncUI _cloudSyncUI;
        private readonly VPSApiService _vpsApiService;
        
        private bool _showMenu = false;
        private Rect _menuRect = new Rect(20, 20, 600, 600);
        private Vector2 _scrollPosition = Vector2.zero;
        
        // Cursor management
        private CursorLockMode _originalCursorLockState;
        private bool _originalCursorVisible;
        
        private string _biomeFilter = "";
        private string _dateFilter = "";
        private string _nameFilter = "";
        private string _gipfelcodeSearch = "";
        private bool _showOnlyLocalPaths = false;
        private SortColumn _currentSortColumn = SortColumn.CreationTime;
        private bool _sortAscending = false;
        
        // Tab system
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "📂 Meine Pfade", "🌐 Online Teilen" };
        
        // Tag selection system
        private PathData _currentTagEditPath = null;
        private bool _showTagSelection = false;
        private System.Action<PathData> _tagSelectionCallback = null;

        private enum SortColumn { CreationTime, BiomeName, Duration, PlayerName }

        public PathfinderUI(PathDataService pathDataService, PathVisualizationManager visualizationManager,
            ServerConfigService serverConfigService, VPSApiService vpsApiService,
            PathUploadService pathUploadService, PathDownloadService pathDownloadService)
        {
            _pathDataService = pathDataService;
            _visualizationManager = visualizationManager;
            _vpsApiService = vpsApiService;
            _cloudSyncUI = new CloudSyncUI(serverConfigService, vpsApiService, pathUploadService, pathDownloadService);
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
                _menuRect = GUILayout.Window(0, _menuRect, DrawMenuWindow, "Peak Pathfinder - Journal");
            }
            
            // Draw tag selection overlay OUTSIDE of the main window context
            if (_showTagSelection && _currentTagEditPath != null)
            {
                DrawTagSelectionOverlay();
            }
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
                    DrawPathJournalTab();
                    break;
                case 1:
                    DrawCloudSyncTab();
                    break;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawPathJournalTab()
        {
            // Search and Filter Section
            GUILayout.BeginVertical("box");
            GUILayout.Label("Suchen & Filtern");
            
            // First row: Text filters
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(50));
            _nameFilter = GUILayout.TextField(_nameFilter, GUILayout.Width(120));
            GUILayout.Label("Biom:", GUILayout.Width(50));
            _biomeFilter = GUILayout.TextField(_biomeFilter, GUILayout.Width(100));
            GUILayout.Label("Gipfelcode:", GUILayout.Width(70));
            _gipfelcodeSearch = GUILayout.TextField(_gipfelcodeSearch, GUILayout.Width(80));
            GUILayout.EndHorizontal();
            
            // Second row: Buttons and toggles
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Suchen", GUILayout.Width(100)))
            {
                // Manual search trigger
                if (!string.IsNullOrEmpty(_gipfelcodeSearch) && _gipfelcodeSearch.Length >= 4)
                {
                    SearchForGipfelcode(_gipfelcodeSearch);
                }
            }
            
            if (GUILayout.Button("🗑️ Filter löschen", GUILayout.Width(120)))
            {
                // Clear all filters
                _nameFilter = "";
                _biomeFilter = "";
                _gipfelcodeSearch = "";
                _showOnlyLocalPaths = false;
            }
            
            GUILayout.FlexibleSpace();
            
            bool newShowOnlyLocal = GUILayout.Toggle(_showOnlyLocalPaths, "Nur lokale Pfade");
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
            if(GUILayout.Button(GetSortLabel("Biom", SortColumn.BiomeName), GUILayout.Width(100))) SetSortColumn(SortColumn.BiomeName);
            if(GUILayout.Button(GetSortLabel("Dauer", SortColumn.Duration), GUILayout.Width(70))) SetSortColumn(SortColumn.Duration);
            if(GUILayout.Button(GetSortLabel("Datum", SortColumn.CreationTime), GUILayout.Width(80))) SetSortColumn(SortColumn.CreationTime);
            GUILayout.Label("Gipfelcode", GUILayout.Width(70));
            GUILayout.Label("Tags", GUILayout.Width(80));
            GUILayout.Label("Aktionen", GUILayout.Width(80)); // Actions
            GUILayout.EndHorizontal();

            // Table Content
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            var processedPaths = GetProcessedPaths();
            List<Guid> pathsToDelete = new List<Guid>();

            foreach (var pathData in processedPaths)
            {
                // Ensure ShareCode is generated
                pathData.GenerateShareCode();
                
                DrawPathTableRow(pathData, pathsToDelete);
            }

            if (pathsToDelete.Count > 0)
            {
                _pathDataService.DeletePaths(pathsToDelete);
                _visualizationManager.UpdateVisuals();
            }

            GUILayout.EndScrollView();
            
            // Action Buttons
            GUILayout.Space(10);
            GUILayout.BeginHorizontal("box");
            if(GUILayout.Button("Alle anzeigen")) _visualizationManager.ShowAllPaths();
            if(GUILayout.Button("Alle ausblenden")) _visualizationManager.HideAllPaths();
            
            GUILayout.Space(20);
            
            // Quick actions for cloud saves
            if (_pathDataService.GetAllPaths().Any(p => p.IsFromCloud))
            {
                if(GUILayout.Button("Nur Cloud-Pfade")) ShowOnlyCloudPaths();
                if(GUILayout.Button("Nur lokale Pfade")) ShowOnlyLocalPaths();
            }
            
            GUILayout.EndHorizontal();
        }

        private void DrawPathTableRow(PathData pathData, List<Guid> pathsToDelete)
        {
            GUILayout.BeginHorizontal("box");

            // Visibility Toggle
            bool isVisible = _visualizationManager.IsPathVisible(pathData.Id);
            bool newVisibility = GUILayout.Toggle(isVisible, "", GUILayout.Width(30));
            if (newVisibility != isVisible)
            {
                _visualizationManager.TogglePathVisibility(pathData.Id);
            }

            // Display Name (with cloud indicator)
            string displayName = pathData.GetDisplayName();
            if (pathData.IsFromCloud)
                displayName = $"C: {displayName}";
                
            GUILayout.Label(displayName, GUILayout.Width(120));

            // Biome
            string biomeDisplay = pathData.BiomeName ?? "Unknown";
            if (biomeDisplay.Length > 12) biomeDisplay = biomeDisplay.Substring(0, 12) + "...";
            GUILayout.Label(biomeDisplay, GUILayout.Width(100));

            // Duration
            TimeSpan time = TimeSpan.FromSeconds(pathData.DurationInSeconds);
            string durationString = $"{time.Minutes:D2}:{time.Seconds:D2}";
            GUILayout.Label(durationString, GUILayout.Width(70));

            // Date
            string dateString = pathData.CreationTime.ToString("dd.MM");
            GUILayout.Label(dateString, GUILayout.Width(80));

            // Gipfelcode
            GUILayout.Label(pathData.ShareCode, GUILayout.Width(70));
            
            // Tags
            string tagsDisplay = pathData.GetTagsDisplay();
            if (tagsDisplay.Length > 10) tagsDisplay = tagsDisplay.Substring(0, 10) + "...";
            GUILayout.Label(tagsDisplay, GUILayout.Width(80));

            // Action Buttons
            GUILayout.BeginHorizontal(GUILayout.Width(80));
            
            if (GUILayout.Button("Copy", GUILayout.Width(25)))
            {
                // Copy gipfelcode to clipboard
                Debug.Log($"Gipfelcode kopiert: {pathData.ShareCode}");
                GUIUtility.systemCopyBuffer = pathData.ShareCode;
            }
            
            if (GUILayout.Button("Tag", GUILayout.Width(25)))
            {
                // Show tag selection for this path
                ShowTagSelectionForPath(pathData);
            }
            
            if (GUILayout.Button("Del", GUILayout.Width(25)))
            {
                pathsToDelete.Add(pathData.Id);
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.EndHorizontal();
        }

        private void ShowOnlyCloudPaths()
        {
            var allPaths = _pathDataService.GetAllPaths();
            foreach (var path in allPaths)
            {
                if (path.IsFromCloud)
                    _visualizationManager.SetPathVisibility(path.Id, true);
                else
                    _visualizationManager.SetPathVisibility(path.Id, false);
            }
        }

        private void ShowOnlyLocalPaths()
        {
            var allPaths = _pathDataService.GetAllPaths();
            foreach (var path in allPaths)
            {
                if (!path.IsFromCloud)
                    _visualizationManager.SetPathVisibility(path.Id, true);
                else
                    _visualizationManager.SetPathVisibility(path.Id, false);
            }
        }

        private void DrawCloudSyncTab()
        {
            _cloudSyncUI.OnGUI();
        }
        
        private List<PathData> GetProcessedPaths()
        {
            IEnumerable<PathData> processed = _pathDataService.GetAllPaths();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(_biomeFilter))
                processed = processed.Where(p => (p.BiomeName ?? "").IndexOf(_biomeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                
            if (!string.IsNullOrWhiteSpace(_dateFilter))
                processed = processed.Where(p => p.CreationTime.ToString("dd.MM.yy").Contains(_dateFilter));
                
            if (!string.IsNullOrWhiteSpace(_nameFilter))
                processed = processed.Where(p => p.GetDisplayName().IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                
            if (!string.IsNullOrWhiteSpace(_gipfelcodeSearch))
                processed = processed.Where(p => (p.ShareCode ?? "").IndexOf(_gipfelcodeSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                
            // Apply "Nur lokale Pfade" filter
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

        private void SearchForGipfelcode(string gipfelcode)
        {
            Debug.Log($"Suche nach Gipfelcode: {gipfelcode}");
            
            // First search in local paths
            var existingPath = _pathDataService.GetAllPaths()
                .FirstOrDefault(p => p.ShareCode != null && 
                    p.ShareCode.IndexOf(gipfelcode, StringComparison.OrdinalIgnoreCase) >= 0);
            
            if (existingPath != null)
            {
                // Make the found path visible
                _visualizationManager.SetPathVisibility(existingPath.Id, true);
                Debug.Log($"Gipfelcode gefunden: {existingPath.GetDisplayName()}");
            }
            else
            {
                Debug.Log($"Gipfelcode '{gipfelcode}' nicht lokal gefunden. Suche auf Server...");
                SearchForGipfelcodeOnServer(gipfelcode);
            }
        }
        
        private void SearchForGipfelcodeOnServer(string gipfelcode)
        {
            if (_vpsApiService == null)
            {
                Debug.LogError("VPS API Service nicht verfügbar für Server-Suche");
                return;
            }
            
            _vpsApiService.SearchPathByGipfelcode(gipfelcode, (pathData, error) =>
            {
                if (pathData != null)
                {
                    // Path found on server, add it to local data and make it visible
                    pathData.IsFromCloud = true;
                    _pathDataService.AddPath(pathData);
                    _visualizationManager.UpdateVisuals();
                    _visualizationManager.SetPathVisibility(pathData.Id, true);
                    
                    Debug.Log($"Gipfelcode '{gipfelcode}' auf Server gefunden: {pathData.GetDisplayName()}");
                }
                else
                {
                    Debug.Log($"Gipfelcode '{gipfelcode}' nicht gefunden. {error ?? "Pfad existiert nicht."}");
                }
            });
        }
        
        // Tag Selection Methods
        private void ShowTagSelectionForPath(PathData pathData)
        {
            ShowTagSelectionForPath(pathData, null);
        }
        
        public void ShowTagSelectionForPath(PathData pathData, System.Action<PathData> callback)
        {
            _currentTagEditPath = pathData;
            _tagSelectionCallback = callback;
            if (!_showTagSelection)
            {
                _showTagSelection = true;
                UpdateCursorState();
            }
        }
        
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
                ? $"🏔️ Neue Kletterung erfolgreich! Tags wählen:"
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
                GUILayout.Label("Wähle passende Tags um anderen zu helfen:");
            }
            else
            {
                // This is editing an existing path
                GUILayout.Label("Wähle Tags für diesen Pfad:");
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
                        _pathDataService.SavePathsToFile(false); // Save changes
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("Keine Tags ausgewählt");
            }
            
            GUILayout.Space(10);
            
            // Available tags for selection
            GUILayout.Label("Verfügbare Tags:");
            foreach (string tag in PathTags.GetTagOptions())
            {
                if (!_currentTagEditPath.HasTag(tag))
                {
                    if (GUILayout.Button($"+ {tag}"))
                    {
                        _currentTagEditPath.AddTag(tag);
                        _pathDataService.SavePathsToFile(false); // Save changes
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
                if (GUILayout.Button("Schließen"))
                {
                    _showTagSelection = false;
                    _currentTagEditPath = null;
                    UpdateCursorState();
                }
            }
            
            GUILayout.EndHorizontal();
            
            GUI.DragWindow();
        }
        
        // Cursor Management Methods
        private void UpdateCursorState()
        {
            bool shouldShowCursor = _showMenu || _showTagSelection;
            
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