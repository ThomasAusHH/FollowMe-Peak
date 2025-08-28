using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection; // Hinzugefügt für das Laden aus der DLL
using UnityEngine;
using BepInEx.Logging;

namespace FollowMePeak.Services
{
    public class AssetBundleService
    {
        private static AssetBundleService _instance;
        public static AssetBundleService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AssetBundleService();
                }
                return _instance;
            }
        }

        private ManualLogSource _logger;
        private AssetBundle _modUIBundle;
        private readonly Dictionary<string, UnityEngine.Object> _cachedAssets = new Dictionary<string, UnityEngine.Object>();
        private bool _isLoaded = false;
        private bool _isLoading = false;

        // Asset-Namen im Bundle
        public const string MOD_MENU_CANVAS_PREFAB = "ModMenuCanvas";
        public const string MOD_MENU_PANEL_PREFAB = "MyModMenuPanel";
        public const string MOD_MENU_MAIN_PREFAB = "ModMenuMain";

        private AssetBundleService()
        {
            _logger = BepInEx.Logging.Logger.CreateLogSource("AssetBundleService");
        }

        /// <summary>
        /// Lädt das UI AssetBundle als eingebettete Ressource aus der Mod-DLL.
        /// </summary>
        public IEnumerator LoadModUIBundle(Action<bool> onComplete = null)
        {
            if (_isLoaded)
            {
                _logger.LogInfo("Mod UI bundle is already loaded.");
                onComplete?.Invoke(true);
                yield break;
            }

            if (_isLoading)
            {
                _logger.LogWarning("Mod UI bundle is already being loaded.");
                yield break; // Warten, bis der Ladevorgang abgeschlossen ist, anstatt einen Fehler auszulösen
            }

            _isLoading = true;
            byte[] assetBundleData = null;

            try
            {

                var assembly = Assembly.GetExecutingAssembly();

                string resourceName = "PeakClimbfinder.modui";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        _logger.LogError($"Embedded resource '{resourceName}' not found! Make sure the Build Action is set to 'Embedded Resource'.");
                        // Optional: Alle verfügbaren Ressourcen auflisten, um den richtigen Namen zu finden.
                        _logger.LogInfo("Available embedded resources:");
                        foreach (var name in assembly.GetManifestResourceNames())
                        {
                            _logger.LogInfo($" -> {name}");
                        }
                        
                        _isLoading = false;
                        onComplete?.Invoke(false);
                        yield break;
                    }

                    // 4. Den Datenstrom in ein Byte-Array kopieren, das Unity verarbeiten kann.
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        assetBundleData = memoryStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while reading the embedded asset bundle: {ex}");
                _isLoading = false;
                onComplete?.Invoke(false);
                yield break;
            }

            // 5. Das AssetBundle asynchron aus dem Byte-Array im Speicher laden.
            var bundleLoadRequest = AssetBundle.LoadFromMemoryAsync(assetBundleData);
            yield return bundleLoadRequest;

            // 6. Das Ergebnis verarbeiten.
            if (bundleLoadRequest.assetBundle == null)
            {
                _logger.LogError("Failed to load AssetBundle from memory. The bundle might be corrupt or incompatible.");
                _isLoading = false;
                onComplete?.Invoke(false);
                yield break;
            }

            _modUIBundle = bundleLoadRequest.assetBundle;
            _logger.LogInfo($"Successfully loaded AssetBundle '{_modUIBundle.name}' from embedded resource.");

            // Liste alle Assets im Bundle für Debugging-Zwecke auf
            string[] assetNames = _modUIBundle.GetAllAssetNames();
            _logger.LogInfo($"AssetBundle contains {assetNames.Length} assets:");
            foreach (string assetName in assetNames)
            {
                _logger.LogInfo($"  - {assetName}");
            }

            _isLoaded = true;
            _isLoading = false;
            onComplete?.Invoke(true);
        }

        public GameObject GetPrefab(string prefabName)
        {
            if (!_isLoaded || _modUIBundle == null)
            {
                _logger.LogError("AssetBundle is not loaded. Call LoadModUIBundle first.");
                return null;
            }

            if (_cachedAssets.TryGetValue(prefabName, out var cachedAsset))
            {
                _logger.LogInfo($"Returning cached prefab: {prefabName}");
                return cachedAsset as GameObject;
            }

            try
            {
                _logger.LogInfo($"Attempting to load prefab: {prefabName}");
                
                // Versuche verschiedene Pfadvarianten
                GameObject prefab = null;
                
                // Versuche direkten Namen
                prefab = _modUIBundle.LoadAsset<GameObject>(prefabName);
                
                // Wenn nicht gefunden, versuche mit lowercase
                if (prefab == null)
                {
                    string lowerName = prefabName.ToLower();
                    _logger.LogInfo($"Trying lowercase variant: {lowerName}");
                    prefab = _modUIBundle.LoadAsset<GameObject>(lowerName);
                }
                
                // Wenn immer noch nicht gefunden, durchsuche alle Assets
                if (prefab == null)
                {
                    _logger.LogInfo($"Searching through all assets for partial match...");
                    string[] allAssets = _modUIBundle.GetAllAssetNames();
                    foreach (string assetPath in allAssets)
                    {
                        if (assetPath.ToLower().Contains(prefabName.ToLower()))
                        {
                            _logger.LogInfo($"Found potential match: {assetPath}");
                            var loadedAsset = _modUIBundle.LoadAsset(assetPath);
                            if (loadedAsset is GameObject)
                            {
                                prefab = loadedAsset as GameObject;
                                _logger.LogInfo($"Successfully loaded from path: {assetPath}");
                                break;
                            }
                        }
                    }
                }

                if (prefab != null)
                {
                    _cachedAssets[prefabName] = prefab;
                    _logger.LogInfo($"Successfully loaded and cached prefab: {prefabName}");
                    
                    // Log prefab details
                    _logger.LogInfo($"Prefab components:");
                    foreach (var component in prefab.GetComponents<Component>())
                    {
                        _logger.LogInfo($"  - {component.GetType().Name}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Prefab '{prefabName}' not found in the AssetBundle.");
                    _logger.LogInfo("Available GameObjects in bundle:");
                    foreach (var asset in _modUIBundle.GetAllAssetNames())
                    {
                        var obj = _modUIBundle.LoadAsset(asset);
                        if (obj is GameObject)
                        {
                            _logger.LogInfo($"  - {asset} (GameObject)");
                        }
                    }
                }

                return prefab;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading prefab '{prefabName}': {ex.Message}");
                return null;
            }
        }

        public T GetAsset<T>(string assetName) where T : UnityEngine.Object
        {
            if (!_isLoaded || _modUIBundle == null)
            {
                _logger.LogError("AssetBundle is not loaded. Call LoadModUIBundle first.");
                return null;
            }

            if (_cachedAssets.TryGetValue(assetName, out var cachedAsset))
            {
                return cachedAsset as T;
            }

            try
            {
                T asset = _modUIBundle.LoadAsset<T>(assetName);
                
                if (asset != null)
                {
                    _cachedAssets[assetName] = asset;
                    // _logger.LogInfo($"Successfully loaded and cached asset: {assetName} of type {typeof(T).Name}");
                }
                else
                {
                    _logger.LogWarning($"Asset '{assetName}' of type {typeof(T).Name} not found in AssetBundle");
                }

                return asset;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading asset '{assetName}': {ex.Message}");
                return null;
            }
        }

        public bool IsLoaded => _isLoaded;

        public void Unload()
        {
            if (_modUIBundle != null)
            {
                _logger.LogInfo("Unloading Mod UI AssetBundle");
                _modUIBundle.Unload(true); // true entlädt auch alle geladenen Assets
                _modUIBundle = null;
            }

            _cachedAssets.Clear();
            _isLoaded = false;
            _isLoading = false;
        }
    }
}