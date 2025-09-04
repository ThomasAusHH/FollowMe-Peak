using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FollowMePeak.ModMenu.UI.Helpers;
using FollowMePeak.Utils;

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
        private Toggle _saveDeathClimbToggle;
        
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
                ModLogger.Instance?.Error("[SettingsController] menuRoot is null!");
                return;
            }
            
            var transform = menuRoot.transform;
            ModLogger.Instance?.Info("[SettingsController] Starting initialization...");
            
            // Find Settings Button with correct path
            ModLogger.Instance?.Info("[SettingsController] Searching for SettingsButton at path: MyModMenuPanel/SettingsButton");
            _settingsButton = UIElementFinder.FindGameObject(transform, "MyModMenuPanel/SettingsButton");
            
            // Fallback: Try recursive search if direct path fails
            if (_settingsButton == null)
            {
                ModLogger.Instance?.Info("[SettingsController] Direct path failed, trying recursive search for SettingsButton");
                var settingsButtonTransform = UIElementFinder.FindChildRecursive(transform, "SettingsButton");
                _settingsButton = settingsButtonTransform?.gameObject;
            }
            if (_settingsButton != null)
            {
                ModLogger.Instance?.Info($"[SettingsController] Found SettingsButton at: {UIElementFinder.GetTransformPath(_settingsButton.transform)}");
                var button = _settingsButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(OnSettingsButtonClick);
                    ModLogger.Instance?.Info("[SettingsController] Settings button listener added successfully");
                }
            }
            else
            {
                ModLogger.Instance?.Error("[SettingsController] SettingsButton not found!");
            }
            
            // Find Settings Panel with correct path
            ModLogger.Instance?.Info("[SettingsController] Searching for SettingsMenuPanel at path: MyModMenuPanel/SettingsMenuPanel");
            _settingsMenuPanel = UIElementFinder.FindGameObject(transform, "MyModMenuPanel/SettingsMenuPanel");
            
            if (_settingsMenuPanel == null)
            {
                ModLogger.Instance?.Info("[SettingsController] Direct path failed, trying recursive search for SettingsMenuPanel");
                var panelTransform = UIElementFinder.FindChildRecursive(transform, "SettingsMenuPanel");
                _settingsMenuPanel = panelTransform?.gameObject;
            }
            
            if (_settingsMenuPanel != null)
            {
                ModLogger.Instance?.Info($"[SettingsController] Found SettingsMenuPanel at: {UIElementFinder.GetTransformPath(_settingsMenuPanel.transform)}");
                // IMPORTANT: Hide panel initially
                _settingsMenuPanel.SetActive(false);
                ModLogger.Instance?.Info("[SettingsController] Settings panel initially hidden");
            }
            else
            {
                ModLogger.Instance?.Error("[SettingsController] SettingsMenuPanel not found!");
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
                ModLogger.Instance?.Info("[SettingsController] Found SettingsMenuActualToggle text component");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] SettingsMenuActualToggle text not found");
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
                ModLogger.Instance?.Info("[SettingsController] Record button found and listener added");
            }
            else
            {
                ModLogger.Instance?.Error("[SettingsController] SettingsMenuRecordToggleButton not found!");
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
                ModLogger.Instance?.Info("[SettingsController] Press Any Key text found and hidden");
            }
            else
            {
                ModLogger.Instance?.Error("[SettingsController] SettingsMenuPressAnyKey not found!");
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
                ModLogger.Instance?.Info("[SettingsController] Apply button found and listener added");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] Apply button not found");
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
                ModLogger.Instance?.Info("[SettingsController] Reset button found and listener added");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] Reset button not found");
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
                ModLogger.Instance?.Info("[SettingsController] Close button found and listener added");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] Close button not found");
            }
            
            // Find Save Death Climb Toggle - try both names (in case of typo)
            _saveDeathClimbToggle = UIElementFinder.FindComponent<Toggle>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuSaveDeathClimbToggle");
            if (_saveDeathClimbToggle == null)
            {
                // Try alternative name in case of typo in UI
                _saveDeathClimbToggle = UIElementFinder.FindComponent<Toggle>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuDaveDeathClimbToggle");
            }
            if (_saveDeathClimbToggle == null && _settingsMenuPanel != null)
            {
                // Try relative to panel
                var toggle = _settingsMenuPanel.transform.Find("SettingsMenuSaveDeathClimbToggle");
                if (toggle == null)
                {
                    toggle = _settingsMenuPanel.transform.Find("SettingsMenuDaveDeathClimbToggle");
                }
                if (toggle != null)
                {
                    _saveDeathClimbToggle = toggle.GetComponent<Toggle>();
                }
            }
            
            if (_saveDeathClimbToggle != null)
            {
                _saveDeathClimbToggle.isOn = Plugin.SaveDeathClimbs.Value;
                _saveDeathClimbToggle.onValueChanged.RemoveAllListeners();
                _saveDeathClimbToggle.onValueChanged.AddListener(OnSaveDeathClimbsChanged);
                ModLogger.Instance?.Info($"[SettingsController] Save Death Climb toggle found and initialized - Value: {Plugin.SaveDeathClimbs.Value}");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] Save Death Climb toggle not found");
            }
            
            // Initialize current key display
            _originalKey = Plugin.ModMenuToggleKey.Value;
            _pendingKey = _originalKey;
            UpdateKeyDisplay(_originalKey);
            
            ModLogger.Instance?.Info($"[SettingsController] Initialization complete - Current key: {_originalKey}, Elements found: Button={_settingsButton != null}, Panel={_settingsMenuPanel != null}, Record={_recordToggleButton != null}");
        }
        
        private void OnSettingsButtonClick()
        {
            ModLogger.Instance?.Info("[SettingsController] Settings button clicked");
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
            ModLogger.Instance?.Info("[SettingsController] Starting key recording...");
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
            ModLogger.Instance?.Info($"[SettingsController] Stopping key recording. New key: {newKey}");
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
                ModLogger.Instance?.Info($"[SettingsController] Recorded new key: {_pendingKey}");
            }
            else
            {
                ModLogger.Instance?.Info("[SettingsController] Key recording cancelled");
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
            ModLogger.Instance?.Info($"[SettingsController] Apply clicked. Pending: {_pendingKey}, Current: {Plugin.ModMenuToggleKey.Value}");
            
            if (_pendingKey != Plugin.ModMenuToggleKey.Value)
            {
                Plugin.ModMenuToggleKey.Value = _pendingKey;
                Plugin.Instance.Config.Save();
                _originalKey = _pendingKey;
                
                ModLogger.Instance?.Info($"[SettingsController] Applied new toggle key: {_pendingKey}");
            }
            
            if (_settingsMenuPanel != null)
                _settingsMenuPanel.SetActive(false);
        }
        
        private void OnResetClick()
        {
            ModLogger.Instance?.Info("[SettingsController] Reset clicked");
            _pendingKey = KeyCode.F1;
            UpdateKeyDisplay(_pendingKey);
        }
        
        private void OnCloseClick()
        {
            ModLogger.Instance?.Info("[SettingsController] Close clicked");
            
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
                ModLogger.Instance?.Info($"[SettingsController] Updated key display to: {displayName}");
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
        
        private void OnSaveDeathClimbsChanged(bool value)
        {
            Plugin.SaveDeathClimbs.Value = value;
            Plugin.Instance.Config.Save();
            ModLogger.Instance?.Info($"[SettingsController] Save Death Climbs toggled: {value}");
        }
    }
}
