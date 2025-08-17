using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using PeakPathfinder.Models;
using PeakPathfinder.Services;

namespace PeakPathfinder.Managers
{
    public class PathRecordingManager
    {
        private readonly PathDataService _pathDataService;
        private readonly ManualLogSource _logger;
        
        private List<Vector3> _currentRecordedPath = new List<Vector3>();
        private bool _isRecording = false;
        private float _recordingStartTime;
        private MonoBehaviour _coroutineRunner;

        public PathRecordingManager(PathDataService pathDataService, ManualLogSource logger, MonoBehaviour coroutineRunner)
        {
            _pathDataService = pathDataService;
            _logger = logger;
            _coroutineRunner = coroutineRunner;
        }

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            if (_isRecording) return;
            _isRecording = true;
            _currentRecordedPath.Clear();
            _recordingStartTime = Time.time;
            _logger.LogInfo("Pfad-Aufzeichnung gestartet!");
            _coroutineRunner.StartCoroutine(RecordPathRoutine());
        }

        public void StopRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;
            _logger.LogInfo($"Aufzeichnung gestoppt. {_currentRecordedPath.Count} Punkte zwischengespeichert.");
        }

        public void SaveCurrentPath(string biomeName)
        {
            StopRecording();
            if (_currentRecordedPath.Count < 2) return;
            
            var newPathData = new PathData
            {
                Id = Guid.NewGuid(),
                CreationTime = DateTime.Now,
                BiomeName = biomeName ?? "Unbekannt",
                DurationInSeconds = Time.time - _recordingStartTime,
                Points = _currentRecordedPath.Select(vec => new SerializableVector3(vec)).ToList()
            };
            
            // Generate share code for the new path
            newPathData.GenerateShareCode();
            
            _pathDataService.AddPath(newPathData);
            _pathDataService.SavePathsToFile(false);
            
            // Show tag selection popup for successful paths
            ShowTagSelectionForNewPath(newPathData);
        }
        
        private void ShowTagSelectionForNewPath(PathData pathData)
        {
            // We'll trigger this through the Plugin to show the tag selection UI
            _logger.LogInfo($"Neuer Pfad erstellt: {pathData.GetDisplayName()} (Code: {pathData.ShareCode})");
            
            // Trigger tag selection in the UI
            Plugin.Instance.ShowTagSelectionForNewPath(pathData);
        }

        private IEnumerator RecordPathRoutine()
        {
            while (_isRecording)
            {
                if (Camera.main != null) 
                    _currentRecordedPath.Add(Camera.main.transform.position);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}