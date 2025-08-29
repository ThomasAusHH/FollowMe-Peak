using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FollowMePeak.Services;
using FollowMePeak.Models;

namespace FollowMePeak.ModMenu.UI.Tabs.Components
{
    public class ClimbServerLoader
    {
        private VPSApiService _apiService;
        private ClimbDataService _climbDataService;
        private bool _hasLoadedForCurrentLevel = false;
        private string _lastLoadedLevel = "";
        private string _lastBiomeFilter = "";
        private int? _lastAscentFilter = null;
        private string _lastPeakCodeFilter = "";
        private string _lastSortBy = "created_at";     // Default
        private string _lastSortOrder = "desc";        // Default
        private bool _isLoading = false;
        private List<ClimbData> _currentPageClimbs = new List<ClimbData>();
        
        public event Action<List<ClimbData>> OnServerClimbsLoaded;
        public event Action<string> OnLoadError;
        public event Action<int, int> OnPaginationUpdated;
        
        public List<ClimbData> CurrentPageClimbs => _currentPageClimbs;
        public bool IsLoading => _isLoading;
        public string LastPeakCodeFilter => _lastPeakCodeFilter;
        
        public ClimbServerLoader(VPSApiService apiService, ClimbDataService climbDataService)
        {
            _apiService = apiService;
            _climbDataService = climbDataService;
        }
        
        public void CheckAndLoadInitialData(string biomeFilter = "", int? ascentFilter = null, string peakCodeFilter = "")
        {
            // Check if we have a valid level ID (this is more reliable than scene name)
            string levelId = _climbDataService?.CurrentLevelID;
            if (string.IsNullOrEmpty(levelId) || levelId.Contains("_unknown") || levelId.Contains("placeholder"))
            {
                Debug.Log($"[ClimbServerLoader] Not in a valid level (levelId: {levelId}), skipping load");
                _currentPageClimbs.Clear();
                return;
            }
            
            // Check if we already loaded for this level and filter combination
            if (_hasLoadedForCurrentLevel && _lastLoadedLevel == levelId && 
                _lastBiomeFilter == biomeFilter && _lastAscentFilter == ascentFilter &&
                _lastPeakCodeFilter == peakCodeFilter)
            {
                Debug.Log($"[ClimbServerLoader] Already loaded climbs for {levelId} with filters");
                return;
            }
            
            // Load climbs from server
            LoadClimbsFromServer(levelId, biomeFilter, ascentFilter, peakCodeFilter);
        }
        
        private void LoadClimbsFromServer(string levelId, string biomeFilter = "", int? ascentFilter = null, 
            string peakCodeFilter = "", string sortBy = "created_at", string sortOrder = "desc")
        {
            if (_isLoading)
            {
                Debug.Log("[ClimbServerLoader] Already loading, skipping");
                return;
            }
            
            if (_apiService == null)
            {
                Debug.LogError("[ClimbServerLoader] API Service not available");
                OnLoadError?.Invoke("API Service not available");
                return;
            }
            
            if (string.IsNullOrEmpty(levelId))
            {
                Debug.LogWarning("[ClimbServerLoader] No level ID provided");
                OnLoadError?.Invoke("No level ID available");
                return;
            }
            
            _isLoading = true;
            Debug.Log($"[ClimbServerLoader] Loading climbs - Level: {levelId}, Biome: '{biomeFilter}', Ascent: '{ascentFilter}', Peak: '{peakCodeFilter}', Sort: {sortBy} {sortOrder}");
            
            // Clear previous server climbs
            _currentPageClimbs.Clear();
            
            // Request first 25 climbs using the DownloadClimbs method with biome filter
            _apiService.DownloadClimbs(levelId, (downloadedClimbs, error, meta) =>
            {
                _isLoading = false;
                
                if (downloadedClimbs != null)
                {
                    Debug.Log($"[ClimbServerLoader] Received {downloadedClimbs.Count} climbs from server");
                    
                    // Store climbs for display
                    _currentPageClimbs.Clear();
                    foreach (var climb in downloadedClimbs)
                    {
                        climb.IsFromCloud = true;
                        
                        // Check if we already have this climb locally
                        var existingClimb = _climbDataService.GetAllClimbs()
                            .FirstOrDefault(c => c.Id == climb.Id || 
                                (c.ShareCode != null && climb.ShareCode != null && c.ShareCode == climb.ShareCode));
                        
                        if (existingClimb == null)
                        {
                            // Add to data service if not already present
                            _climbDataService.AddClimb(climb);
                        }
                        
                        _currentPageClimbs.Add(climb);
                    }
                    
                    _hasLoadedForCurrentLevel = true;
                    _lastLoadedLevel = levelId;
                    _lastBiomeFilter = biomeFilter;
                    _lastAscentFilter = ascentFilter;
                    _lastPeakCodeFilter = peakCodeFilter;
                    _lastSortBy = sortBy;
                    _lastSortOrder = sortOrder;
                    
                    // Save climbs locally
                    _climbDataService.SaveClimbsToFile(false);
                    
                    // Notify about pagination (based on meta if available)
                    if (meta != null)
                    {
                        int totalPages = meta.Total > 0 ? (int)Math.Ceiling((float)meta.Total / 25) : 1;
                        OnPaginationUpdated?.Invoke(0, totalPages);
                    }
                    else
                    {
                        OnPaginationUpdated?.Invoke(0, 1);
                    }
                    
                    // Notify that climbs are loaded
                    OnServerClimbsLoaded?.Invoke(_currentPageClimbs);
                }
                else
                {
                    string errorMsg = error ?? "Failed to load climbs from server";
                    Debug.LogError($"[ClimbServerLoader] {errorMsg}");
                    OnLoadError?.Invoke(errorMsg);
                }
            }, 25, 0, "", biomeFilter, peakCodeFilter, sortBy, sortOrder, ascentFilter);
        }
        
        
        public void Reset()
        {
            _hasLoadedForCurrentLevel = false;
            _lastLoadedLevel = "";
            _lastBiomeFilter = "";
            _lastAscentFilter = null;
            _lastPeakCodeFilter = "";
            _lastSortBy = "created_at";      // Reset to default
            _lastSortOrder = "desc";          // Reset to default
            _isLoading = false;
            _currentPageClimbs.Clear();
        }
        
