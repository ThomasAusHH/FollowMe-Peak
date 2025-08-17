using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using PeakPathfinder.Models;

namespace PeakPathfinder.Services
{
    public class PathDataService
    {
        private readonly ManualLogSource _logger;
        private List<PathData> _allLoadedPaths = new List<PathData>();
        private string _currentLevelID = "";

        public PathDataService(ManualLogSource logger)
        {
            _logger = logger;
        }

        public string CurrentLevelID
        {
            get => _currentLevelID;
            set => _currentLevelID = value;
        }

        public List<PathData> GetAllPaths() => _allLoadedPaths;

        public void AddPath(PathData pathData)
        {
            _allLoadedPaths.Add(pathData);
        }

        public void DeletePaths(List<Guid> pathIds)
        {
            _allLoadedPaths.RemoveAll(p => pathIds.Contains(p.Id));
            SavePathsToFile(false);
        }


        public void SavePathsToFile(bool addNewPath = true)
        {
            if (string.IsNullOrEmpty(_currentLevelID) || _currentLevelID.EndsWith("_unknown")) return;
            
            try
            {
                string directoryPath = Path.Combine(Paths.PluginPath, "PeakPathfinder_Data");
                Directory.CreateDirectory(directoryPath);
                string filePath = Path.Combine(directoryPath, $"{_currentLevelID}.json");
                string json = JsonConvert.SerializeObject(_allLoadedPaths, Formatting.Indented);
                File.WriteAllText(filePath, json);
                _logger.LogInfo($"Erfolgreich {_allLoadedPaths.Count} Pfade in '{filePath}' gespeichert.");
            }
            catch (Exception e)
            {
                _logger.LogError($"Fehler beim Speichern der Pfade: {e}");
            }
        }

        public void LoadPathsFromFile()
        {
            _allLoadedPaths.Clear();
            if (string.IsNullOrEmpty(_currentLevelID) || _currentLevelID.EndsWith("_unknown")) return;
            
            string filePath = Path.Combine(Paths.PluginPath, "PeakPathfinder_Data", $"{_currentLevelID}.json");
            if (!File.Exists(filePath))
            {
                _logger.LogInfo($"Keine Pfad-Datei für '{_currentLevelID}' gefunden.");
                return;
            }
            try
            {
                string json = File.ReadAllText(filePath);
                _allLoadedPaths = JsonConvert.DeserializeObject<List<PathData>>(json) ?? new List<PathData>();
                _logger.LogInfo($"{_allLoadedPaths.Count} Pfade für Level '{_currentLevelID}' geladen.");
            }
            catch (Exception e)
            {
                _logger.LogError($"Fehler beim Laden der Pfade (möglicherweise altes Format?): {e.Message}");
            }
        }

        public void ClearPaths()
        {
            _allLoadedPaths.Clear();
        }
    }
}