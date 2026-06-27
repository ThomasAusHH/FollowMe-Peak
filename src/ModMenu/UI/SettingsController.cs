using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BepInEx.Configuration;
using FollowMePeak.ModMenu.UI.Helpers;
using FollowMePeak.Utils;

namespace FollowMePeak.ModMenu.UI
{
    public class SettingsController
    {
        // UI Element References
        private GameObject _settingsButton;
        private GameObject _settingsMenuPanel;
        private GameObject _pressAnyKeyText;
        private Button _applyButton;
        private Button _resetButton;
        private Button _closeButton;
        private Toggle _saveDeathClimbToggle;
        
        // Key Bindings
        private KeyBinding _menuToggleBinding;
        private KeyBinding _routesToggleBinding;
        
        private class KeyBinding
        {
            public string Name;
            public ConfigEntry<KeyCode> Config;
            public TMP_Text DisplayText;
            public Button RecordButton;
            public KeyCode OriginalKey;
            public KeyCode PendingKey;
            public bool IsRecording;
        }
        
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
                _settingsMenuPanel.SetActive(false);
                ModLogger.Instance?.Info("[SettingsController] Settings panel initially hidden");
            }
            else
            {
                ModLogger.Instance?.Error("[SettingsController] SettingsMenuPanel not found!");
            }
            
            // Initialize Menu Toggle Binding
            _menuToggleBinding = new KeyBinding
            {
                Name = "MenuToggle",
                Config = Plugin.ModMenuToggleKey
            };
            
            // Find Actual Toggle Text for menu
            _menuToggleBinding.DisplayText = UIElementFinder.FindComponent<TMP_Text>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuActualToggle");
            if (_menuToggleBinding.DisplayText == null && _settingsMenuPanel != null)
            {
                var actualToggle = _settingsMenuPanel.transform.Find("SettingsMenuActualToggle");
                if (actualToggle != null)
                {
                    _menuToggleBinding.DisplayText = actualToggle.GetComponent<TMP_Text>();
                }
            }
            
