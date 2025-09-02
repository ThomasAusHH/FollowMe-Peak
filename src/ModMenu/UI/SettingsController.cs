using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FollowMePeak.ModMenu.UI.Helpers;
using System.Collections.Generic;

namespace FollowMePeak.ModMenu.UI
{
    public class SettingsController
    {
        // UI Element References
        private GameObject _settingsButton;
        private GameObject _settingsMenuPanel;
        private TMP_Text _actualToggleText;
        private Button _recordToggleButton;
        private GameObject _pressAnyKeyText;
        private Button _applyButton;
        private Button _resetButton;
        private Button _closeButton;
        
        // State
        private bool _isRecording = false;
        private KeyCode _pendingKey = KeyCode.None;
        private KeyCode _originalKey;
        
        // Keys to exclude from recording
        private static readonly HashSet<KeyCode> InvalidKeys = new HashSet<KeyCode>
        {
            KeyCode.None,
            KeyCode.Escape,
            KeyCode.Mouse0,
            KeyCode.Mouse1,
            KeyCode.Mouse2,
            KeyCode.Mouse3,
            KeyCode.Mouse4,
            KeyCode.Mouse5,
            KeyCode.Mouse6
        };
        
        public void Initialize(GameObject menuRoot)
        {
            if (menuRoot == null)
            {
                Debug.LogError("[SettingsController] menuRoot is null!");
                return;
            }
            
            var transform = menuRoot.transform;
            Debug.Log("[SettingsController] Starting initialization...");
            
            // Find Settings Button with correct path
            Debug.Log("[SettingsController] Searching for SettingsButton at path: MyModMenuPanel/SettingsButton");
            _settingsButton = UIElementFinder.FindGameObject(transform, "MyModMenuPanel/SettingsButton");
            
            // Fallback: Try recursive search if direct path fails
            if (_settingsButton == null)
            {
                Debug.Log("[SettingsController] Direct path failed, trying recursive search for SettingsButton");
                var settingsButtonTransform = UIElementFinder.FindChildRecursive(transform, "SettingsButton");
                _settingsButton = settingsButtonTransform?.gameObject;
            }
            if (_settingsButton != null)
            {
                Debug.Log($"[SettingsController] Found SettingsButton at: {UIElementFinder.GetTransformPath(_settingsButton.transform)}");
                var button = _settingsButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(OnSettingsButtonClick);
                    Debug.Log("[SettingsController] Settings button listener added successfully");
                }
            }
            else
            {
                Debug.LogError("[SettingsController] SettingsButton not found!");
            }
            
            // Find Settings Panel with correct path
            Debug.Log("[SettingsController] Searching for SettingsMenuPanel at path: MyModMenuPanel/SettingsMenuPanel");
            _settingsMenuPanel = UIElementFinder.FindGameObject(transform, "MyModMenuPanel/SettingsMenuPanel");
            
            if (_settingsMenuPanel == null)
            {
                Debug.Log("[SettingsController] Direct path failed, trying recursive search for SettingsMenuPanel");
                var panelTransform = UIElementFinder.FindChildRecursive(transform, "SettingsMenuPanel");
                _settingsMenuPanel = panelTransform?.gameObject;
            }
            
            if (_settingsMenuPanel != null)
            {
                Debug.Log($"[SettingsController] Found SettingsMenuPanel at: {UIElementFinder.GetTransformPath(_settingsMenuPanel.transform)}");
                // IMPORTANT: Hide panel initially
                _settingsMenuPanel.SetActive(false);
                Debug.Log("[SettingsController] Settings panel initially hidden");
            }
            else
            {
                Debug.LogError("[SettingsController] SettingsMenuPanel not found!");
            }
            
            // Find Actual Toggle Text with correct path
            _actualToggleText = UIElementFinder.FindComponent<TMP_Text>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuActualToggle");
            if (_actualToggleText == null && _settingsMenuPanel != null)
            {
                // Try relative to panel
                var actualToggle = _settingsMenuPanel.transform.Find("SettingsMenuActualToggle");
                if (actualToggle != null)
                {
                    _actualToggleText = actualToggle.GetComponent<TMP_Text>();
                }
            }
            
            if (_actualToggleText != null)
            {
                Debug.Log("[SettingsController] Found SettingsMenuActualToggle text component");
            }
            else
            {
                Debug.LogWarning("[SettingsController] SettingsMenuActualToggle text not found");
            }
            
            // Find Record Button with correct path
            var recordButton = UIElementFinder.FindComponent<Button>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuRecordToggleButton");
            if (recordButton == null && _settingsMenuPanel != null)
            {
                // Try relative to panel
                recordButton = _settingsMenuPanel.transform.Find("SettingsMenuRecordToggleButton")?.GetComponent<Button>();
            }
            
