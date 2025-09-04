using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FollowMePeak.ModMenu.UI.Helpers;
using FollowMePeak.Services;
using FollowMePeak.Utils;

namespace FollowMePeak.ModMenu.UI.Tabs
{
    /// <summary>
    /// Controls the Cloud Sync tab UI
    /// </summary>
    public class CloudSyncTabController
    {
        // UI Elements - Main containers
        private GameObject _cloudSyncPage;
        private GameObject _cloudSyncActive;
        private GameObject _cloudSyncInactive;
        
        // UI Elements - Active area
        private Toggle _cloudSyncToggle;
        private Toggle _cloudSyncStatus;  // Non-interactive status display
        private Button _cloudSyncTestButton;
        private TextMeshProUGUI _cloudSyncTestLabel;
        
        // UI Elements - Name area
        private TMP_InputField _cloudSyncNameInput;
        private TextMeshProUGUI _cloudSyncActualName;
        private Button _cloudSyncNameSaveButton;
        
        // Services
        private ServerConfigService _serverConfig;
        private VPSApiService _apiService;
        
        public GameObject CloudSyncPage => _cloudSyncPage;
        
        public void Initialize(GameObject root, ServerConfigService serverConfig, VPSApiService apiService)
        {
            _serverConfig = serverConfig;
            _apiService = apiService;
            
            FindUIElements(root);
            SetupToggles();
            SetupButtons();
            LoadSettings();
            UpdateActiveInactiveVisibility();
        }
        