            if (_menuToggleBinding.DisplayText != null)
            {
                ModLogger.Instance?.Info("[SettingsController] Found SettingsMenuActualToggle text component");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] SettingsMenuActualToggle text not found");
            }
            
            // Find Record Button for menu
            var menuRecordButton = UIElementFinder.FindComponent<Button>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuRecordToggleButton");
            if (menuRecordButton == null && _settingsMenuPanel != null)
            {
                menuRecordButton = _settingsMenuPanel.transform.Find("SettingsMenuRecordToggleButton")?.GetComponent<Button>();
            }
            
            if (menuRecordButton != null)
            {
                _menuToggleBinding.RecordButton = menuRecordButton;
                _menuToggleBinding.RecordButton.onClick.RemoveAllListeners();
                _menuToggleBinding.RecordButton.onClick.AddListener(() => StartKeyRecording(_menuToggleBinding));
                ModLogger.Instance?.Info("[SettingsController] Menu record button found and listener added");
            }
            else
            {
                ModLogger.Instance?.Error("[SettingsController] SettingsMenuRecordToggleButton not found!");
            }
            
            // Initialize Routes Visibility Toggle Binding
            _routesToggleBinding = new KeyBinding
            {
                Name = "RoutesVisibility",
                Config = Plugin.ToggleRoutesVisibilityKey
            };
            
            // Find Actual Toggle Text for routes visibility
            _routesToggleBinding.DisplayText = UIElementFinder.FindComponent<TMP_Text>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuActualToggleLastSelected");
            if (_routesToggleBinding.DisplayText == null && _settingsMenuPanel != null)
            {
                var visibilityToggle = _settingsMenuPanel.transform.Find("SettingsMenuActualToggleLastSelected");
                if (visibilityToggle != null)
                {
                    _routesToggleBinding.DisplayText = visibilityToggle.GetComponent<TMP_Text>();
                }
            }
            
            // Fallback to old name just in case
            if (_routesToggleBinding.DisplayText == null)
            {
                _routesToggleBinding.DisplayText = UIElementFinder.FindComponent<TMP_Text>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuActualVisibilityToggle");
                if (_routesToggleBinding.DisplayText == null && _settingsMenuPanel != null)
                {
                    var visibilityToggle = _settingsMenuPanel.transform.Find("SettingsMenuActualVisibilityToggle");
                    if (visibilityToggle != null)
                    {
                        _routesToggleBinding.DisplayText = visibilityToggle.GetComponent<TMP_Text>();
                    }
                }
            }
            
            if (_routesToggleBinding.DisplayText != null)
            {
                ModLogger.Instance?.Info("[SettingsController] Found SettingsMenuActualToggleLastSelected text component");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] SettingsMenuActualToggleLastSelected text not found");
            }
            
            // Find Record Button for routes visibility
            var routesRecordButton = UIElementFinder.FindComponent<Button>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuRecordToggleButtonLastSelected");
            if (routesRecordButton == null && _settingsMenuPanel != null)
            {
                routesRecordButton = _settingsMenuPanel.transform.Find("SettingsMenuRecordToggleButtonLastSelected")?.GetComponent<Button>();
            }
            
            // Fallback to old name
            if (routesRecordButton == null)
            {
                routesRecordButton = UIElementFinder.FindComponent<Button>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuRecordVisibilityButton");
                if (routesRecordButton == null && _settingsMenuPanel != null)
                {
                    routesRecordButton = _settingsMenuPanel.transform.Find("SettingsMenuRecordVisibilityButton")?.GetComponent<Button>();
                }
            }
            
            if (routesRecordButton != null)
            {
                _routesToggleBinding.RecordButton = routesRecordButton;
                _routesToggleBinding.RecordButton.onClick.RemoveAllListeners();
                _routesToggleBinding.RecordButton.onClick.AddListener(() => StartKeyRecording(_routesToggleBinding));
                ModLogger.Instance?.Info("[SettingsController] Routes visibility record button found and listener added");
            }
            else
            {
                ModLogger.Instance?.Warning("[SettingsController] SettingsMenuRecordToggleButtonLastSelected not found");
            }
            
            // Find Press Any Key Text with correct path
            _pressAnyKeyText = UIElementFinder.FindGameObject(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuPressAnyKey");
            if (_pressAnyKeyText == null && _settingsMenuPanel != null)
            {
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
                _saveDeathClimbToggle = UIElementFinder.FindComponent<Toggle>(transform, "MyModMenuPanel/SettingsMenuPanel/SettingsMenuDaveDeathClimbToggle");
            }
            if (_saveDeathClimbToggle == null && _settingsMenuPanel != null)
            {
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
            
            // Initialize current key displays
            ResetBindingToCurrent(_menuToggleBinding);
            ResetBindingToCurrent(_routesToggleBinding);
            
            ModLogger.Instance?.Info($"[SettingsController] Initialization complete - MenuKey: {_menuToggleBinding.Config.Value}, RoutesKey: {_routesToggleBinding.Config?.Value}, Elements found: Button={_settingsButton != null}, Panel={_settingsMenuPanel != null}");
        }
        
        private void ResetBindingToCurrent(KeyBinding binding)
        {
            if (binding?.Config == null) return;
            binding.OriginalKey = binding.Config.Value;
            binding.PendingKey = binding.OriginalKey;
            binding.IsRecording = false;
            UpdateKeyDisplay(binding);
        }
        
        private KeyBinding GetRecordingBinding()
        {
            if (_menuToggleBinding != null && _menuToggleBinding.IsRecording) return _menuToggleBinding;
            if (_routesToggleBinding != null && _routesToggleBinding.IsRecording) return _routesToggleBinding;
            return null;
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
                    ResetBindingToCurrent(_menuToggleBinding);
                    ResetBindingToCurrent(_routesToggleBinding);
                }
            }
        }
        
        private void StartKeyRecording(KeyBinding target)
        {
            if (target == null || target.Config == null) return;
            
            // Cancel any other active recording first
            var currentlyRecording = GetRecordingBinding();
            if (currentlyRecording != null && currentlyRecording != target)
            {
                StopKeyRecording(currentlyRecording, null);
            }
            
            ModLogger.Instance?.Info($"[SettingsController] Starting key recording for {target.Name}...");
            target.IsRecording = true;
            
            if (_pressAnyKeyText != null)
                _pressAnyKeyText.SetActive(true);
            
            if (target.RecordButton != null)
                target.RecordButton.interactable = false;
            
            // Disable control buttons during recording
            SetControlButtonsInteractable(false);
        }
        
        private void StopKeyRecording(KeyBinding target, KeyCode? newKey)
        {
            if (target == null) return;
            
            ModLogger.Instance?.Info($"[SettingsController] Stopping key recording for {target.Name}. New key: {newKey}");
            target.IsRecording = false;
            
            if (_pressAnyKeyText != null && GetRecordingBinding() == null)
                _pressAnyKeyText.SetActive(false);
            
            if (target.RecordButton != null)
                target.RecordButton.interactable = true;
            
            // Re-enable control buttons if nothing is recording
            if (GetRecordingBinding() == null)
            {
                SetControlButtonsInteractable(true);
            }
            
            if (newKey.HasValue)
            {
                target.PendingKey = newKey.Value;
                UpdateKeyDisplay(target);
                ModLogger.Instance?.Info($"[SettingsController] Recorded new key for {target.Name}: {target.PendingKey}");
            }
            else
            {
                ModLogger.Instance?.Info($"[SettingsController] Key recording cancelled for {target.Name}");
            }
        }
        
        private void SetControlButtonsInteractable(bool interactable)
        {
            if (_applyButton != null)
                _applyButton.interactable = interactable;
            if (_resetButton != null)
                _resetButton.interactable = interactable;
            if (_closeButton != null)
                _closeButton.interactable = interactable;
        }
        
        public void Update()
        {
            var recording = GetRecordingBinding();
            if (recording == null) return;
            
            // Check for ESC to cancel
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopKeyRecording(recording, null);
                return;
            }
            
            // Check all KeyCodes
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (InvalidKeys.Contains(key)) continue;
                if (key.ToString().StartsWith("JoystickButton")) continue;
                
                if (Input.GetKeyDown(key))
                {
                    StopKeyRecording(recording, key);
                    break;
                }
            }
        }
        
        private void OnApplyClick()
        {
            ModLogger.Instance?.Info("[SettingsController] Apply clicked");
            
            ApplyBindingIfChanged(_menuToggleBinding);
            ApplyBindingIfChanged(_routesToggleBinding);
            
            Plugin.Instance.Config.Save();
            
            if (_settingsMenuPanel != null)
                _settingsMenuPanel.SetActive(false);
        }
        
        private void ApplyBindingIfChanged(KeyBinding binding)
        {
            if (binding?.Config == null) return;
            if (binding.PendingKey != binding.Config.Value)
            {
                binding.Config.Value = binding.PendingKey;
                binding.OriginalKey = binding.PendingKey;
                ModLogger.Instance?.Info($"[SettingsController] Applied new {binding.Name} key: {binding.PendingKey}");
            }
        }
        
        private void OnResetClick()
        {
            ModLogger.Instance?.Info("[SettingsController] Reset clicked");
            
            if (_menuToggleBinding != null)
            {
                _menuToggleBinding.PendingKey = KeyCode.F1;
                UpdateKeyDisplay(_menuToggleBinding);
            }
            if (_routesToggleBinding != null)
            {
                _routesToggleBinding.PendingKey = KeyCode.F2;
                UpdateKeyDisplay(_routesToggleBinding);
            }
        }
        
        private void OnCloseClick()
        {
            ModLogger.Instance?.Info("[SettingsController] Close clicked");
            
            RevertBindingIfNeeded(_menuToggleBinding);
            RevertBindingIfNeeded(_routesToggleBinding);
            
            if (_settingsMenuPanel != null)
                _settingsMenuPanel.SetActive(false);
        }
        
        private void RevertBindingIfNeeded(KeyBinding binding)
        {
            if (binding?.Config == null) return;
            if (binding.PendingKey != binding.OriginalKey)
            {
                binding.PendingKey = binding.OriginalKey;
                UpdateKeyDisplay(binding);
            }
        }
        
        private void UpdateKeyDisplay(KeyBinding binding)
        {
            if (binding?.DisplayText == null) return;
            
            string displayName = FormatKeyName(binding.PendingKey);
            binding.DisplayText.text = displayName;
            ModLogger.Instance?.Info($"[SettingsController] Updated {binding.Name} key display to: {displayName}");
        }
        
        private string FormatKeyName(KeyCode key)
        {
            string name = key.ToString();
            
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
