using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FollowMePeak.Services;
using TMPro;
using FollowMePeak.Utils;

namespace FollowMePeak.ModMenu
{
    public enum MenuPosition
    {
        RightEdge,
        LeftEdge,
        Center,
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft
    }
    
    public class ModMenuManager
    {
        // AssetBundle UI elements
        private GameObject _assetBundleMenuInstance;
        private bool _assetBundleLoaded = false;
        private ModMenuUIController _uiController;
        private UI.SettingsController _settingsController;
        
        // Update Message
        private bool _hasCheckedForUpdate = false;
        private Models.UpdateMessage _lastUpdateMessage = null;
        
        // Services (to be set from Plugin)
        public static Services.ServerConfigService ServerConfig { get; set; }
        public static Services.VPSApiService ApiService { get; set; }
        public static Services.ClimbUploadService UploadService { get; set; }
        public static Services.ClimbDownloadService DownloadService { get; set; }
        public static Services.ClimbDataService ClimbDataService { get; set; }
        public static Managers.ClimbVisualizationManager VisualizationManager { get; set; }
        
        public ModMenuManager()
        {
            ModLogger.Instance?.Info("[ModMenu] Manager initialized for AssetBundle menu");
        }
        
        public void OnAssetBundleLoaded()
        {
            ModLogger.Instance?.Info($"[ModMenu] OnAssetBundleLoaded called - Previous state: {_assetBundleLoaded}");
            _assetBundleLoaded = true;
            ModLogger.Instance?.Info($"[ModMenu] AssetBundle loaded flag set to: {_assetBundleLoaded}");
            ModLogger.Instance?.Info($"[ModMenu] AssetBundleService.IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            CreateAssetBundleMenu();
        }
        
        private void CreateAssetBundleMenu()
        {
            ModLogger.Instance?.Info("[ModMenu] CreateAssetBundleMenu called");
            ModLogger.Instance?.Info($"[ModMenu]   - _assetBundleLoaded: {_assetBundleLoaded}");
            ModLogger.Instance?.Info($"[ModMenu]   - AssetBundleService.IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            
            if (!_assetBundleLoaded || !AssetBundleService.Instance.IsLoaded)
            {
                ModLogger.Instance?.Error($"[ModMenu] Cannot create AssetBundle menu - bundle not loaded (loaded:{_assetBundleLoaded}, service:{AssetBundleService.Instance?.IsLoaded ?? false})");
                return;
            }
            
            // Check if menu already exists
            if (_assetBundleMenuInstance != null)
            {
                ModLogger.Instance?.Warning("[ModMenu] Menu instance already exists - skipping creation");
                return;
            }
            
            try
            {
                ModLogger.Instance?.Info("[ModMenu] Attempting to get prefabs from AssetBundle...");
                
                // Try to get the main menu prefab from the AssetBundle
                GameObject menuPrefab = AssetBundleService.Instance.GetPrefab(AssetBundleService.MOD_MENU_MAIN_PREFAB);
                ModLogger.Instance?.Info($"[ModMenu] Tried {AssetBundleService.MOD_MENU_MAIN_PREFAB}: {menuPrefab != null}");
                
                // If specific prefab not found, try alternatives
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab(AssetBundleService.MOD_MENU_CANVAS_PREFAB);
                    ModLogger.Instance?.Info($"[ModMenu] Tried {AssetBundleService.MOD_MENU_CANVAS_PREFAB}: {menuPrefab != null}");
                }
                
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab(AssetBundleService.MOD_MENU_PANEL_PREFAB);
                    ModLogger.Instance?.Info($"[ModMenu] Tried {AssetBundleService.MOD_MENU_PANEL_PREFAB}: {menuPrefab != null}");
                }
                
                // If still no prefab, try generic names
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab("ModMenu");
                    ModLogger.Instance?.Info($"[ModMenu] Tried 'ModMenu': {menuPrefab != null}");
                }
                
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab("Canvas");
                    ModLogger.Instance?.Info($"[ModMenu] Tried 'Canvas': {menuPrefab != null}");
                }
                
                if (menuPrefab == null)
                {
                    ModLogger.Instance?.Error("[ModMenu] No suitable menu prefab found in AssetBundle");
                    return;
                }
                