        private void FindUIElements(GameObject root)
        {
            ModLogger.Instance?.Info("[CloudSyncTab] Finding UI elements");
            
            // Find Cloud Sync Page
            Transform pages = root.transform.Find("MyModMenuPanel/Pages");
            if (pages != null)
            {
                Transform cloudSyncPage = pages.Find("CloudSyncPage");
                if (cloudSyncPage != null) _cloudSyncPage = cloudSyncPage.gameObject;
            }
            
            if (_cloudSyncPage == null) return;
            
            // Find Active/Inactive containers
            Transform cloudSyncActive = _cloudSyncPage.transform.Find("CloudSyncActive");
            if (cloudSyncActive != null) _cloudSyncActive = cloudSyncActive.gameObject;
            
            Transform cloudSyncInactive = _cloudSyncPage.transform.Find("CloudSyncInactive");
            if (cloudSyncInactive != null) _cloudSyncInactive = cloudSyncInactive.gameObject;
            
            // Find the activation toggle in CloudSyncInactive first
            if (_cloudSyncInactive != null)
            {
                Toggle[] inactiveToggles = _cloudSyncInactive.GetComponentsInChildren<Toggle>();
                foreach (var toggle in inactiveToggles)
                {
                    // Use the first toggle found in inactive area as the activation toggle
                    if (_cloudSyncToggle == null)
                    {
                        _cloudSyncToggle = toggle;
                        break;
                    }
                }
            }
            
            // Find elements in Active container
            if (_cloudSyncActive != null)
            {
                // First try to find CloudSyncActiveArea
                Transform activeArea = _cloudSyncActive.transform.Find("CloudSyncActiveArea");
                
                // Search for toggles in the appropriate container
                Transform searchContainer = activeArea != null ? activeArea : _cloudSyncActive.transform;
                
                // Find all toggles in the container
                Toggle[] allToggles = searchContainer.GetComponentsInChildren<Toggle>();
                
                // Only search for toggle if we haven't found it yet in CloudSyncInactive
                if (_cloudSyncToggle == null)
                {
                    // Try to find specific toggle by name first
                    _cloudSyncToggle = UIElementFinder.FindComponent<Toggle>(searchContainer, "CloudSyncToggle");
                    
                    // If not found, look for any toggle that's not the status toggle
                    if (_cloudSyncToggle == null && allToggles.Length > 0)
                    {
                        foreach (var toggle in allToggles)
                        {
                            if (!toggle.name.Contains("Status"))
                            {
                                _cloudSyncToggle = toggle;
                                break;
                            }
                        }
                    }
                }
                
                _cloudSyncStatus = UIElementFinder.FindComponent<Toggle>(searchContainer, "CloudSyncStatus");
                _cloudSyncTestButton = UIElementFinder.FindComponent<Button>(searchContainer, "CloudSyncTestButton");
                
                // If test button not found in active area, search entire CloudSyncActive
                if (_cloudSyncTestButton == null && _cloudSyncActive != null)
                {
                    ModLogger.Instance?.Info("[CloudSyncTab] Searching for CloudSyncTestButton in entire CloudSyncActive...");
                    _cloudSyncTestButton = UIElementFinder.FindComponent<Button>(_cloudSyncActive.transform, "CloudSyncTestButton");
                    ModLogger.Instance?.Info($"[CloudSyncTab] CloudSyncTestButton found in CloudSyncActive: {_cloudSyncTestButton != null}");
                }
                
                // If still not found, search in CloudSyncPage
                if (_cloudSyncTestButton == null && _cloudSyncPage != null)
                {
                    ModLogger.Instance?.Info("[CloudSyncTab] Searching for CloudSyncTestButton in entire CloudSyncPage...");
                    Button[] allButtons = _cloudSyncPage.GetComponentsInChildren<Button>();
                    foreach (var button in allButtons)
                    {
                        if (button.name.Contains("Test") || button.name.Contains("CloudSyncTest"))
                        {
                            _cloudSyncTestButton = button;
                            ModLogger.Instance?.Info($"[CloudSyncTab] Found test button: {button.name} at {GetGameObjectPath(button.gameObject)}");
                            break;
                        }
                    }
                }
                
                // Search for status toggle if not found
                if (_cloudSyncStatus == null && _cloudSyncActive != null)
                {
                    ModLogger.Instance?.Info("[CloudSyncTab] Searching for CloudSyncStatus toggle in entire CloudSyncActive...");
                    Toggle[] statusToggles = _cloudSyncActive.GetComponentsInChildren<Toggle>();
                    foreach (var toggle in statusToggles)
                    {
                        if (toggle.name.Contains("Status"))
                        {
                            _cloudSyncStatus = toggle;
                            ModLogger.Instance?.Info($"[CloudSyncTab] Found status toggle: {toggle.name}");
                            break;
                        }
                    }
                }
                
                // Find the label within the test button
                if (_cloudSyncTestButton != null)
                {
                    _cloudSyncTestLabel = _cloudSyncTestButton.GetComponentInChildren<TextMeshProUGUI>();
                }
                
                // Find CloudSyncNameArea
                Transform nameArea = _cloudSyncActive.transform.Find("CloudSyncNameArea");
                ModLogger.Instance?.Info($"[CloudSyncTab] CloudSyncNameArea found: {nameArea != null}");
                if (nameArea != null)
                {
                    _cloudSyncActualName = UIElementFinder.FindComponent<TextMeshProUGUI>(nameArea, "CloudSyncName/CloudSyncActualName");
                    _cloudSyncNameInput = UIElementFinder.FindComponent<TMP_InputField>(nameArea, "CloudSyncName/CloudSyncNameEnter");
                    _cloudSyncNameSaveButton = UIElementFinder.FindComponent<Button>(nameArea, "CloudSyncNameSaveButton");
                    
                    ModLogger.Instance?.Info($"[CloudSyncTab] CloudSyncNameSaveButton found in nameArea: {_cloudSyncNameSaveButton != null}");
                }
                
                // If save button not found, search in entire CloudSyncActive
                if (_cloudSyncNameSaveButton == null)
                {
                    ModLogger.Instance?.Info("[CloudSyncTab] Searching for CloudSyncNameSaveButton in entire CloudSyncActive...");
                    _cloudSyncNameSaveButton = UIElementFinder.FindComponent<Button>(_cloudSyncActive.transform, "CloudSyncNameSaveButton");
                    ModLogger.Instance?.Info($"[CloudSyncTab] CloudSyncNameSaveButton found in CloudSyncActive: {_cloudSyncNameSaveButton != null}");
                }
            }
            
            // If save button still not found, search entire page
            if (_cloudSyncNameSaveButton == null && _cloudSyncPage != null)
            {
                ModLogger.Instance?.Info("[CloudSyncTab] Searching for CloudSyncNameSaveButton in entire CloudSyncPage...");
                Button[] allButtons = _cloudSyncPage.GetComponentsInChildren<Button>();
                ModLogger.Instance?.Info($"[CloudSyncTab] Found {allButtons.Length} buttons in CloudSyncPage:");
                foreach (var button in allButtons)
                {
                    ModLogger.Instance?.Info($"[CloudSyncTab]   - Button: {button.name} (path: {GetGameObjectPath(button.gameObject)})");
                    if (button.name.Contains("CloudSyncNameSaveButton") || button.name.Contains("Save"))
                    {
                        _cloudSyncNameSaveButton = button;
                        ModLogger.Instance?.Info($"[CloudSyncTab] Found save button: {button.name}");
                        break;
                    }
                }
            }
            
            // Fallback: if still no toggle found, search entire CloudSyncPage
            if (_cloudSyncToggle == null && _cloudSyncPage != null)
            {
                Toggle[] allPageToggles = _cloudSyncPage.GetComponentsInChildren<Toggle>();
                foreach (var toggle in allPageToggles)
                {
                    // Skip status toggles and use the first non-status toggle
                    if (!toggle.name.Contains("Status"))
                    {
                        _cloudSyncToggle = toggle;
                        break;
                    }
                }
            }
            
            if (_cloudSyncToggle == null)
            {
                ModLogger.Instance?.Error("[CloudSyncTab] CRITICAL: No CloudSync activation toggle found anywhere!");
            }
        }
        
