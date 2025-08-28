using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FollowMePeak.Services;
using TMPro;

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
        
        // Services (to be set from Plugin)
        public static Services.ServerConfigService ServerConfig { get; set; }
        public static Services.VPSApiService ApiService { get; set; }
        public static Services.ClimbUploadService UploadService { get; set; }
        public static Services.ClimbDownloadService DownloadService { get; set; }
        public static Services.ClimbDataService ClimbDataService { get; set; }
        public static Managers.ClimbVisualizationManager VisualizationManager { get; set; }
        
        public ModMenuManager()
        {
            Debug.Log("[ModMenu] Manager initialized for AssetBundle menu");
        }
        
        public void OnAssetBundleLoaded()
        {
            Debug.Log($"[ModMenu] OnAssetBundleLoaded called - Previous state: {_assetBundleLoaded}");
            _assetBundleLoaded = true;
            Debug.Log($"[ModMenu] AssetBundle loaded flag set to: {_assetBundleLoaded}");
            Debug.Log($"[ModMenu] AssetBundleService.IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            CreateAssetBundleMenu();
        }
        
        private void CreateAssetBundleMenu()
        {
            Debug.Log("[ModMenu] CreateAssetBundleMenu called");
            Debug.Log($"[ModMenu]   - _assetBundleLoaded: {_assetBundleLoaded}");
            Debug.Log($"[ModMenu]   - AssetBundleService.IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            
            if (!_assetBundleLoaded || !AssetBundleService.Instance.IsLoaded)
            {
                Debug.LogError($"[ModMenu] Cannot create AssetBundle menu - bundle not loaded (loaded:{_assetBundleLoaded}, service:{AssetBundleService.Instance?.IsLoaded ?? false})");
                return;
            }
            
            // Check if menu already exists
            if (_assetBundleMenuInstance != null)
            {
                Debug.LogWarning("[ModMenu] Menu instance already exists - skipping creation");
                return;
            }
            
            try
            {
                Debug.Log("[ModMenu] Attempting to get prefabs from AssetBundle...");
                
                // Try to get the main menu prefab from the AssetBundle
                GameObject menuPrefab = AssetBundleService.Instance.GetPrefab(AssetBundleService.MOD_MENU_MAIN_PREFAB);
                Debug.Log($"[ModMenu] Tried {AssetBundleService.MOD_MENU_MAIN_PREFAB}: {menuPrefab != null}");
                
                // If specific prefab not found, try alternatives
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab(AssetBundleService.MOD_MENU_CANVAS_PREFAB);
                    Debug.Log($"[ModMenu] Tried {AssetBundleService.MOD_MENU_CANVAS_PREFAB}: {menuPrefab != null}");
                }
                
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab(AssetBundleService.MOD_MENU_PANEL_PREFAB);
                    Debug.Log($"[ModMenu] Tried {AssetBundleService.MOD_MENU_PANEL_PREFAB}: {menuPrefab != null}");
                }
                
                // If still no prefab, try generic names
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab("ModMenu");
                    Debug.Log($"[ModMenu] Tried 'ModMenu': {menuPrefab != null}");
                }
                
                if (menuPrefab == null)
                {
                    menuPrefab = AssetBundleService.Instance.GetPrefab("Canvas");
                    Debug.Log($"[ModMenu] Tried 'Canvas': {menuPrefab != null}");
                }
                
                if (menuPrefab == null)
                {
                    Debug.LogError("[ModMenu] No suitable menu prefab found in AssetBundle");
                    return;
                }
                
                // Instantiate the menu from prefab
                _assetBundleMenuInstance = Object.Instantiate(menuPrefab);
                Object.DontDestroyOnLoad(_assetBundleMenuInstance);
                // WICHTIG: Menu initial deaktiviert lassen
                _assetBundleMenuInstance.SetActive(false);
                
                Debug.Log($"[ModMenu] AssetBundle menu created: {_assetBundleMenuInstance.name}");
                
                // Set up any required components or references
                SetupAssetBundleMenuComponents();
                
                // Initialize UI Controller
                InitializeUIController();
                
                // WICHTIG: Stelle sicher, dass das Menu definitiv deaktiviert ist
                _assetBundleMenuInstance.SetActive(false);
                Debug.Log("[ModMenu] Menu instance forcefully deactivated after setup");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ModMenu] Failed to create AssetBundle menu: {e.Message}\n{e.StackTrace}");
            }
        }
        
        private void SetupAssetBundleMenuComponents()
        {
            if (_assetBundleMenuInstance == null) return;
            
            Debug.Log("[ModMenu] Setting up AssetBundle menu components...");
            
            // Log all components in the instantiated prefab
            Debug.Log($"[ModMenu] Root GameObject: {_assetBundleMenuInstance.name}");
            Debug.Log($"[ModMenu] Root components:");
            foreach (var comp in _assetBundleMenuInstance.GetComponents<Component>())
            {
                Debug.Log($"[ModMenu]   - {comp.GetType().Name}");
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
                Debug.Log($"[ModMenu] Found Canvas with existing settings:");
                Debug.Log($"[ModMenu]   - RenderMode: {canvas.renderMode}");
                Debug.Log($"[ModMenu]   - SortingOrder: {canvas.sortingOrder}");
                
                // Stelle sicher, dass es ScreenSpaceOverlay ist (für UI-Sichtbarkeit)
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    Debug.LogWarning($"[ModMenu] Canvas renderMode was {canvas.renderMode}, setting to ScreenSpaceOverlay");
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                
                // Nur sortingOrder anpassen für maximale Sichtbarkeit
                canvas.sortingOrder = 32767;
                canvas.enabled = true;
                
                // Log Canvas RectTransform settings from Unity
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    Debug.Log($"[ModMenu] Canvas RectTransform from Unity:");
                    Debug.Log($"[ModMenu]   - Anchors: Min{canvasRect.anchorMin} Max{canvasRect.anchorMax}");
                    Debug.Log($"[ModMenu]   - Size: {canvasRect.sizeDelta}");
                    Debug.Log($"[ModMenu]   - Position: {canvasRect.anchoredPosition}");
                    Debug.Log($"[ModMenu]   - Pivot: {canvasRect.pivot}");
                }
            }
            else
            {
                Debug.LogError("[ModMenu] No Canvas found in AssetBundle prefab!");
                return;
            }
            
            // CanvasScaler sollte bereits in Unity konfiguriert sein
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Debug.Log($"[ModMenu] CanvasScaler found with settings:");
                Debug.Log($"[ModMenu]   - UIScaleMode: {scaler.uiScaleMode}");
                Debug.Log($"[ModMenu]   - ReferenceResolution: {scaler.referenceResolution}");
            }
            
            // GraphicRaycaster Konfiguration
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log("[ModMenu] Added GraphicRaycaster");
            }
            
            // Force all GameObjects to UI layer
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer == -1)
            {
                Debug.LogWarning("[ModMenu] UI layer not found! Using layer 5 as fallback");
                uiLayer = 5; // Standard Unity UI layer
            }
            SetLayerRecursively(_assetBundleMenuInstance, uiLayer);
            Debug.Log($"[ModMenu] Set all objects to UI layer (layer {uiLayer})");
            
            // Ensure all UI elements are visible
            EnsureUIElementsVisible(_assetBundleMenuInstance);
            
            // NICHT die Position/Größe ändern - Unity-Einstellungen beibehalten!
            Debug.Log("[ModMenu] Keeping Unity's positioning and size settings");
            
            // Check and setup EventSystem
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogWarning("[ModMenu] No EventSystem found - creating one");
                var eventSystemGO = new GameObject("ModMenuEventSystem");
                eventSystem = eventSystemGO.AddComponent<EventSystem>();
                var inputModule = eventSystemGO.AddComponent<StandaloneInputModule>();
                Object.DontDestroyOnLoad(eventSystemGO);
                Debug.Log("[ModMenu] EventSystem created");
            }
            else
            {
                Debug.Log($"[ModMenu] EventSystem already exists: {eventSystem.name}");
            }
            
            // Ensure GraphicRaycaster can receive events
            if (raycaster != null)
            {
                raycaster.ignoreReversedGraphics = false;
                raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                Debug.Log($"[ModMenu] GraphicRaycaster configured - blockingObjects: {raycaster.blockingObjects}");
            }
            else
            {
                Debug.LogWarning("[ModMenu] No GraphicRaycaster found after setup!");
            }
            
            // Add a debug background to see if Canvas is rendering
            AddDebugBackground(canvas);
            
            // Final check: Ensure the menu is NOT active
            if (_assetBundleMenuInstance.activeSelf)
            {
                Debug.LogWarning("[ModMenu] Menu was active after setup - deactivating!");
                _assetBundleMenuInstance.SetActive(false);
            }
            
            Debug.Log("[ModMenu] AssetBundle menu components setup complete");
        }
        
        private void EnsureUIElementsVisible(GameObject root)
        {
            // Check Image components but don't force alpha changes unless necessary
            var images = root.GetComponentsInChildren<Image>(true);
            Debug.Log($"[ModMenu] Found {images.Length} Image components");
            foreach (var img in images)
            {
                img.enabled = true;
                if (img.color.a < 0.1f)
                {
                    Debug.LogWarning($"[ModMenu] Image {img.name} has very low alpha: {img.color.a}");
                    // Only adjust if it's essentially invisible
                    if (img.color.a < 0.01f)
                    {
                        Color c = img.color;
                        c.a = 0.9f; // Use 0.9 instead of 1.0 for semi-transparency
                        img.color = c;
                        Debug.Log($"[ModMenu] Adjusted alpha for {img.name} to {c.a}");
                    }
                }
                
                // Set raycastTarget to true for interactive elements
                img.raycastTarget = true;
                Debug.Log($"[ModMenu]   - {img.name}: color={img.color}, raycastTarget={img.raycastTarget}");
            }
            
            // Enable all Text components
            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            Debug.Log($"[ModMenu] Found {texts.Length} TextMeshProUGUI components");
            foreach (var txt in texts)
            {
                txt.enabled = true;
                Debug.Log($"[ModMenu]   - {txt.name}: text='{txt.text}', color={txt.color}");
            }
            
            // Check for Button components and ensure they're interactable
            var buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            Debug.Log($"[ModMenu] Found {buttons.Length} Button components");
            foreach (var btn in buttons)
            {
                btn.interactable = true;
                
                // Ensure the button's Image component can receive raycasts
                var btnImage = btn.GetComponent<Image>();
                if (btnImage != null)
                {
                    btnImage.raycastTarget = true;
                }
                
                Debug.Log($"[ModMenu]   - Button {btn.name}: interactable={btn.interactable}");
                
                // Log button's onClick listeners
                if (btn.onClick != null)
                {
                    Debug.Log($"[ModMenu]     -> onClick has {btn.onClick.GetPersistentEventCount()} persistent listeners");
                }
                
                // Add a test listener to verify button clicks work
                btn.onClick.AddListener(() => {
                    Debug.Log($"[ModMenu] Button clicked: {btn.name}");
                });
            }
            
            // Don't force activate all GameObjects here - let them maintain their intended state
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            Debug.Log($"[ModMenu] Total {allTransforms.Length} GameObjects in hierarchy");
        }
        
        private void AddDebugBackground(Canvas canvas)
        {
            // Debug-Background nicht mehr nötig, da Menu jetzt sichtbar ist
            return;
            
            /*
            Debug.Log("[ModMenu] Adding debug background panel...");
            
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
            
            Debug.Log($"[ModMenu] Debug background added with color: {bg.color}");
            */
        }
        
        // Diese Methode wird nicht mehr benötigt, da wir Unity-Einstellungen beibehalten
        private void FixMenuPositionAndSize()
        {
            // Deprecated - Unity-Einstellungen werden beibehalten
            Debug.Log("[ModMenu] FixMenuPositionAndSize skipped - keeping Unity AssetBundle settings");
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
        
        public void ToggleAssetBundleMenu()
        {
            Debug.Log($"[ModMenu] ToggleAssetBundleMenu called");
            Debug.Log($"[ModMenu]   - _assetBundleLoaded: {_assetBundleLoaded}");
            Debug.Log($"[ModMenu]   - AssetBundleService.Instance exists: {AssetBundleService.Instance != null}");
            Debug.Log($"[ModMenu]   - AssetBundleService.IsLoaded: {AssetBundleService.Instance?.IsLoaded ?? false}");
            Debug.Log($"[ModMenu]   - _assetBundleMenuInstance: {_assetBundleMenuInstance != null}");
            
            if (!_assetBundleLoaded)
            {
                Debug.LogError("[ModMenu] AssetBundle not loaded yet - cannot toggle menu");
                Debug.LogError($"[ModMenu] Try manually checking service: {AssetBundleService.Instance?.IsLoaded ?? false}");
                
                // Try to force load if service says it's loaded but we don't know about it
                if (AssetBundleService.Instance?.IsLoaded == true)
                {
                    Debug.LogWarning("[ModMenu] Service reports loaded but manager wasn't notified - forcing OnAssetBundleLoaded");
                    OnAssetBundleLoaded();
                }
                else
                {
                    return;
                }
            }
            
            if (_assetBundleMenuInstance == null)
            {
                Debug.LogError("[ModMenu] AssetBundle menu instance is null - attempting to create");
                CreateAssetBundleMenu();
                if (_assetBundleMenuInstance == null)
                {
                    Debug.LogError("[ModMenu] Failed to create menu instance");
                    return;
                }
            }
            
            bool newState = !_assetBundleMenuInstance.activeSelf;
            _assetBundleMenuInstance.SetActive(newState);
            Debug.Log($"[ModMenu] AssetBundle menu toggled to: {newState}");
            
            // Handle cursor visibility and input
            if (newState)
            {
                // Enable cursor for menu interaction
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                Debug.Log("[ModMenu] Cursor enabled for menu interaction");
                
                // Ensure EventSystem is active
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.enabled = true;
                    Debug.Log($"[ModMenu] EventSystem enabled: {eventSystem.name}");
                    
                    // Force update EventSystem to recognize UI
                    eventSystem.SetSelectedGameObject(null);
                    
                    // Check if we can interact with UI
                    var raycaster = _assetBundleMenuInstance.GetComponentInChildren<GraphicRaycaster>();
                    if (raycaster != null)
                    {
                        Debug.Log($"[ModMenu] GraphicRaycaster found and active: {raycaster.enabled}");
                    }
                }
                
                // Bring menu to front - finde das richtige Canvas im Container
                Canvas[] canvases = _assetBundleMenuInstance.GetComponentsInChildren<Canvas>();
                foreach (var canvas in canvases)
                {
                    if (canvas != null)
                    {
                        canvas.sortingOrder = 32767; // Ensure it's on top
                        Debug.Log($"[ModMenu] Canvas '{canvas.name}' sortingOrder set to: {canvas.sortingOrder}");
                    }
                }
                
                // Trigger OnShow for the active tab when opening menu
                _uiController?.OnMenuOpened();
            }
            else
            {
                // Restore game's cursor state
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                Debug.Log("[ModMenu] Cursor disabled - returning to game");
                
                // Clear any selected UI element
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
                Debug.LogError("[ModMenu] Cannot initialize UI Controller - menu instance is null");
                return;
            }
            
            Debug.Log("[ModMenu] Initializing UI Controller");
            
            // Create and initialize the UI controller
            _uiController = new ModMenuUIController();
            _uiController.Initialize(_assetBundleMenuInstance);
            
            Debug.Log("[ModMenu] UI Controller initialized");
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