                // Instantiate the menu from prefab
                _assetBundleMenuInstance = Object.Instantiate(menuPrefab);
                Object.DontDestroyOnLoad(_assetBundleMenuInstance);
                // WICHTIG: Menu initial deaktiviert lassen
                _assetBundleMenuInstance.SetActive(false);
                
                ModLogger.Instance?.Info($"[ModMenu] AssetBundle menu created: {_assetBundleMenuInstance.name}");
                
                // Set up any required components or references
                SetupAssetBundleMenuComponents();
                
                // Initialize UI Controller
                InitializeUIController();
                
                // WICHTIG: Stelle sicher, dass das Menu definitiv deaktiviert ist
                _assetBundleMenuInstance.SetActive(false);
                ModLogger.Instance?.Info("[ModMenu] Menu instance forcefully deactivated after setup");
            }
            catch (System.Exception e)
            {
                ModLogger.Instance?.Error($"[ModMenu] Failed to create AssetBundle menu: {e.Message}\n{e.StackTrace}");
            }
        }
        
        private void SetupAssetBundleMenuComponents()
        {
            if (_assetBundleMenuInstance == null) return;
            
            ModLogger.Instance?.Info("[ModMenu] Setting up AssetBundle menu components...");
            
            // Log all components in the instantiated prefab
            ModLogger.Instance?.Info($"[ModMenu] Root GameObject: {_assetBundleMenuInstance.name}");
            ModLogger.Instance?.Info($"[ModMenu] Root components:");
            foreach (var comp in _assetBundleMenuInstance.GetComponents<Component>())
            {
                ModLogger.Instance?.Info($"[ModMenu]   - {comp.GetType().Name}");
            }
            
            // Find Canvas
            Canvas canvas = _assetBundleMenuInstance.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = _assetBundleMenuInstance.GetComponentInChildren<Canvas>();
            }
            
            if (canvas != null)
            {
                // WICHTIG: Behalte die Unity-Einstellungen bei!
                ModLogger.Instance?.Info($"[ModMenu] Found Canvas with existing settings:");
                ModLogger.Instance?.Info($"[ModMenu]   - RenderMode: {canvas.renderMode}");
                ModLogger.Instance?.Info($"[ModMenu]   - SortingOrder: {canvas.sortingOrder}");
                
                // Stelle sicher, dass es ScreenSpaceOverlay ist (für UI-Sichtbarkeit)
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    ModLogger.Instance?.Warning($"[ModMenu] Canvas renderMode was {canvas.renderMode}, setting to ScreenSpaceOverlay");
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                
                // Nur sortingOrder anpassen für maximale Sichtbarkeit
                canvas.sortingOrder = 32767;
                canvas.enabled = true;
                
                // Log Canvas RectTransform settings from Unity
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    ModLogger.Instance?.Info($"[ModMenu] Canvas RectTransform from Unity:");
                    ModLogger.Instance?.Info($"[ModMenu]   - Anchors: Min{canvasRect.anchorMin} Max{canvasRect.anchorMax}");
                    ModLogger.Instance?.Info($"[ModMenu]   - Size: {canvasRect.sizeDelta}");
                    ModLogger.Instance?.Info($"[ModMenu]   - Position: {canvasRect.anchoredPosition}");
                    ModLogger.Instance?.Info($"[ModMenu]   - Pivot: {canvasRect.pivot}");
                }
            }
            else
            {
                ModLogger.Instance?.Error("[ModMenu] No Canvas found in AssetBundle prefab!");
                return;
            }
            
            // CanvasScaler sollte bereits in Unity konfiguriert sein
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                ModLogger.Instance?.Info($"[ModMenu] CanvasScaler found with settings:");
                ModLogger.Instance?.Info($"[ModMenu]   - UIScaleMode: {scaler.uiScaleMode}");
                ModLogger.Instance?.Info($"[ModMenu]   - ReferenceResolution: {scaler.referenceResolution}");
            }
            
            // GraphicRaycaster Konfiguration
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                ModLogger.Instance?.Info("[ModMenu] Added GraphicRaycaster");
            }
            
            // Force all GameObjects to UI layer
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer == -1)
            {
                ModLogger.Instance?.Warning("[ModMenu] UI layer not found! Using layer 5 as fallback");
                uiLayer = 5; // Standard Unity UI layer
            }
            SetLayerRecursively(_assetBundleMenuInstance, uiLayer);
            ModLogger.Instance?.Info($"[ModMenu] Set all objects to UI layer (layer {uiLayer})");
            
            // Ensure all UI elements are visible
            EnsureUIElementsVisible(_assetBundleMenuInstance);
            
            // NICHT die Position/Größe ändern - Unity-Einstellungen beibehalten!
            ModLogger.Instance?.Info("[ModMenu] Keeping Unity's positioning and size settings");
            
            // Check and setup EventSystem
            if (EventSystem.current == null)
            {
                ModLogger.Instance?.Error("[ModMenu] No EventSystem found in the scene! The UI will not be clickable. This is unexpected.");
            }
            else
            {
                ModLogger.Instance?.Info($"[ModMenu] EventSystem already exists: {EventSystem.current.name}");
            }
            
            // Ensure GraphicRaycaster can receive events
            if (raycaster != null)
            {
                raycaster.ignoreReversedGraphics = false;
                raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                ModLogger.Instance?.Info($"[ModMenu] GraphicRaycaster configured - blockingObjects: {raycaster.blockingObjects}");
            }
            else
            {
                ModLogger.Instance?.Warning("[ModMenu] No GraphicRaycaster found after setup!");
            }
            
            // Add a debug background to see if Canvas is rendering
            AddDebugBackground(canvas);
            
            // Final check: Ensure the menu is NOT active
            if (_assetBundleMenuInstance.activeSelf)
            {
                ModLogger.Instance?.Warning("[ModMenu] Menu was active after setup - deactivating!");
                _assetBundleMenuInstance.SetActive(false);
            }
            
            ModLogger.Instance?.Info("[ModMenu] AssetBundle menu components setup complete");
        }
        
        private void EnsureUIElementsVisible(GameObject root)
        {
            // Check Image components but don't force alpha changes unless necessary
            var images = root.GetComponentsInChildren<Image>(true);
            ModLogger.Instance?.Info($"[ModMenu] Found {images.Length} Image components");
            foreach (var img in images)
            {
                img.enabled = true;
                if (img.color.a < 0.1f)
                {
                    ModLogger.Instance?.Warning($"[ModMenu] Image {img.name} has very low alpha: {img.color.a}");
                    // Only adjust if it's essentially invisible
                    if (img.color.a < 0.01f)
                    {
                        Color c = img.color;
                        c.a = 0.9f; // Use 0.9 instead of 1.0 for semi-transparency
                        img.color = c;
                        ModLogger.Instance?.Info($"[ModMenu] Adjusted alpha for {img.name} to {c.a}");
                    }
                }
                
                // DO NOT force raycastTarget here. It should be set correctly in the prefab.
                // Forcing it to true makes non-interactive backgrounds block clicks.
                ModLogger.Instance?.Info($"[ModMenu]   - {img.name}: color={img.color}, raycastTarget={img.raycastTarget}");
            }
            
            // Enable all Text components
            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            ModLogger.Instance?.Info($"[ModMenu] Found {texts.Length} TextMeshProUGUI components");
            foreach (var txt in texts)
            {
                txt.enabled = true;
                ModLogger.Instance?.Info($"[ModMenu]   - {txt.name}: text='{txt.text}', color={txt.color}");
            }
            
            // Check for Button components and ensure they're interactable
            var buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            ModLogger.Instance?.Info($"[ModMenu] Found {buttons.Length} Button components");
            foreach (var btn in buttons)
            {
                btn.interactable = true;
                
                // Ensure the button's Image component can receive raycasts
                var btnImage = btn.GetComponent<Image>();
                if (btnImage != null)
                {
                    btnImage.raycastTarget = true;
                }
                
                ModLogger.Instance?.Info($"[ModMenu]   - Button {btn.name}: interactable={btn.interactable}");
                
                // Log button's onClick listeners
                if (btn.onClick != null)
                {
                    ModLogger.Instance?.Info($"[ModMenu]     -> onClick has {btn.onClick.GetPersistentEventCount()} persistent listeners");
                }
                
                // Add a test listener to verify button clicks work
                btn.onClick.AddListener(() => {
                    ModLogger.Instance?.Info($"[ModMenu] Button clicked: {btn.name}");
                });
            }
            
            // Don't force activate all GameObjects here - let them maintain their intended state
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            ModLogger.Instance?.Info($"[ModMenu] Total {allTransforms.Length} GameObjects in hierarchy");
        }
        
        private void AddDebugBackground(Canvas canvas)
        {
            // Debug-Background nicht mehr nötig, da Menu jetzt sichtbar ist
            return;
            
            /*
            ModLogger.Instance?.Info("[ModMenu] Adding debug background panel...");
            
            // Create a debug panel to ensure something is visible
            GameObject debugPanel = new GameObject("DebugBackground");
            debugPanel.transform.SetParent(canvas.transform, false);
            
            RectTransform rectTransform = debugPanel.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            Image bg = debugPanel.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Dark gray with transparency
            
            ModLogger.Instance?.Info($"[ModMenu] Debug background added with color: {bg.color}");
            */
        }
        
        // Diese Methode wird nicht mehr benötigt, da wir Unity-Einstellungen beibehalten
        private void FixMenuPositionAndSize()
        {
            // Deprecated - Unity-Einstellungen werden beibehalten
            ModLogger.Instance?.Info("[ModMenu] FixMenuPositionAndSize skipped - keeping Unity AssetBundle settings");
        }
        
        // Diese Methode wird nicht mehr benötigt, da FixMenuPositionAndSize alles handhabt
        private void PositionMenu(MenuPosition position, float width = 400, float height = 600, float margin = 20)
        {
            // Deprecated - wird durch FixMenuPositionAndSize ersetzt
        }
        
        private void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        
        private void CheckForUpdateMessages()
        {
            ModLogger.Instance?.Info("[ModMenu] CheckForUpdateMessages called");
            
            // Check for update messages on first open
            if (!_hasCheckedForUpdate && ApiService != null)
            {
                _hasCheckedForUpdate = true;
                ModLogger.Instance?.Info($"[ModMenu] First time check - calling API for version: {FollowMePeak.Plugin.MOD_VERSION}");
                
                ApiService.CheckForUpdateMessage(FollowMePeak.Plugin.MOD_VERSION, (updateMessage) =>
                {
                    ModLogger.Instance?.Info($"[ModMenu] Update check callback - HasUpdate: {updateMessage?.HasUpdate}, Type: {updateMessage?.Type}, Message: {updateMessage?.Message?.Substring(0, System.Math.Min(50, updateMessage?.Message?.Length ?? 0))}...");
                    
                    if (updateMessage != null && updateMessage.HasUpdate)
                    {
                        _lastUpdateMessage = updateMessage;
                        // Pass to ClimbsTab if it exists
                        if (_uiController != null)
                        {
                            var climbsTab = _uiController.GetClimbsTabController();
                            ModLogger.Instance?.Info($"[ModMenu] Got ClimbsTabController: {climbsTab != null}");
                            
                            if (climbsTab != null)
                            {
                                ModLogger.Instance?.Info("[ModMenu] Calling ShowUpdateMessage on ClimbsTab");
                                climbsTab.ShowUpdateMessage(updateMessage);
                            }
                            else
                            {
                                ModLogger.Instance?.Error("[ModMenu] ClimbsTabController is null!");
                            }
                        }
                        else
                        {
                            ModLogger.Instance?.Error("[ModMenu] UIController is null!");
                        }
                    }
                    else
                    {
                        ModLogger.Instance?.Info("[ModMenu] No update message or HasUpdate is false");
                    }
                });
            }
            // If we already have a message, show it again (unless dismissed)
            else if (_lastUpdateMessage != null && _lastUpdateMessage.HasUpdate)
            {
                ModLogger.Instance?.Info("[ModMenu] Showing cached update message");
                var climbsTab = _uiController?.GetClimbsTabController();
                climbsTab?.ShowUpdateMessage(_lastUpdateMessage);
            }
            else
            {
                ModLogger.Instance?.Info($"[ModMenu] Not checking - already checked: {_hasCheckedForUpdate}, ApiService: {ApiService != null}, cached message: {_lastUpdateMessage != null}");
            }
        }
        
        public void ToggleAssetBundleMenu()
        {
            ModLogger.Instance?.Info($"[ModMenu] ToggleAssetBundleMenu called");
            ModLogger.Instance?.Info($"[ModMenu]   - _assetBundleLoaded: {_assetBundleLoaded}");
            ModLogger.Instance?.Info($"[ModMenu]   - AssetBundleService.Instance exists: {AssetBundleService.Instance != null}");
            ModLogger.Instance?.Info($"[ModMenu]   - AssetBundleService.IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            ModLogger.Instance?.Info($"[ModMenu]   - _assetBundleMenuInstance: {_assetBundleMenuInstance != null}");
            
            if (!_assetBundleLoaded)
            {
                ModLogger.Instance?.Error("[ModMenu] AssetBundle not loaded yet - cannot toggle menu");
                ModLogger.Instance?.Error($"[ModMenu] Try manually checking service: {AssetBundleService.Instance?.IsLoaded ?? false}");
                
                // Try to force load if service says it's loaded but we don't know about it
                if (AssetBundleService.Instance?.IsLoaded == true)
                {
                    ModLogger.Instance?.Warning("[ModMenu] Service reports loaded but manager wasn't notified - forcing OnAssetBundleLoaded");
                    OnAssetBundleLoaded();
                }
                else
                {
                    return;
                }
            }
            
            if (_assetBundleMenuInstance == null)
            {
                ModLogger.Instance?.Error("[ModMenu] AssetBundle menu instance is null - attempting to create");
                CreateAssetBundleMenu();
                if (_assetBundleMenuInstance == null)
                {
                    ModLogger.Instance?.Error("[ModMenu] Failed to create menu instance");
                    return;
                }
            }
            
            bool newState = !_assetBundleMenuInstance.activeSelf;
            _assetBundleMenuInstance.SetActive(newState);
            ModLogger.Instance?.Info($"[ModMenu] AssetBundle menu toggled to: {newState}");

            // Let the game's own UI (e.g. ESC menu) handle cursor locking/unlocking
            // to avoid conflicts with other mods like AdvancedConsole.
            
            if (newState)
            {
                // Ensure EventSystem is active
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.enabled = true;
                    // Force update EventSystem to recognize UI
                    eventSystem.SetSelectedGameObject(null);
                }
                
                // Bring menu to front
                Canvas[] canvases = _assetBundleMenuInstance.GetComponentsInChildren<Canvas>();
                foreach (var canvas in canvases)
                {
                    if (canvas != null)
                    {
                        canvas.sortingOrder = 32767; // Ensure it's on top
                    }
                }
                
                // Trigger OnShow for the active tab when opening menu
                _uiController?.OnMenuOpened();
                
                // Check for update messages on first open
                CheckForUpdateMessages();
            }
            else
            {
                // Clear any selected UI element when closing
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(null);
                }
            }
        }
        
        private void InitializeUIController()
        {
            if (_assetBundleMenuInstance == null)
            {
                ModLogger.Instance?.Error("[ModMenu] Cannot initialize UI Controller - menu instance is null");
                return;
            }
            
            ModLogger.Instance?.Info("[ModMenu] Initializing UI Controller");
            
            // Create and initialize the UI controller
            _uiController = new ModMenuUIController();
            _uiController.Initialize(_assetBundleMenuInstance);
            
            // Initialize Settings Controller
            _settingsController = new UI.SettingsController();
            _settingsController.Initialize(_assetBundleMenuInstance);
            
            ModLogger.Instance?.Info("[ModMenu] UI Controller and Settings Controller initialized");
        }
        
        public void Update()
        {
            // Update Settings Controller for key recording
            _settingsController?.Update();
        }
        
        public void Cleanup()
        {
            // Clean up UI Controller
            if (_uiController != null)
            {
                _uiController.Cleanup();
                _uiController = null;
            }
            
            // Clean up AssetBundle menu
            if (_assetBundleMenuInstance != null)
            {
                Object.DestroyImmediate(_assetBundleMenuInstance);
                _assetBundleMenuInstance = null;
            }
        }
    }
}