        private void SetupToggles()
        {
            ModLogger.Instance?.Info("[CloudSyncTab] Setting up toggles");
            
            if (_cloudSyncToggle != null)
            {
                _cloudSyncToggle.onValueChanged.RemoveAllListeners();
                _cloudSyncToggle.onValueChanged.AddListener(OnCloudSyncToggled);
            }
            else
            {
                ModLogger.Instance?.Error("[CloudSyncTab] Cannot setup toggle - _cloudSyncToggle is null!");
            }
            
            // Make status toggle non-interactive (read-only visual indicator)
            if (_cloudSyncStatus != null)
            {
                _cloudSyncStatus.interactable = false;
                ModLogger.Instance?.Info($"[CloudSyncTab] Status toggle set to non-interactive (read-only)");
            }
            else
            {
                ModLogger.Instance?.Warning("[CloudSyncTab] CloudSyncStatus toggle not found - cannot set to non-interactive");
            }
        }
        
        private void SetupButtons()
        {
            ModLogger.Instance?.Info("[CloudSyncTab] Setting up buttons");
            
            if (_cloudSyncNameSaveButton != null)
            {
                ModLogger.Instance?.Info($"[CloudSyncTab] Attaching listener to CloudSyncNameSaveButton: {_cloudSyncNameSaveButton.name}");
                _cloudSyncNameSaveButton.onClick.RemoveAllListeners();
                _cloudSyncNameSaveButton.onClick.AddListener(SaveCloudSyncName);
                ModLogger.Instance?.Info("[CloudSyncTab] SaveCloudSyncName listener attached successfully");
            }
            else
            {
                ModLogger.Instance?.Error("[CloudSyncTab] CloudSyncNameSaveButton is null - cannot attach listener!");
            }
            
            if (_cloudSyncTestButton != null)
            {
                ModLogger.Instance?.Info($"[CloudSyncTab] Attaching listener to CloudSyncTestButton: {_cloudSyncTestButton.name}");
                _cloudSyncTestButton.onClick.RemoveAllListeners();
                _cloudSyncTestButton.onClick.AddListener(TestConnection);
                ModLogger.Instance?.Info("[CloudSyncTab] TestConnection listener attached successfully");
            }
            else
            {
                ModLogger.Instance?.Error("[CloudSyncTab] CloudSyncTestButton is null - cannot attach listener!");
            }
        }
        
        private void OnCloudSyncToggled(bool value)
        {
            ModLogger.Instance?.Info($"[CloudSyncTab] Cloud Sync toggled: {value}");
            
            if (_serverConfig != null)
            {
                // Use the service method which properly handles enabling/disabling cloud sync
                _serverConfig.SetCloudSyncEnabled(value);
                
                // Update UI visibility
                UpdateActiveInactiveVisibility();
                
                // Update connection status if enabled
                if (value)
                {
                    UpdateConnectionStatus();
                }
            }
        }
        