            if (recordButton != null)
            {
                _recordToggleButton = recordButton;
                _recordToggleButton.onClick.RemoveAllListeners();
                _recordToggleButton.onClick.AddListener(StartKeyRecording);
                Debug.Log("[SettingsController] Record button found and listener added");
            }
            else
            {
                Debug.LogError("[SettingsController] SettingsMenuRecordToggleButton not found!");
            }
            
            // Find Press Any Key Text with correct path
            _pressAnyKeyText = UIElementFinder.FindGameObject(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuPressAnyKey");
            if (_pressAnyKeyText == null && _settingsMenuPanel != null)
            {
                // Try relative to panel
                var pressAnyKeyTransform = _settingsMenuPanel.transform.Find("SettingsMenuPressAnyKey");
                _pressAnyKeyText = pressAnyKeyTransform?.gameObject;
            }
            
            if (_pressAnyKeyText != null)
            {
                _pressAnyKeyText.SetActive(false);
                Debug.Log("[SettingsController] Press Any Key text found and hidden");
            }
            else
            {
                Debug.LogError("[SettingsController] SettingsMenuPressAnyKey not found!");
            }
            
            // Find Control Buttons with correct paths
            _applyButton = UIElementFinder.FindComponent<Button>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuButtonArea/SettingsMenuApplyButton");
            if (_applyButton == null && _settingsMenuPanel != null)
            {
                var buttonArea = _settingsMenuPanel.transform.Find("SettingsMenuButtonArea");
                if (buttonArea != null)
                {
                    _applyButton = buttonArea.Find("SettingsMenuApplyButton")?.GetComponent<Button>();
                }
            }
            
            if (_applyButton != null)
            {
                _applyButton.onClick.RemoveAllListeners();
                _applyButton.onClick.AddListener(OnApplyClick);
                Debug.Log("[SettingsController] Apply button found and listener added");
            }
            else
            {
                Debug.LogWarning("[SettingsController] Apply button not found");
            }
            
            _resetButton = UIElementFinder.FindComponent<Button>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuButtonArea/SettingsMenuResetButton");
            if (_resetButton == null && _settingsMenuPanel != null)
            {
                var buttonArea = _settingsMenuPanel.transform.Find("SettingsMenuButtonArea");
                if (buttonArea != null)
                {
                    _resetButton = buttonArea.Find("SettingsMenuResetButton")?.GetComponent<Button>();
                }
            }
            
            if (_resetButton != null)
            {
                _resetButton.onClick.RemoveAllListeners();
                _resetButton.onClick.AddListener(OnResetClick);
                Debug.Log("[SettingsController] Reset button found and listener added");
            }
            else
            {
                Debug.LogWarning("[SettingsController] Reset button not found");
            }
            
            _closeButton = UIElementFinder.FindComponent<Button>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuButtonArea/SettingsMenuCloseButton");
            if (_closeButton == null && _settingsMenuPanel != null)
            {
                var buttonArea = _settingsMenuPanel.transform.Find("SettingsMenuButtonArea");
                if (buttonArea != null)
                {
                    _closeButton = buttonArea.Find("SettingsMenuCloseButton")?.GetComponent<Button>();
                }
            }
            
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(OnCloseClick);
                Debug.Log("[SettingsController] Close button found and listener added");
            }
            else
            {
                Debug.LogWarning("[SettingsController] Close button not found");
            }
            
            // Initialize current key display
            _originalKey = Plugin.ModMenuToggleKey.Value;
            _pendingKey = _originalKey;
            UpdateKeyDisplay(_originalKey);
            
            Debug.Log($"[SettingsController] Initialization complete - Current key: {_originalKey}, Elements found: Button={_settingsButton != null}, Panel={_settingsMenuPanel != null}, Record={_recordToggleButton != null}");
        }
        
        private void OnSettingsButtonClick()
        {
            Debug.Log("[SettingsController] Settings button clicked");
            if (_settingsMenuPanel != null)
            {
                bool isActive = _settingsMenuPanel.activeSelf;
                _settingsMenuPanel.SetActive(!isActive);
                
                if (!isActive)
                {
                    // Reset to current saved key when opening
                    _originalKey = Plugin.ModMenuToggleKey.Value;
                    _pendingKey = _originalKey;
                    UpdateKeyDisplay(_originalKey);
                }
            }
        }
        
