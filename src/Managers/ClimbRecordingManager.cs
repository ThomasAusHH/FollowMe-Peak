using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FollowMePeak.Models;
using FollowMePeak.Services;
using FollowMePeak.Utils;
using Zorro.Core;

namespace FollowMePeak.Managers
{
    public class ClimbRecordingManager
    {
        private readonly ClimbDataService _climbDataService;
        private readonly ModLogger _logger;
        
        private List<Vector3> _currentRecordedClimb = new List<Vector3>();
        private float _recordingStartTime;
        private MonoBehaviour _coroutineRunner;
        private bool _wasDeathDetected = false;
        
        // Static flag to track if death has occurred in this session
        // This prevents helicopter detection after death
        public static bool PlayerDiedThisSession { get; private set; } = false;

        public bool IsRecording { get; private set; } = false;
        public ClimbRecordingManager(ClimbDataService climbDataService, ModLogger logger, MonoBehaviour coroutineRunner)
        {
            _climbDataService = climbDataService;
            _logger = logger;
            _coroutineRunner = coroutineRunner;
        }

        public void StartRecording()
        {
            if (IsRecording) 
            {
                _logger.Warning("Recording already active - stopping previous recording before starting new one");
                StopRecording();
            }
            
            // Check if we're in a valid level
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("Level_"))
            {
                _logger.Warning("StartRecording called but not in a Level scene");
                return;
            }
            
            IsRecording = true;
            _currentRecordedClimb = [];
            _recordingStartTime = Time.time;
            _wasDeathDetected = false;
            
            // Reset static death flag when starting a new recording (new level/run)
            PlayerDiedThisSession = false;
            
            // Reset Fly Detection for new recording (safety reset)
            Detection.SimpleFlyDetector.ResetForNewRecording();
            
            _logger.Info("[ClimbRecording] Started by RUN STARTED event!");
            _coroutineRunner.StartCoroutine(RecordClimbRoutine());
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            
            // Clear recorded data if we have less than minimum points (incomplete climb)
            if (_currentRecordedClimb.Count < 2)
            {
                _logger.Info($"Recording stopped with insufficient data ({_currentRecordedClimb.Count} points) - clearing");
                _currentRecordedClimb = new List<Vector3>();
            }
            else
            {
                _logger.Info($"Recording stopped. {_currentRecordedClimb.Count} points cached for potential save.");
            }
        }

        public void SaveCurrentClimb(string biomeName)
        {
            // Special handling for Peak ending (helicopter)
            if (biomeName == "Peak")
            {
                _logger.Info("[Helicopter] Saving Peak climb from helicopter ending");
            }
            
            StopRecording();

            var currentClimb = _currentRecordedClimb;
            if (currentClimb.Count < 2) return;
            
            // Check if death was detected and if we should save death climbs
            if (_wasDeathDetected && !Plugin.SaveDeathClimbs.Value)
            {
                _logger.Info("Climb not saved: Player died during recording and SaveDeathClimbs is disabled.");
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
            _logger.Info($"[FlyDetection] Captured before async: Flagged={wasFlagged}, Score={flaggedScore}, Reason={flaggedReason}");

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
                _logger.Info($"Climb saved with ascent level: {newClimbData.AscentLevel}");
                
                // Log detection state if flagged
                if (newClimbData.WasFlagged)
                {
                    _logger.Warning($"[FlyDetection] Climb was flagged: Score={newClimbData.FlaggedScore}, Reason={newClimbData.FlaggedReason}");
                }
                
                if (newClimbData.WasDeathClimb)
                {
                    _logger.Warning($"[Death] Climb was saved where player died (will not be uploaded to cloud)");
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
            _logger.Info($"Neue Kletterroute erstellt: {climbData.GetDisplayName()} (Code: {climbData.ShareCode})");
            
            // Trigger tag selection in the UI
            Plugin.Instance.ShowTagSelectionForNewClimb(climbData);
        }

        // Public method to be called when player dies (kept for Harmony patch compatibility)
        public void OnPlayerDeath()
        {
            // Set static flag immediately to prevent helicopter detection
            PlayerDiedThisSession = true;
            _logger.Info("[Death] Death flag set - helicopter detection disabled");
            
            if (IsRecording)
            {
                _wasDeathDetected = true;
                _logger.Info("[Death] Player death detected during recording");
                
                // Save the death climb if configured to do so
                if (Plugin.SaveDeathClimbs.Value && _currentRecordedClimb.Count >= 2)
                {
                    _logger.Info("[Death] Saving death climb as configured");
                    // We need to get the biome name - use "Death" as fallback if we can't determine it
                    string biomeName = GetCurrentBiomeName();
                    SaveCurrentClimb(biomeName);
                }
                else
                {
                    StopRecording();
                    _logger.Info("[Death] Death climb not saved (SaveDeathClimbs is disabled or too few points)");
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
                    _logger.Info("[Death] Player death detected during recording");
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
