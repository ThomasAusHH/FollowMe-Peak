using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using FollowMePeak.Models;

namespace FollowMePeak.Services
{
    public class ClimbDataService
    {
        private readonly ManualLogSource _logger;
        private List<ClimbData> _allLoadedClimbs = new List<ClimbData>();
        private string _currentLevelID = "";

        public ClimbDataService(ManualLogSource logger)
        {
            _logger = logger;
        }

        public string CurrentLevelID
        {
            get => _currentLevelID;
            set => _currentLevelID = value;
        }

        public List<ClimbData> GetAllClimbs() => _allLoadedClimbs;

        public void AddClimb(ClimbData climbData)
        {
            _allLoadedClimbs.Add(climbData);
        }

        public void DeleteClimbs(List<Guid> climbIds)
        {
            _allLoadedClimbs.RemoveAll(c => climbIds.Contains(c.Id));
            SaveClimbsToFile(false);
        }


        public void SaveClimbsToFile(bool addNewClimb = true)
        {
            if (string.IsNullOrEmpty(_currentLevelID) || _currentLevelID.EndsWith("_unknown")) return;
            
            try
            {
                string directoryPath = Path.Combine(Paths.PluginPath, "FollowMePeak_Data");
                Directory.CreateDirectory(directoryPath);
                string filePath = Path.Combine(directoryPath, $"{_currentLevelID}.json");
                string json = JsonConvert.SerializeObject(_allLoadedClimbs, Formatting.Indented);
                File.WriteAllText(filePath, json);
                _logger.LogInfo($"Successfully saved {_allLoadedClimbs.Count} climbs to '{filePath}'.");
            }
            catch (Exception e)
            {
                _logger.LogError($"Error saving climbs: {e}");
            }
        }

        public void LoadClimbsFromFile()
        {
            _allLoadedClimbs.Clear();
            if (string.IsNullOrEmpty(_currentLevelID) || _currentLevelID.EndsWith("_unknown")) return;
            
            string filePath = Path.Combine(Paths.PluginPath, "FollowMePeak_Data", $"{_currentLevelID}.json");
            if (!File.Exists(filePath))
            {
                _logger.LogInfo($"No climb file found for '{_currentLevelID}'.");
                return;
            }
            try
            {
                string json = File.ReadAllText(filePath);
                _allLoadedClimbs = JsonConvert.DeserializeObject<List<ClimbData>>(json) ?? new List<ClimbData>();
                _logger.LogInfo($"{_allLoadedClimbs.Count} climbs loaded for level '{_currentLevelID}'.");
            }
            catch (Exception e)
            {
                _logger.LogError($"Error loading climbs (possibly old format?): {e.Message}");
            }
        }

        public void ClearClimbs()
        {
            _allLoadedClimbs.Clear();
        }
    }
}