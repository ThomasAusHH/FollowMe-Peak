using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using FollowMePeak.Models;
using FollowMePeak.Utils;

namespace FollowMePeak.Services
{
    public class ClimbDataService
    {
        private readonly ModLogger _logger;
        private List<ClimbData> _allLoadedClimbs = new List<ClimbData>();
        private string _currentLevelID = "";

        public ClimbDataService(ModLogger logger)
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
            
            var allLoadedClimbs = new List<ClimbData>(_allLoadedClimbs);
            var filePath = Path.Combine(Paths.PluginPath, "FollowMePeak_Data", $"{_currentLevelID}.json");
            FileUtils.WriteJsonFileInBackground(_logger, filePath, allLoadedClimbs);
        }

        public void LoadClimbsFromFile()
        {
            _allLoadedClimbs.Clear();
            if (string.IsNullOrEmpty(_currentLevelID) || _currentLevelID.EndsWith("_unknown")) return;
            
            string filePath = Path.Combine(Paths.PluginPath, "FollowMePeak_Data", $"{_currentLevelID}.json");
            if (!File.Exists(filePath))
            {
                _logger.Info($"No climb file found for '{_currentLevelID}'.");
                return;
            }
            try
            {
                string json = File.ReadAllText(filePath);
                _allLoadedClimbs = JsonConvert.DeserializeObject<List<ClimbData>>(json, CommonJsonSettings.Default) ?? new List<ClimbData>();
                _logger.Info($"{_allLoadedClimbs.Count} climbs loaded for level '{_currentLevelID}'.");
            }
            catch (Exception e)
            {
                _logger.Error($"Error loading climbs (possibly old format?): {e.Message}");
            }
        }

        public void ClearClimbs()
        {
            _allLoadedClimbs.Clear();
        }
    }
}