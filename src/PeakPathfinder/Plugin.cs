using BepInEx;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;
using PeakPathfinder.Services;
using PeakPathfinder.Managers;
using PeakPathfinder.UI;
using PeakPathfinder.Patches;

namespace PeakPathfinder
{
    [BepInPlugin("com.thomasaushh.peakpathfinder", "Peak Pathfinder", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private PathDataService _pathDataService;
        private PathRecordingManager _recordingManager;
        private PathVisualizationManager _visualizationManager;
        private PathfinderUI _ui;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"Plugin {Info.Metadata.GUID} wurde geladen!");
            
            InitializeServices();
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            Harmony.CreateAndPatchAll(typeof(PluginPatches));
            Logger.LogInfo("Harmony Patches wurden angewendet.");
        }

        private void InitializeServices()
        {
            _pathDataService = new PathDataService(Logger);
            _recordingManager = new PathRecordingManager(_pathDataService, Logger, this);
            _visualizationManager = new PathVisualizationManager(_pathDataService);
            _ui = new PathfinderUI(_pathDataService, _visualizationManager);
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _ui.ShowMenu = !_ui.ShowMenu;
            }
        }
        
        private void OnGUI()
        {
            _ui.OnGUI();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.StartsWith("Level_"))
            {
                StartCoroutine(InitializePathSystem(scene));
                _recordingManager.StartRecording();
            }
            else
            {
                _pathDataService.CurrentLevelID = "";
                _visualizationManager.ClearVisuals();
            }
        }

        private IEnumerator InitializePathSystem(Scene scene)
        {
            yield return new WaitForSeconds(0.5f);
            var nextLevelService = GameHandler.GetService<NextLevelService>();
            if (nextLevelService != null && nextLevelService.Data.IsSome)
            {
                int levelIndex = nextLevelService.Data.Value.CurrentLevelIndex;
                _pathDataService.CurrentLevelID = $"{scene.name}_{levelIndex}";
                Logger.LogInfo($"Level erkannt: {_pathDataService.CurrentLevelID}");
                _pathDataService.LoadPathsFromFile();
                _visualizationManager.InitializePathVisibility();
            }
            else
            {
                Logger.LogError("NextLevelService oder dessen Daten konnten nicht gefunden werden!");
                _pathDataService.CurrentLevelID = scene.name + "_unknown";
            }
        }

        public void OnCampfireLit(string biomeName)
        {
            _recordingManager.SaveCurrentPath(biomeName);
            _recordingManager.StartRecording();
        }
    }
}