        private void SaveCloudSyncName()
        {
            if (_cloudSyncNameInput == null || _serverConfig == null) return;
            
            string newName = _cloudSyncNameInput.text;
            ModLogger.Instance?.Info($"[CloudSyncTab] SaveCloudSyncName called with: '{newName}'");
            
            if (!string.IsNullOrEmpty(newName))
            {
                ModLogger.Instance?.Info($"[CloudSyncTab] Saving player name: {newName}");
                // Use the service method which properly handles setting player name
                _serverConfig.SetPlayerName(newName);
                
                // Update display to show the saved name (might be sanitized by SetPlayerName)
                if (_cloudSyncActualName != null)
                {
                    _cloudSyncActualName.text = _serverConfig.Config.PlayerName;
                }
                
                // Don't clear input field - keep showing the current name
                // This matches the old UI behavior
                // Update input field with the sanitized name
                _cloudSyncNameInput.text = _serverConfig.Config.PlayerName;
            }
            else
            {
                ModLogger.Instance?.Warning("[CloudSyncTab] Cannot save empty player name");
            }
        }
        
        public void LoadSettings()
        {
            ModLogger.Instance?.Info("[CloudSyncTab] Loading settings");
            
            if (_serverConfig != null && _serverConfig.Config != null)
            {
                // Set cloud sync toggle
                if (_cloudSyncToggle != null)
                {
                    _cloudSyncToggle.isOn = _serverConfig.Config.EnableCloudSync;
                }
                
                // Set player name display
                if (_cloudSyncActualName != null)
                {
                    _cloudSyncActualName.text = string.IsNullOrEmpty(_serverConfig.Config.PlayerName) 
                        ? "Not set" 
                        : _serverConfig.Config.PlayerName;
                }
                
                // Initialize input field with current name
                if (_cloudSyncNameInput != null)
                {
                    _cloudSyncNameInput.text = _serverConfig.Config.PlayerName ?? "";
                    ModLogger.Instance?.Info($"[CloudSyncTab] Initialized name input with: '{_cloudSyncNameInput.text}'");
                }
                
                // Update connection status
                UpdateConnectionStatus();
            }
        }
        
        private void UpdateActiveInactiveVisibility()
        {
            bool isEnabled = _serverConfig?.Config?.EnableCloudSync ?? false;
            
            if (_cloudSyncActive != null)
                _cloudSyncActive.SetActive(isEnabled);
                
            if (_cloudSyncInactive != null)
                _cloudSyncInactive.SetActive(!isEnabled);
            
            ModLogger.Instance?.Info($"[CloudSyncTab] Updated visibility - Active: {isEnabled}, Inactive: {!isEnabled}");
        }
        
        private void UpdateConnectionStatus()
        {
            if (_cloudSyncStatus != null && _apiService != null)
            {
                _cloudSyncStatus.isOn = _apiService.IsServerReachable;
                ModLogger.Instance?.Info($"[CloudSyncTab] Connection status: {_apiService.IsServerReachable}");
            }
        }
        
        private void TestConnection()
        {
            if (_apiService == null) return;
            
            ModLogger.Instance?.Info("[CloudSyncTab] Testing connection...");
            
            // Update button text to show testing
            if (_cloudSyncTestLabel != null)
            {
                _cloudSyncTestLabel.text = "Testing...";
            }
            
            // Perform health check
            _apiService.CheckServerHealth((bool isReachable) =>
            {
                // Callback after health check completes
                UpdateConnectionStatus();
                
                // Reset button text
                if (_cloudSyncTestLabel != null)
                {
                    _cloudSyncTestLabel.text = "Test";
                }
                
                ModLogger.Instance?.Info($"[CloudSyncTab] Health check complete. Server reachable: {isReachable}");
            });
        }
        
        public void Show()
        {
            if (_cloudSyncPage != null)
            {
                _cloudSyncPage.SetActive(true);
                
                // Update visibility when showing the page
                UpdateActiveInactiveVisibility();
                
                // Update connection status if enabled
                if (_serverConfig?.Config?.EnableCloudSync == true)
                {
                    UpdateConnectionStatus();
                }
            }
        }
        
        public void Hide()
        {
            if (_cloudSyncPage != null)
                _cloudSyncPage.SetActive(false);
        }
        
        public void Cleanup()
        {
            ModLogger.Instance?.Info("[CloudSyncTab] Cleaning up");
            
            if (_cloudSyncNameSaveButton != null)
                _cloudSyncNameSaveButton.onClick.RemoveAllListeners();
            
            if (_cloudSyncToggle != null)
                _cloudSyncToggle.onValueChanged.RemoveAllListeners();
            
            if (_cloudSyncTestButton != null)
                _cloudSyncTestButton.onClick.RemoveAllListeners();
        }
        
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
