using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;
using FollowMePeak.Utils;

namespace FollowMePeak.Detection
{
    public static class SimpleFlyDetector
    {
        // Logger removed - using ModLogger.Instance instead
        private static float _detectionScore = 0f;
        private static string _lastReason = "No detection performed";
        private static List<string> _activeFlags = new List<string>();
        
        // Detection state
        private static float _lastCheckTime = 0f;
        private static float _checkInterval = FlyDetectionConfig.DetectionCheckInterval;
        
        // Detailed logging
        private static float _lastDetailedLogTime = 0f;
        private static float _detailedLogInterval = 1.5f;
        
        // State tracking for detection logic
        private static Dictionary<int, bool> _lastGravityStates = new Dictionary<int, bool>();
        private static Vector3 _lastVelocity;
        private static bool _hasLastVelocity = false;
        private static int _consecutiveSustainedVelocityFrames = 0;
        private static int _consecutiveKinematicFrames = 0;
        
        // Cached per-character data (avoids scene scans and repeated allocations)
        private static GameObject _cachedCharacterObject;
        private static Rigidbody[] _cachedRigidbodies;
        
        // Cached reflection lookups for IsPlayerInExemptState
        private static bool _reflectionInitialized = false;
        private static System.Reflection.FieldInfo _characterDataField;
        private static System.Reflection.FieldInfo _isVineClimbingField;
        private static System.Reflection.FieldInfo _isRopeClimbingField;
        private static System.Reflection.FieldInfo _fallSecondsField;

        // Run start tracking
        private static float _runStartTime = -1f;
        private static bool _detectionEnabled = false;
        private static string _lastSceneName = "";
        private static bool _isInValidLevel = false;
        
        public static bool IsFlyDetected { get; private set; }
        public static float DetectionScore => _detectionScore;
        public static string LastDetectionReason => _lastReason;
        
        // Sticky flags for current recording
        public static bool WasDetectedInCurrentRecording { get; private set; }
        public static float MaxScoreInCurrentRecording { get; private set; }
        public static string ReasonForCurrentRecording { get; private set; } = "No detection in this recording";
        
        static SimpleFlyDetector()
        {
            // Configuration is now loaded from FlyDetectionConfig constants
            _checkInterval = FlyDetectionConfig.DetectionCheckInterval;
        }
        
        /// <summary>
        /// Called when the run starts to enable detection
        /// </summary>
        public static void OnRunStarted()
        {
            if (!_isInValidLevel) 
            {
                ModLogger.Instance?.Warning("[FlyDetection] OnRunStarted called but not in valid level");
                return;
            }
            
            _runStartTime = Time.time;
            _detectionEnabled = true;
            ModLogger.Instance?.Info("[FlyDetection] Detection activated by RUN STARTED event");
            
            // Reset detection state
            _detectionScore = 0f;
            IsFlyDetected = false;
            _activeFlags.Clear();
            _lastReason = "Detection just started";
        }
        
