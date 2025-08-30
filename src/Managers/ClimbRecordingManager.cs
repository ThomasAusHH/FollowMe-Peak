using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using FollowMePeak.Models;
using FollowMePeak.Services;

namespace FollowMePeak.Managers
{
    public class ClimbRecordingManager(
        ClimbDataService climbDataService,
        ManualLogSource logger,
        MonoBehaviour coroutineRunner)
    {
        private string _currentBiome = string.Empty;
        private List<Vector3> _currentRecordedClimb = new List<Vector3>();
        private float _recordingStartTime;

        public bool IsRecording { get; private set; } = false;

        public void StartRecording(string biomeName)
        {
            if (IsRecording && _currentBiome == biomeName) return;
            IsRecording = true;
            _currentBiome = biomeName ?? "Unknown";
            _currentRecordedClimb = [];
            _recordingStartTime = Time.time;
            logger.LogInfo($"Kletter-Aufzeichnung gestartet im Biome {biomeName}!");
            coroutineRunner.StartCoroutine(RecordClimbRoutine());
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            logger.LogInfo($"Aufzeichnung gestoppt. {_currentRecordedClimb.Count} Punkte zwischengespeichert.");
        }

        public void SaveCurrentClimb()
        {
            StopRecording();

            var biome = _currentBiome;
            var currentClimb = _currentRecordedClimb;
            if (currentClimb.Count < 2) return;

            // Reset the current recorded climb
            // (but don't clear the list, we need that list to save it; instead assign a new empty list)
            _currentRecordedClimb = [];

            var durationInSeconds = Time.time - _recordingStartTime;

            BepInEx.ThreadingHelper.Instance.StartAsyncInvoke(CreateClimbData);
            return;

            Action CreateClimbData()
            {
                var newClimbData = new ClimbData
                {
                    Id = Guid.NewGuid(),
                    CreationTime = DateTime.Now,
                    BiomeName = biome,
                    DurationInSeconds = durationInSeconds,
                    Points = currentClimb,
                    AscentLevel = Ascents.currentAscent,
                };

                // Generate share code for the new climb
                newClimbData.GenerateShareCode();
                return () => AfterClimbIsCreated(newClimbData);
            }

            void AfterClimbIsCreated(ClimbData newClimbData)
            {
                logger.LogInfo($"Climb saved with ascent level: {newClimbData.AscentLevel}");

                climbDataService.AddClimb(newClimbData);
                climbDataService.SaveClimbsToFile(false);
            
                // Show tag selection popup for successful climbs
                ShowTagSelectionForNewClimb(newClimbData);
            }
        }
        
        private void ShowTagSelectionForNewClimb(ClimbData climbData)
        {
            // We'll trigger this through the Plugin to show the tag selection UI
            logger.LogInfo($"Neue Kletterroute erstellt: {climbData.GetDisplayName()} (Code: {climbData.ShareCode})");
            
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
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