        // Call this when level changes to force reload on next show
        public void OnLevelChanged()
        {
            Debug.Log($"[ClimbServerLoader] Level changed, resetting state");
            Reset();
        }
        
        // Force reload with new filter
        public void ReloadWithBiomeFilter(string biomeFilter)
        {
            Debug.Log($"[ClimbServerLoader] Reloading with biome filter: {biomeFilter}");
            
            string levelId = _climbDataService?.CurrentLevelID;
            if (string.IsNullOrEmpty(levelId) || levelId.Contains("_unknown") || levelId.Contains("placeholder"))
            {
                Debug.Log($"[ClimbServerLoader] Not in a valid level, skipping reload");
                return;
            }
            
            // Force reload by resetting the cached filter
            _hasLoadedForCurrentLevel = false;
            // Keep current sorting when biome filter changes
            LoadClimbsFromServer(levelId, biomeFilter, _lastAscentFilter, _lastPeakCodeFilter, _lastSortBy, _lastSortOrder);
        }
        
        // Force reload with combined filters
        public void ReloadWithFilters(string biomeFilter, int? ascentLevel)
        {
            Debug.Log($"[ClimbServerLoader] Reloading - Biome: '{biomeFilter}', Ascent: {ascentLevel}");
            
            string levelId = _climbDataService?.CurrentLevelID;
            if (string.IsNullOrEmpty(levelId) || levelId.Contains("_unknown") || levelId.Contains("placeholder"))
            {
                Debug.Log($"[ClimbServerLoader] Not in a valid level, skipping reload");
                return;
            }
            
            // Force reload by resetting the cached filter
            _hasLoadedForCurrentLevel = false;
            _lastBiomeFilter = biomeFilter;
            _lastAscentFilter = ascentLevel;
            // Keep current sorting when filters change
            LoadClimbsFromServer(levelId, biomeFilter, ascentLevel, _lastPeakCodeFilter, _lastSortBy, _lastSortOrder);
        }
        
        // Force reload with all filters
        public void ReloadWithAllFilters(string biomeFilter, int? ascentLevel, string peakCode)
        {
            Debug.Log($"[ClimbServerLoader] Reloading - Biome: '{biomeFilter}', Ascent: {ascentLevel}, PeakCode: '{peakCode}'");
            
            string levelId = _climbDataService?.CurrentLevelID;
            if (string.IsNullOrEmpty(levelId) || levelId.Contains("_unknown") || levelId.Contains("placeholder"))
            {
                Debug.Log($"[ClimbServerLoader] Not in a valid level, skipping reload");
                return;
            }
            
            // Force reload by resetting the cached filter
            _hasLoadedForCurrentLevel = false;
            _lastBiomeFilter = biomeFilter;
            _lastAscentFilter = ascentLevel;
            _lastPeakCodeFilter = peakCode;
            // Keep current sorting when filters change
            LoadClimbsFromServer(levelId, biomeFilter, ascentLevel, peakCode, _lastSortBy, _lastSortOrder);
        }
        
        // Apply duration sorting
        public void ApplyDurationSorting(bool ascending)
        {
            string sortOrder = ascending ? "asc" : "desc";
            Debug.Log($"[ClimbServerLoader] Applying duration sorting: {sortOrder}");
            
            string levelId = _climbDataService?.CurrentLevelID;
            if (string.IsNullOrEmpty(levelId) || levelId.Contains("_unknown") || levelId.Contains("placeholder"))
            {
                Debug.Log($"[ClimbServerLoader] Not in valid level, skipping sorting");
                return;
            }
            
            // Only change to duration sorting, keep all other filters
            _hasLoadedForCurrentLevel = false;
            LoadClimbsFromServer(levelId, _lastBiomeFilter, _lastAscentFilter, 
                _lastPeakCodeFilter, "duration", sortOrder);
        }
        
        // Reset to default sorting
        public void ResetToDefaultSorting()
        {
            Debug.Log($"[ClimbServerLoader] Resetting to default sorting (created_at desc)");
            
            string levelId = _climbDataService?.CurrentLevelID;
            if (string.IsNullOrEmpty(levelId) || levelId.Contains("_unknown") || levelId.Contains("placeholder"))
            {
                return;
            }
            
            // Clear the peak code filter when resetting to defaults
            _lastPeakCodeFilter = "";
            _hasLoadedForCurrentLevel = false;
            LoadClimbsFromServer(levelId, _lastBiomeFilter, _lastAscentFilter, 
                "", "created_at", "desc");
        }
    }
}