        /// <summary>
        /// Führt die Fly-Mod-Erkennung durch. Sollte regelmäßig (z.B. in Update) aufgerufen werden.
        /// </summary>
        public static void PerformDetection()
        {
            if (!_isInValidLevel) return;
            
            // Check if detection is enabled
            if (!_detectionEnabled)
            {
                _lastReason = "Waiting for RUN STARTED event";
                return;
            }
            
            // Rate limit checks
            if (Time.time - _lastCheckTime < _checkInterval) return;
            _lastCheckTime = Time.time;
            
            _activeFlags.Clear();
            float score = 0;
            
            bool doDetailedLog = (Time.time - _lastDetailedLogTime > _detailedLogInterval);
            if (doDetailedLog) _lastDetailedLogTime = Time.time;
            
            // Only check the local player. A full scene scan via FindObjectsByType<GameObject>()
            // causes main-thread spikes (micro-stuttering), and remote players must never
            // flag our own recordings anyway.
            var character = Character.localCharacter;
            if (character == null)
            {
                _lastReason = "No local character available";
                return;
            }
            
            var go = character.gameObject;
            if (!go.activeInHierarchy) return;

            Vector3 pos = go.transform.position;
            if (pos == Vector3.zero || pos.y > 1000f || pos.y < -100f) return;

            // The rigidbody set of a character is stable - cache it to avoid
            // GetComponentsInChildren allocations on every check
            if (go != _cachedCharacterObject)
            {
                _cachedCharacterObject = go;
                _cachedRigidbodies = go.GetComponentsInChildren<Rigidbody>();
                _lastGravityStates.Clear();
            }
            var rigidbodies = _cachedRigidbodies;
            if (rigidbodies == null || rigidbodies.Length == 0) return;

            int kinematicCount = 0;
            int gravityJustEnabledCount = 0;

            // Logik zur Erkennung von Kanonen-Abschüssen
            foreach (var rb in rigidbodies)
            {
                if (rb == null) continue;
                if (rb.isKinematic) kinematicCount++;

                int rbId = rb.GetInstanceID();
                bool currentGravity = rb.useGravity;
                if (_lastGravityStates.TryGetValue(rbId, out bool lastGravity))
                {
                    if (!lastGravity && currentGravity)
                    {
                        gravityJustEnabledCount++;
                    }
                }
                _lastGravityStates[rbId] = currentGravity;
            }

            // Kanonen-Erkennung
            bool isCannonLaunch = gravityJustEnabledCount > 10; 
            if (isCannonLaunch && doDetailedLog)
            {
                ModLogger.Instance?.Info($"[FlyDetection] Cannon launch detected! Pausing velocity checks.");
            }
            
            // *** NEU: Aufruf der zentralen Ausnahme-Prüfung ***
            bool isPlayerExempt = IsPlayerInExemptState(character);
            if (isPlayerExempt && doDetailedLog)
            {
                ModLogger.Instance?.Info($"[FlyDetection] Player is exempt (climbing/falling). Pausing velocity checks.");
            }

            // --- Detektionslogik ---

            // 1. Kinematic Check (läuft immer)
            if (kinematicCount > 8)
            {
                score += 60;
                _activeFlags.Add($"Kinematic mode on {kinematicCount} RBs");
            }
            
            if (kinematicCount == 18)
            {
                _consecutiveKinematicFrames++;
                if (_consecutiveKinematicFrames > 3)
                {
                    score += 25;
                    _activeFlags.Add($"Persistent full kinematic mode ({_consecutiveKinematicFrames} frames)");
                }
            }
            else
            {
                _consecutiveKinematicFrames = 0;
            }
            
            // 2. Velocity Checks (nur wenn keine Ausnahme vorliegt)
            // *** GEÄNDERT: Bedingung um isPlayerExempt erweitert ***
            if (!isCannonLaunch && !isPlayerExempt)
            {
                var mainRb = rigidbodies[0];
                if (mainRb != null)
                {
                    Vector3 currentVelocity = mainRb.linearVelocity;
                    float verticalSpeed = currentVelocity.y;
                    float horizontalSpeed = new Vector2(currentVelocity.x, currentVelocity.z).magnitude;

                    // Direkte Geschwindigkeits-Checks
                    if (horizontalSpeed > 15f)
                    {
                        score += 50;
                        _activeFlags.Add($"High horizontal speed: {horizontalSpeed:F1} m/s");
                    }
                    if (verticalSpeed > 15f)
                    {
                        score += 50;
                        _activeFlags.Add($"High vertical speed: {verticalSpeed:F1} m/s");
                    }

                    // Analyse für unnatürlich konstante Bewegung
                    bool isSustainedHorizontal = horizontalSpeed > 4f && Math.Abs(verticalSpeed) < 0.5f;
                    bool isSustainedVertical = Math.Abs(verticalSpeed) > 4f && horizontalSpeed < 0.5f;
                    bool isSustainedDiagonal = horizontalSpeed > 3f && verticalSpeed > 3f;

                    if ((isSustainedHorizontal || isSustainedVertical || isSustainedDiagonal)
                        && _hasLastVelocity && Vector3.Distance(currentVelocity, _lastVelocity) < 1.0f)
                    {
                        _consecutiveSustainedVelocityFrames++;
                    }
                    else
                    {
                        _consecutiveSustainedVelocityFrames = 0;
                    }
                    
                    if (_consecutiveSustainedVelocityFrames > 4)
                    {
                        score += 55;
                        _activeFlags.Add($"Unnaturally sustained velocity for {_consecutiveSustainedVelocityFrames * _checkInterval:F1}s");
                    }
                    
                    _lastVelocity = currentVelocity;
                    _hasLastVelocity = true;
                }
            }

            if (doDetailedLog)
            {
                 ModLogger.Instance?.Info($"[Debug] {go.name} | Kinematic: {kinematicCount} | Score: {score:F0}");
            }
            
            // Final score and detection update
            _detectionScore = Mathf.Min(score, 100);
            float threshold = FlyDetectionConfig.DetectionThreshold;
            bool wasDetected = IsFlyDetected;
            IsFlyDetected = _detectionScore >= threshold;
            
            if (_activeFlags.Count > 0)
            {
                _lastReason = string.Join(", ", _activeFlags);
            }
            else
            {
                _lastReason = "No anomalies detected";
            }
            
            if (IsFlyDetected && !wasDetected)
            {
                // Set sticky flag for current recording
                WasDetectedInCurrentRecording = true;
                MaxScoreInCurrentRecording = Math.Max(MaxScoreInCurrentRecording, _detectionScore);
                ReasonForCurrentRecording = _lastReason;
                LogDetection();
            }
            
            // Update max score if already detected
            if (WasDetectedInCurrentRecording && _detectionScore > MaxScoreInCurrentRecording)
            {
                MaxScoreInCurrentRecording = _detectionScore;
                ReasonForCurrentRecording = _lastReason;
            }
        }
        
