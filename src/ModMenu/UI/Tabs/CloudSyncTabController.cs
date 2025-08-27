using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FollowMePeak.Services;
using FollowMePeak.ModMenu.UI.Helpers;

namespace FollowMePeak.ModMenu.UI.Tabs
{
    /// <summary>
    /// Controls the Cloud Sync tab UI
    /// </summary>
    public class CloudSyncTabController
    {
        // UI Elements
        private GameObject _cloudSyncPage;
        private Toggle _cloudSyncToggle;
        private TMP_InputField _cloudSyncNameInput;
        private TextMeshProUGUI _cloudSyncActualName;
        private Button _cloudSyncNameSaveButton;
        
        // Services
        private ServerConfigService _serverConfig;
        
        public GameObject CloudSyncPage => _cloudSyncPage;
        
        public void Initialize(GameObject root, ServerConfigService serverConfig)
        {
            _serverConfig = serverConfig;
            
            FindUIElements(root);
            SetupToggles();
            SetupButtons();
            LoadSettings();
        }
        
        private void FindUIElements(GameObject root)
        {
            Debug.Log("[CloudSyncTab] Finding UI elements");
            
            // Find Cloud Sync Page
            Transform pages = root.transform.Find("MyModMenuPanel/Pages");
            if (pages != null)
            {
                Transform cloudSyncPage = pages.Find("CloudSyncPage");
                if (cloudSyncPage != null) _cloudSyncPage = cloudSyncPage.gameObject;
            }
            
            if (_cloudSyncPage == null) return;
            
            // Find Active Area Elements
            Transform activeArea = _cloudSyncPage.transform.Find("CloudSyncActiveArea");
            if (activeArea != null)
            {
                _cloudSyncToggle = UIElementFinder.FindComponent<Toggle>(activeArea, "CloudSyncToggle");
            }
            
            // Find Name Area Elements
            Transform nameArea = _cloudSyncPage.transform.Find("CloudSyncNameArea");
            if (nameArea != null)
            {
                _cloudSyncActualName = UIElementFinder.FindComponent<TextMeshProUGUI>(nameArea, "CloudSyncName/CloudSyncActualName");
                _cloudSyncNameInput = UIElementFinder.FindComponent<TMP_InputField>(nameArea, "CloudSyncName/CloudSyncNameEnter");
                _cloudSyncNameSaveButton = UIElementFinder.FindComponent<Button>(nameArea, "CloudSyncNameSaveButton");
            }
        }
        
        private void SetupToggles()
        {
            Debug.Log("[CloudSyncTab] Setting up toggles");
            
            if (_cloudSyncToggle != null)
            {
                _cloudSyncToggle.onValueChanged.RemoveAllListeners();
                _cloudSyncToggle.onValueChanged.AddListener(OnCloudSyncToggled);
            }
        }
        
        private void SetupButtons()
        {
            Debug.Log("[CloudSyncTab] Setting up buttons");
            
            if (_cloudSyncNameSaveButton != null)
            {
                _cloudSyncNameSaveButton.onClick.RemoveAllListeners();
                _cloudSyncNameSaveButton.onClick.AddListener(SaveCloudSyncName);
            }
        }
        
        private void OnCloudSyncToggled(bool value)
        {
            Debug.Log($"[CloudSyncTab] Cloud Sync toggled: {value}");
            
            if (_serverConfig != null)
            {
                _serverConfig.Config.EnableCloudSync = value;
                _serverConfig.SaveConfig();
            }
        }
        
        private void SaveCloudSyncName()
        {
            if (_cloudSyncNameInput == null || _serverConfig == null) return;
            
            string newName = _cloudSyncNameInput.text;
            if (!string.IsNullOrWhiteSpace(newName))
            {
                Debug.Log($"[CloudSyncTab] Saving player name: {newName}");
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
        
        public void LoadSettings()
        {
            Debug.Log("[CloudSyncTab] Loading settings");
            
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
        
        public void Show()
        {
            if (_cloudSyncPage != null)
                _cloudSyncPage.SetActive(true);
        }
        
        public void Hide()
        {
            if (_cloudSyncPage != null)
                _cloudSyncPage.SetActive(false);
        }
        
        public void Cleanup()
        {
            Debug.Log("[CloudSyncTab] Cleaning up");
            
            if (_cloudSyncNameSaveButton != null)
                _cloudSyncNameSaveButton.onClick.RemoveAllListeners();
            
            if (_cloudSyncToggle != null)
                _cloudSyncToggle.onValueChanged.RemoveAllListeners();
        }
    }
}