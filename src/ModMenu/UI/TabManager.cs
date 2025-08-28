using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FollowMePeak.ModMenu.UI.Helpers;

namespace FollowMePeak.ModMenu.UI
{
    /// <summary>
    /// Manages tab switching and visual states
    /// </summary>
    public class TabManager
    {
        // Tab buttons
        private Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
        private Dictionary<string, GameObject> _tabPages = new Dictionary<string, GameObject>();
        
        // Visual states
        private readonly Color _activeTabColor = new Color(1f, 1f, 1f, 1f);
        private readonly Color _inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        private readonly Color _activeTextColor = new Color(0.196f, 0.196f, 0.196f, 1f);
        private readonly Color _inactiveTextColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        // Current active tab
        private string _currentActiveTab = "";
        
        // Tab change callback
        public delegate void TabChangedHandler(string tabName);
        public event TabChangedHandler OnTabChanged;
        
        public void Initialize(GameObject root)
        {
            Debug.Log("[TabManager] Initializing");
            FindTabElements(root);
        }
        
        private void FindTabElements(GameObject root)
        {
            // Find tab buttons
            Transform tabMenu = root.transform.Find("MyModMenuPanel/TabMenu");
            if (tabMenu != null)
            {
                RegisterTabButton(tabMenu, "ClimbsButton", "Climbs");
                RegisterTabButton(tabMenu, "CloudSyncButton", "CloudSync");
            }
            
            // Find tab pages
            Transform pages = root.transform.Find("MyModMenuPanel/Pages");
            if (pages != null)
            {
                RegisterTabPage(pages, "ClimbsPage", "Climbs");
                RegisterTabPage(pages, "CloudSyncPage", "CloudSync");
            }
        }
        
        private void RegisterTabButton(Transform parent, string buttonName, string tabId)
        {
            Transform buttonTransform = parent.Find(buttonName);
            if (buttonTransform != null)
            {
                Button button = buttonTransform.GetComponent<Button>();
                if (button != null)
                {
                    _tabButtons[tabId] = button;
                    
                    // Setup click listener
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => SwitchToTab(tabId));
                    
                    Debug.Log($"[TabManager] Registered tab button: {tabId}");
                }
            }
        }
        
        private void RegisterTabPage(Transform parent, string pageName, string tabId)
        {
            Transform pageTransform = parent.Find(pageName);
            if (pageTransform != null)
            {
                _tabPages[tabId] = pageTransform.gameObject;
                Debug.Log($"[TabManager] Registered tab page: {tabId}");
            }
        }
        
        public void RegisterTab(string tabId, Button button, GameObject page)
        {
            if (button != null)
            {
                _tabButtons[tabId] = button;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SwitchToTab(tabId));
            }
            
            if (page != null)
            {
                _tabPages[tabId] = page;
            }
            
            Debug.Log($"[TabManager] Manually registered tab: {tabId}");
        }
        
        public void SwitchToTab(string tabId)
        {
            Debug.Log($"[TabManager] Switching to tab: {tabId}");
            
            if (!_tabButtons.ContainsKey(tabId) || !_tabPages.ContainsKey(tabId))
            {
                Debug.LogError($"[TabManager] Tab '{tabId}' not found");
                return;
            }
            
            // Hide all pages and update button visuals
            foreach (var kvp in _tabPages)
            {
                bool isActive = kvp.Key == tabId;
                
                // Show/hide page
                if (kvp.Value != null)
                    kvp.Value.SetActive(isActive);
                
                // Update button visual
                if (_tabButtons.ContainsKey(kvp.Key))
                {
                    UpdateTabVisuals(_tabButtons[kvp.Key], isActive);
                }
            }
            
            _currentActiveTab = tabId;
            
            // Trigger callback
            OnTabChanged?.Invoke(tabId);
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
        
        public void SetActiveTab(string tabId)
        {
            SwitchToTab(tabId);
        }
        
        public string GetActiveTab()
        {
            return _currentActiveTab;
        }
        
        public void Cleanup()
        {
            Debug.Log("[TabManager] Cleaning up");
            
            // Remove all button listeners
            foreach (var button in _tabButtons.Values)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
            
            _tabButtons.Clear();
            _tabPages.Clear();
            OnTabChanged = null;
        }
    }
}