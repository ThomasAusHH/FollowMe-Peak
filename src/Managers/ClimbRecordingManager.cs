using System;
using System.Collections;
using System.Collections.Generic;
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
            _currentRecordedClimb = [];
            _recordingStartTime = Time.time;
            
            // Reset Fly Detection for new recording (safety reset)
            Detection.SimpleFlyDetector.ResetForNewRecording();
            
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

            var currentClimb = _currentRecordedClimb;
            if (currentClimb.Count < 2) return;

            // Reset the current recorded climb
            // (but don't clear the list, we need that list to save it; instead assign a new empty list)
            _currentRecordedClimb = [];

            var durationInSeconds = Time.time - _recordingStartTime;

            // CAPTURE FLAGS HERE - BEFORE ASYNC and BEFORE any reset happens!
            bool wasFlagged = Detection.SimpleFlyDetector.WasDetectedInCurrentRecording;
            float flaggedScore = Detection.SimpleFlyDetector.MaxScoreInCurrentRecording;
            string flaggedReason = Detection.SimpleFlyDetector.ReasonForCurrentRecording;
            
            // Debug log to verify flag capture
            _logger.LogInfo($"[FlyDetection] Captured before async: Flagged={wasFlagged}, Score={flaggedScore}, Reason={flaggedReason}");

            BepInEx.ThreadingHelper.Instance.StartAsyncInvoke(CreateClimbData);
            return;

            Action CreateClimbData()
            {
                // Use captured flags from closure (they were captured before async)
                var newClimbData = new ClimbData
                {
                    Id = Guid.NewGuid(),
                    CreationTime = DateTime.Now,
                    BiomeName = biomeName ?? "Unbekannt",
                    DurationInSeconds = durationInSeconds,
                    Points = currentClimb,
                    AscentLevel = Ascents.currentAscent,
                    // Set detection flags
                    WasFlagged = wasFlagged,
                    FlaggedScore = flaggedScore,
                    FlaggedReason = flaggedReason
                };

                // Generate share code for the new climb
                newClimbData.GenerateShareCode();
                return () => AfterClimbIsCreated(newClimbData);
            }

            void AfterClimbIsCreated(ClimbData newClimbData)
            {
                _logger.LogInfo($"Climb saved with ascent level: {newClimbData.AscentLevel}");
                
                // Log detection state if flagged
                if (newClimbData.WasFlagged)
                {
                    _logger.LogWarning($"[FlyDetection] Climb was flagged: Score={newClimbData.FlaggedScore}, Reason={newClimbData.FlaggedReason}");
                }

                _climbDataService.AddClimb(newClimbData);
                _climbDataService.SaveClimbsToFile(false);
            
                // Show tag selection popup for successful climbs
                ShowTagSelectionForNewClimb(newClimbData);
            }
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
                {
                    // Record position below camera to prevent the playback line from blocking the player's view
                    // Offset by 0.8 units downward from camera position
                    Vector3 recordPosition = camera.transform.position + Vector3.down * 0.8f;
                    _currentRecordedClimb.Add(recordPosition);
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
