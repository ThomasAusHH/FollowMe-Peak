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
        private float _recordingStartTime;
        private MonoBehaviour _coroutineRunner;

        public bool IsRecording { get; private set; } = false;

        public ClimbRecordingManager(ClimbDataService climbDataService, ManualLogSource logger, MonoBehaviour coroutineRunner)
        {
            _climbDataService = climbDataService;
            _logger = logger;
            _coroutineRunner = coroutineRunner;
        }

        public void StartRecording()
        {
            if (IsRecording) return;
            IsRecording = true;
            _currentRecordedClimb.Clear();
            _recordingStartTime = Time.time;
            _logger.LogInfo("Kletter-Aufzeichnung gestartet!");
            _coroutineRunner.StartCoroutine(RecordClimbRoutine());
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
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
            while (IsRecording)
            {
                // TODO: Is this still valid even if the player is in third-person view?
                var camera = Camera.main;
                if (camera != null)
                    _currentRecordedClimb.Add(camera.transform.position);
                yield return new WaitForSeconds(2.0f);
            }
        }
    }
}