        /// <summary>
        /// Gibt an, ob ein Climb aufgrund einer Fly-Mod-Erkennung geflaggt werden soll.
        /// </summary>
        public static bool ShouldFlagClimb()
        {
            // Use configuration from FlyDetectionConfig
            bool isEnabled = FlyDetectionConfig.IsEnabled;
            bool shouldAutoFlag = FlyDetectionConfig.ShouldAutoFlagClimbs;

            return isEnabled && shouldAutoFlag && IsFlyDetected;
        }

        // *** NEU: Zentrale Funktion zur Prüfung von Spielmechanik-Ausnahmen ***
        /// <summary>
        /// Prüft, ob sich der Spieler in einem Zustand befindet, der hohe Geschwindigkeiten erlaubt.
        /// Deckt Klettern (Liane/Seil) und erzwungene Fallzustände (Wirbelsturm) ab.
        /// Reflection-Lookups werden einmalig gecacht, um Per-Check-Overhead zu vermeiden.
        /// </summary>
        /// <param name="characterComponent">Der Character des lokalen Spielers.</param>
        /// <returns>True, wenn die Geschwindigkeitsprüfung übersprungen werden soll.</returns>
        private static bool IsPlayerInExemptState(Character characterComponent)
        {
            try
            {
                if (!_reflectionInitialized)
                {
                    _reflectionInitialized = true;
                    _characterDataField = characterComponent.GetType().GetField("data");
                    if (_characterDataField != null)
                    {
                        var dataType = _characterDataField.FieldType;
                        _isVineClimbingField = dataType.GetField("isVineClimbing");
                        _isRopeClimbingField = dataType.GetField("isRopeClimbing");
                        _fallSecondsField = dataType.GetField("fallSeconds");
                    }
                }
                
                if (_characterDataField == null) return false;
                
                object dataObject = _characterDataField.GetValue(characterComponent);
                if (dataObject == null) return false;

                // 1. Prüfung für Liane/Seil
                bool isClimbingVine = _isVineClimbingField != null && (bool)_isVineClimbingField.GetValue(dataObject);
                bool isClimbingRope = _isRopeClimbingField != null && (bool)_isRopeClimbingField.GetValue(dataObject);

                // 2. Prüfung für Wirbelsturm (erzwungener Fall)
                bool isFallingForced = false;
                if (_fallSecondsField != null)
                {
                    float fallSeconds = (float)_fallSecondsField.GetValue(dataObject);
                    isFallingForced = fallSeconds > 0f;
                }

                // Alle Ausnahmen kombinieren: Wenn eine davon zutrifft, wird die Erkennung pausiert.
                return isClimbingVine || isClimbingRope || isFallingForced;
            }
            catch (Exception ex)
            {
                ModLogger.Instance?.Error($"[FlyDetection] Fehler beim Prüfen des Ausnahme-Status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setzt den Zustand der Erkennung beim Laden einer neuen Szene zurück.
        /// </summary>
        public static void OnSceneChanged(string sceneName)
        {
            ModLogger.Instance?.Info($"[FlyDetection] Scene changed to {sceneName}, resetting state.");
            
            _lastGravityStates.Clear();
            _consecutiveKinematicFrames = 0;
            _consecutiveSustainedVelocityFrames = 0;
            _hasLastVelocity = false;
            _cachedCharacterObject = null;
            _cachedRigidbodies = null;

            _isInValidLevel = sceneName.StartsWith("Level_");
            
            if (!_isInValidLevel)
            {
                _detectionEnabled = false;
                IsFlyDetected = false;
                _detectionScore = 0;
                _activeFlags.Clear();
                _runStartTime = -1f;
            }
            else if (sceneName != _lastSceneName)
            {
                _detectionEnabled = false;  // Wait for RUN STARTED
                _runStartTime = -1f;
                IsFlyDetected = false;
                _detectionScore = 0;
                _activeFlags.Clear();
                ModLogger.Instance?.Info("[FlyDetection] New level loaded - waiting for RUN STARTED event");
            }
            _lastSceneName = sceneName;
        }
        
        /// <summary>
        /// Resets the detection flags for a new recording (new biome)
        /// </summary>
        public static void ResetForNewRecording()
        {
            ModLogger.Instance?.Info("[FlyDetection] Reset for new recording");
            WasDetectedInCurrentRecording = false;
            MaxScoreInCurrentRecording = 0f;
            ReasonForCurrentRecording = "No detection in this recording";
            // IsFlyDetected remains as is (live status)
        }

        private static void LogDetection()
        {
            ModLogger.Instance?.Warning("======================================");
            ModLogger.Instance?.Warning(">>> FLY MOD DETECTED <<<");
            ModLogger.Instance?.Warning($"Score: {_detectionScore}/100 | Reason: {_lastReason}");
            ModLogger.Instance?.Warning("======================================");
        }
    }
}