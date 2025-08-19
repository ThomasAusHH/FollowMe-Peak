using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using FollowMePeak.Models;
using FollowMePeak.Services;

namespace FollowMePeak.Managers
{
    public class ClimbRecordingManager
    {
        private readonly ClimbDataService _climbDataService;
        private readonly ManualLogSource _logger;
        
        private List<Vector3> _currentRecordedClimb = new List<Vector3>();
        private bool _isRecording = false;
        private float _recordingStartTime;
        private MonoBehaviour _coroutineRunner;

        public ClimbRecordingManager(ClimbDataService climbDataService, ManualLogSource logger, MonoBehaviour coroutineRunner)
        {
            _climbDataService = climbDataService;
            _logger = logger;
            _coroutineRunner = coroutineRunner;
        }

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            if (_isRecording) return;
            _isRecording = true;
            _currentRecordedClimb.Clear();
            _recordingStartTime = Time.time;
            _logger.LogInfo("Kletter-Aufzeichnung gestartet!");
            _coroutineRunner.StartCoroutine(RecordClimbRoutine());
        }

        public void StopRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;
            _logger.LogInfo($"Aufzeichnung gestoppt. {_currentRecordedClimb.Count} Punkte zwischengespeichert.");
        }

        public void SaveCurrentClimb(string biomeName)
        {
            StopRecording();
            if (_currentRecordedClimb.Count < 2) return;
            
            var newClimbData = new ClimbData
            {
                Id = Guid.NewGuid(),
                CreationTime = DateTime.Now,
                BiomeName = biomeName ?? "Unbekannt",
                DurationInSeconds = Time.time - _recordingStartTime,
                Points = _currentRecordedClimb.Select(vec => new SerializableVector3(vec)).ToList()
            };
            
            // Generate share code for the new climb
            newClimbData.GenerateShareCode();
            
            _climbDataService.AddClimb(newClimbData);
            _climbDataService.SaveClimbsToFile(false);
            
            // Show tag selection popup for successful climbs
            ShowTagSelectionForNewClimb(newClimbData);
        }
        
        private void ShowTagSelectionForNewClimb(ClimbData climbData)
        {
            // We'll trigger this through the Plugin to show the tag selection UI
            _logger.LogInfo($"Neue Kletterroute erstellt: {climbData.GetDisplayName()} (Code: {climbData.ShareCode})");
            
            // Trigger tag selection in the UI
            Plugin.Instance.ShowTagSelectionForNewClimb(climbData);
        }

        private IEnumerator RecordClimbRoutine()
        {
            while (_isRecording)
            {
                if (Camera.main != null) 
                    _currentRecordedClimb.Add(Camera.main.transform.position);
                yield return new WaitForSeconds(2.0f);
            }
        }
    }
}