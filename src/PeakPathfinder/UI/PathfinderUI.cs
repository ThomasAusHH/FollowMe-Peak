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
        
        private bool _showMenu = false;
        private Rect _menuRect = new Rect(20, 20, 550, 500);
        private Vector2 _scrollPosition = Vector2.zero;
        
        private string _biomeFilter = "";
        private string _dateFilter = "";
        private SortColumn _currentSortColumn = SortColumn.CreationTime;
        private bool _sortAscending = false;

        private enum SortColumn { CreationTime, BiomeName, Duration }

        public PathfinderUI(PathDataService pathDataService, PathVisualizationManager visualizationManager)
        {
            _pathDataService = pathDataService;
            _visualizationManager = visualizationManager;
        }

        public bool ShowMenu
        {
            get => _showMenu;
            set => _showMenu = value;
        }

        public void OnGUI()
        {
            if (_showMenu)
            {
                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                _menuRect = GUILayout.Window(0, _menuRect, DrawMenuWindow, "Peak Pathfinder - Journal");
            }
        }

        private void DrawMenuWindow(int windowID)
        {
            GUILayout.BeginHorizontal("box");
            GUILayout.Label("Filter:", GUILayout.Width(50));
            GUILayout.Label("Biom:", GUILayout.Width(40));
            _biomeFilter = GUILayout.TextField(_biomeFilter, GUILayout.Width(100));
            GUILayout.Label("Datum (TT.MM.JJ):", GUILayout.Width(120));
            _dateFilter = GUILayout.TextField(_dateFilter, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if(GUILayout.Button(GetSortLabel("Biom", SortColumn.BiomeName))) SetSortColumn(SortColumn.BiomeName);
            if(GUILayout.Button(GetSortLabel("Dauer", SortColumn.Duration))) SetSortColumn(SortColumn.Duration);
            if(GUILayout.Button(GetSortLabel("Datum", SortColumn.CreationTime))) SetSortColumn(SortColumn.CreationTime);
            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            var processedPaths = GetProcessedPaths();
            List<Guid> pathsToDelete = new List<Guid>();

            foreach (var pathData in processedPaths)
            {
                GUILayout.BeginHorizontal("box");

                bool isVisible = _visualizationManager.IsPathVisible(pathData.Id);
                if (GUILayout.Toggle(isVisible, "👁️") != isVisible)
                {
                    _visualizationManager.TogglePathVisibility(pathData.Id);
                }

                TimeSpan time = TimeSpan.FromSeconds(pathData.DurationInSeconds);
                string durationString = $"{time.Minutes:D2}m:{time.Seconds:D2}s";
                GUILayout.Label($"[{pathData.BiomeName ?? "N/A"}] - {durationString} ({pathData.CreationTime:dd.MM.yy})", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("X", GUILayout.Width(30))) pathsToDelete.Add(pathData.Id);
                
                GUILayout.EndHorizontal();
            }

            if (pathsToDelete.Count > 0)
            {
                _pathDataService.DeletePaths(pathsToDelete);
                _visualizationManager.UpdateVisuals();
            }

            GUILayout.EndScrollView();
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Alle anzeigen")) _visualizationManager.ShowAllPaths();
            if(GUILayout.Button("Alle ausblenden")) _visualizationManager.HideAllPaths();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
        
        private List<PathData> GetProcessedPaths()
        {
            IEnumerable<PathData> processed = _pathDataService.GetAllPaths();

            if (!string.IsNullOrWhiteSpace(_biomeFilter))
                processed = processed.Where(p => p.BiomeName.IndexOf(_biomeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(_dateFilter))
                processed = processed.Where(p => p.CreationTime.ToString("dd.MM.yy").Contains(_dateFilter));
            
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
    }
}