        private void StartKeyRecording()
        {
            Debug.Log("[SettingsController] Starting key recording...");
            _isRecording = true;
            
            if (_pressAnyKeyText != null)
                _pressAnyKeyText.SetActive(true);
            
            if (_recordToggleButton != null)
                _recordToggleButton.interactable = false;
            
            // Disable other UI elements during recording
            if (_applyButton != null)
                _applyButton.interactable = false;
            if (_resetButton != null)
                _resetButton.interactable = false;
            if (_closeButton != null)
                _closeButton.interactable = false;
        }
        
        private void StopKeyRecording(KeyCode? newKey = null)
        {
            Debug.Log($"[SettingsController] Stopping key recording. New key: {newKey}");
            _isRecording = false;
            
            if (_pressAnyKeyText != null)
                _pressAnyKeyText.SetActive(false);
            
            if (_recordToggleButton != null)
                _recordToggleButton.interactable = true;
            
            // Re-enable UI elements
            if (_applyButton != null)
                _applyButton.interactable = true;
            if (_resetButton != null)
                _resetButton.interactable = true;
            if (_closeButton != null)
                _closeButton.interactable = true;
            
            if (newKey.HasValue)
            {
                _pendingKey = newKey.Value;
                UpdateKeyDisplay(_pendingKey);
                Debug.Log($"[SettingsController] Recorded new key: {_pendingKey}");
            }
            else
            {
                Debug.Log("[SettingsController] Key recording cancelled");
            }
        }
        
        public void Update()
        {
            if (!_isRecording) return;
            
            // Check for ESC to cancel
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopKeyRecording(null);
                return;
            }
            
            // Check all KeyCodes
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                // Skip invalid keys
                if (InvalidKeys.Contains(key)) continue;
                
                // Skip joystick buttons for now (no gamepad support yet)
                if (key.ToString().StartsWith("JoystickButton")) continue;
                
                if (Input.GetKeyDown(key))
                {
                    StopKeyRecording(key);
                    break;
                }
            }
        }
        
        private void OnApplyClick()
        {
            Debug.Log($"[SettingsController] Apply clicked. Pending: {_pendingKey}, Current: {Plugin.ModMenuToggleKey.Value}");
            
            if (_pendingKey != Plugin.ModMenuToggleKey.Value)
            {
                Plugin.ModMenuToggleKey.Value = _pendingKey;
                Plugin.Instance.Config.Save();
                _originalKey = _pendingKey;
                
                Debug.Log($"[SettingsController] Applied new toggle key: {_pendingKey}");
            }
            
            if (_settingsMenuPanel != null)
                _settingsMenuPanel.SetActive(false);
        }
        
        private void OnResetClick()
        {
            Debug.Log("[SettingsController] Reset clicked");
            _pendingKey = KeyCode.F1;
            UpdateKeyDisplay(_pendingKey);
        }
        
        private void OnCloseClick()
        {
            Debug.Log("[SettingsController] Close clicked");
            
            // Revert to original if not applied
            if (_pendingKey != _originalKey)
            {
                _pendingKey = _originalKey;
                UpdateKeyDisplay(_originalKey);
            }
            
            if (_settingsMenuPanel != null)
                _settingsMenuPanel.SetActive(false);
        }
        
        private void UpdateKeyDisplay(KeyCode key)
        {
            if (_actualToggleText != null)
            {
                string displayName = FormatKeyName(key);
                _actualToggleText.text = displayName;
                Debug.Log($"[SettingsController] Updated key display to: {displayName}");
            }
        }
        
        private string FormatKeyName(KeyCode key)
        {
            string name = key.ToString();
            
            // Special formatting for better readability
            if (name.StartsWith("Alpha"))
                return name.Replace("Alpha", "");
            if (name.StartsWith("Keypad"))
                return "Num " + name.Replace("Keypad", "");
            if (name == "BackQuote")
                return "`";
            if (name == "LeftBracket")
                return "[";
            if (name == "RightBracket")
                return "]";
            if (name == "Semicolon")
                return ";";
            if (name == "Quote")
                return "'";
            if (name == "Backslash")
                return "\\";
            if (name == "Comma")
                return ",";
            if (name == "Period")
                return ".";
            if (name == "Slash")
                return "/";
            if (name == "Minus")
                return "-";
            if (name == "Equals")
                return "=";
            if (name == "LeftShift" || name == "RightShift")
                return "Shift";
            if (name == "LeftControl" || name == "RightControl")
                return "Ctrl";
            if (name == "LeftAlt" || name == "RightAlt")
                return "Alt";
            if (name == "Space")
                return "Space";
            if (name == "Return")
                return "Enter";
            if (name == "Tab")
                return "Tab";
            if (name == "CapsLock")
                return "Caps Lock";
            
            return name;
        }
    }
}