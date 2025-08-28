using System;
using System.Linq;
using UnityEngine;
using FollowMePeak.Services;
using FollowMePeak.Models;

namespace FollowMePeak.ModMenu.UI.Tabs.Components
{
    public class ClimbSearchManager
    {
        private VPSApiService _apiService;
        private ClimbDataService _climbDataService;
        
        public event Action<ClimbData> OnClimbFound;
        public event Action<string> OnSearchFailed;
        
        public ClimbSearchManager(VPSApiService apiService, ClimbDataService climbDataService)
        {
            _apiService = apiService;
            _climbDataService = climbDataService;
        }
        
        public void SearchByCode(string searchCode)
        {
            if (string.IsNullOrWhiteSpace(searchCode))
            {
                Debug.Log("[ClimbSearch] No climb code entered");
                return;
            }
            
            searchCode = searchCode.Trim();
            Debug.Log($"[ClimbSearch] Searching for climb code: {searchCode}");
            
            // Search locally first
            var existingClimb = _climbDataService.GetAllClimbs()
                .FirstOrDefault(p => p.ShareCode != null && 
                    p.ShareCode.IndexOf(searchCode, StringComparison.OrdinalIgnoreCase) >= 0);
            
            if (existingClimb != null)
            {
                Debug.Log($"[ClimbSearch] Found climb locally: {existingClimb.GetDisplayName()}");
                OnClimbFound?.Invoke(existingClimb);
            }
            else
            {
                SearchOnServer(searchCode);
            }
        }
        
        private void SearchOnServer(string peakCode)
        {
            if (_apiService == null)
            {
                Debug.LogError("[ClimbSearch] API Service not available for server search");
                OnSearchFailed?.Invoke("API Service not available");
                return;
            }
            
            _apiService.SearchClimbByPeakCode(peakCode, (climbData, error) =>
            {
                if (climbData != null)
                {
                    climbData.IsFromCloud = true;
                    _climbDataService.AddClimb(climbData);
                    
                    Debug.Log($"[ClimbSearch] Climb found on server: {climbData.GetDisplayName()}");
                    OnClimbFound?.Invoke(climbData);
                }
                else
                {
                    string message = $"Climb code '{peakCode}' not found. {error ?? "Climb does not exist."}";
                    Debug.Log($"[ClimbSearch] {message}");
                    OnSearchFailed?.Invoke(message);
                }
            });
        }
    }
}