using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using FollowMePeak.Models;
using FollowMePeak.Services;
using Zorro.Core;

namespace FollowMePeak.Managers
{
    public class ClimbRecordingManager
    {
        private readonly ClimbDataService _climbDataService;
        private readonly ManualLogSource _logger;
        
        private List<Vector3> _currentRecordedClimb = new List<Vector3>();
        private float _recordingStartTime;
        private MonoBehaviour _coroutineRunner;
        private bool _wasDeathDetected = false;

        public bool IsRecording { get; private set; } = false;
        public ClimbRecordingManager(ClimbDataService climbDataService, ManualLogSource logger, MonoBehaviour coroutineRunner)
        {
            _climbDataService = climbDataService;
            _logger = logger;
            _coroutineRunner = coroutineRunner;
        }

        public void StartRecording()
        {
            if (IsRecording) 
            {
                _logger.LogWarning("Recording already active - stopping previous recording before starting new one");
                StopRecording();
            }
            
            // Check if we're in a valid level
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("Level_"))
            {
                _logger.LogWarning("StartRecording called but not in a Level scene");
                return;
            }
            
            IsRecording = true;
            _currentRecordedClimb = [];
            _recordingStartTime = Time.time;
            _wasDeathDetected = false;
            
            // Reset Fly Detection for new recording (safety reset)
            Detection.SimpleFlyDetector.ResetForNewRecording();
            
            _logger.LogInfo("[ClimbRecording] Started by RUN STARTED event!");
            _coroutineRunner.StartCoroutine(RecordClimbRoutine());
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            
            // Clear recorded data if we have less than minimum points (incomplete climb)
            if (_currentRecordedClimb.Count < 2)
            {
                _logger.LogInfo($"Recording stopped with insufficient data ({_currentRecordedClimb.Count} points) - clearing");
                _currentRecordedClimb = new List<Vector3>();
            }
            else
            {
                _logger.LogInfo($"Recording stopped. {_currentRecordedClimb.Count} points cached for potential save.");
            }
        }

        public void SaveCurrentClimb(string biomeName)
        {
            StopRecording();

            var currentClimb = _currentRecordedClimb;
            if (currentClimb.Count < 2) return;
            
            // Check if death was detected and if we should save death climbs
            if (_wasDeathDetected && !Plugin.SaveDeathClimbs.Value)
            {
                _logger.LogInfo("Climb not saved: Player died during recording and SaveDeathClimbs is disabled.");
                _currentRecordedClimb = [];
                return;
            }

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
                    FlaggedReason = flaggedReason,
                    // Set death flag
                    WasDeathClimb = _wasDeathDetected
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
                
                if (newClimbData.WasDeathClimb)
                {
                    _logger.LogWarning($"[Death] Climb was saved where player died (will not be uploaded to cloud)");
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

        // Public method to be called when player dies (kept for Harmony patch compatibility)
        public void OnPlayerDeath()
        {
            if (IsRecording)
            {
                _wasDeathDetected = true;
                _logger.LogInfo("[Death] Player death detected during recording");
                
                // Save the death climb if configured to do so
                if (Plugin.SaveDeathClimbs.Value && _currentRecordedClimb.Count >= 2)
                {
                    _logger.LogInfo("[Death] Saving death climb as configured");
                    // We need to get the biome name - use "Death" as fallback if we can't determine it
                    string biomeName = GetCurrentBiomeName();
                    SaveCurrentClimb(biomeName);
                }
                else
                {
                    StopRecording();
                    _logger.LogInfo("[Death] Death climb not saved (SaveDeathClimbs is disabled or too few points)");
                }
            }
        }
        
        // Helper method to get current biome name
        private string GetCurrentBiomeName()
        {
            // Try to get biome name from current segment
            try
            {
                if (Zorro.Core.Singleton<MapHandler>.Instance != null)
                {
                    var currentSegment = Zorro.Core.Singleton<MapHandler>.Instance.GetCurrentSegment();
                    return System.Enum.GetName(typeof(Segment), currentSegment) ?? "Death";
                }
            }
            catch
            {
                // Fallback if we can't get the segment
            }
            return "Death";
        }

        private IEnumerator RecordClimbRoutine()
        {
            while (IsRecording)
            {
                // Check if player died
                if (Character.localCharacter != null && Character.localCharacter.data.dead)
                {
                    _wasDeathDetected = true;
                    _logger.LogInfo("[Death] Player death detected during recording");
                    StopRecording();
                    yield break;
                }
                
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
