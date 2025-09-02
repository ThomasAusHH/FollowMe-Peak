using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;

namespace FollowMePeak.Detection
{
    public static class SimpleFlyDetector
    {
        private static ManualLogSource _logger;
        private static float _detectionScore = 0f;
        private static string _lastReason = "No detection performed";
        private static List<string> _activeFlags = new List<string>();
        
        // Detection state
        private static float _lastCheckTime = 0f;
        private static float _checkInterval = 0.5f;
        
        // Detailed logging
        private static float _lastDetailedLogTime = 0f;
        private static float _detailedLogInterval = 1.5f;
        
        // State tracking for detection logic
        private static Dictionary<string, bool> _lastGravityStates = new Dictionary<string, bool>();
        private static Dictionary<string, Vector3> _lastVelocities = new Dictionary<string, Vector3>();
        private static Dictionary<string, int> _consecutiveSustainedVelocityFrames = new Dictionary<string, int>();
        private static int _consecutiveKinematicFrames = 0;

        // Spawn protection
        private static float _gameStartTime = -1f;
        private static float _spawnGracePeriod = 10f;
        private static bool _isInGracePeriod = true;
        private static string _lastSceneName = "";
        private static bool _isInValidLevel = false;
        
        public static bool IsFlyDetected { get; private set; }
        public static float DetectionScore => _detectionScore;
        public static string LastDetectionReason => _lastReason;
        
        static SimpleFlyDetector()
        {
            _logger = BepInEx.Logging.Logger.CreateLogSource("SimpleFlyDetector");
            
            // Lade hier deine Konfiguration (z.B. aus einer statischen Klasse)
            // _checkInterval = YourConfigClass.DetectionCheckInterval;
            // _spawnGracePeriod = YourConfigClass.GracePeriod;
        }
        
        /// <summary>
        /// Führt die Fly-Mod-Erkennung durch. Sollte regelmäßig (z.B. in Update) aufgerufen werden.
        /// </summary>
        public static void PerformDetection()
        {
            if (!_isInValidLevel) return;

            // Grace period management
            if (_gameStartTime < 0)
            {
                _gameStartTime = Time.time;
                _isInGracePeriod = true;
                _logger.LogInfo($"[FlyDetection] Grace period started ({_spawnGracePeriod} seconds)");
            }
            
            if (_isInGracePeriod)
            {
                if (Time.time - _gameStartTime < _spawnGracePeriod)
                {
                    _lastReason = $"Grace period active ({_spawnGracePeriod - (Time.time - _gameStartTime):F1}s remaining)";
                    return;
                }
                _isInGracePeriod = false;
                _logger.LogInfo("[FlyDetection] Grace period ended - detection active");
            }
            
            // Rate limit checks
            if (Time.time - _lastCheckTime < _checkInterval) return;
            _lastCheckTime = Time.time;
            
            _activeFlags.Clear();
            float score = 0;
            
            bool doDetailedLog = (Time.time - _lastDetailedLogTime > _detailedLogInterval);
            if (doDetailedLog) _lastDetailedLogTime = Time.time;
            
            var allGameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            
            foreach (var go in allGameObjects)
            {
                // Filter for relevant player objects
                if ((go.name.Contains("Character") || go.name.Contains("Player")) && 
                    !go.name.Contains("NPC") && !go.name.Contains("Ghost") && !go.name.Contains("Dummy") &&
                    !go.name.Contains("Ragdoll") && !go.name.Contains("Corpse") && !go.name.Contains("Dead") &&
                    go.activeInHierarchy)
                {
                    Vector3 pos = go.transform.position;
                    if (pos == Vector3.zero || pos.y > 1000f || pos.y < -100f) continue;

                    var rigidbodies = go.GetComponentsInChildren<Rigidbody>();
                    if (rigidbodies.Length == 0) continue;

                    int kinematicCount = 0;
                    int gravityJustEnabledCount = 0;

                    // Logik zur Erkennung von Kanonen-Abschüssen
                    foreach (var rb in rigidbodies)
                    {
                        if (rb.isKinematic) kinematicCount++;

                        string rbId = $"{go.name}_{rb.name}";
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
                        _logger.LogInfo($"[FlyDetection] Cannon launch detected! Pausing velocity checks.");
                    }
                    
                    // *** NEU: Aufruf der zentralen Ausnahme-Prüfung ***
                    bool isPlayerExempt = IsPlayerInExemptState(go);
                    if (isPlayerExempt && doDetailedLog)
                    {
                        _logger.LogInfo($"[FlyDetection] Player is exempt (climbing/falling). Pausing velocity checks.");
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
                        Vector3 currentVelocity = mainRb.linearVelocity;
                        float verticalSpeed = currentVelocity.y;
                        float horizontalSpeed = new Vector2(currentVelocity.x, currentVelocity.z).magnitude;

                        // Direkte Geschwindigkeits-Checks
                        if (horizontalSpeed > 10f)
                        {
                            score += 50;
                            _activeFlags.Add($"High horizontal speed: {horizontalSpeed:F1} m/s");
                        }
                        if (verticalSpeed > 10f)
                        {
                            score += 50;
                            _activeFlags.Add($"High vertical speed: {verticalSpeed:F1} m/s");
                        }

                        // Analyse für unnatürlich konstante Bewegung
                        bool isSustainedHorizontal = horizontalSpeed > 4f && Math.Abs(verticalSpeed) < 0.5f;
                        bool isSustainedVertical = Math.Abs(verticalSpeed) > 4f && horizontalSpeed < 0.5f;
                        bool isSustainedDiagonal = horizontalSpeed > 3f && verticalSpeed > 3f;

                        string goName = go.name;
                        if (!_consecutiveSustainedVelocityFrames.ContainsKey(goName))
                        {
                            _consecutiveSustainedVelocityFrames[goName] = 0;
                        }

                        if (isSustainedHorizontal || isSustainedVertical || isSustainedDiagonal)
                        {
                            if (_lastVelocities.TryGetValue(goName, out Vector3 lastVelocity) && Vector3.Distance(currentVelocity, lastVelocity) < 1.0f)
                            {
                                _consecutiveSustainedVelocityFrames[goName]++;
                            }
                            else
                            {
                                _consecutiveSustainedVelocityFrames[goName] = 0;
                            }
                        }
                        else
                        {
                            _consecutiveSustainedVelocityFrames[goName] = 0;
                        }
                        
                        if (_consecutiveSustainedVelocityFrames[goName] > 4)
                        {
                            score += 55;
                            _activeFlags.Add($"Unnaturally sustained velocity for {_consecutiveSustainedVelocityFrames[goName] * _checkInterval:F1}s");
                        }
                        
                        _lastVelocities[goName] = currentVelocity;
                    }

                    if (doDetailedLog)
                    {
                         _logger.LogInfo($"[Debug] {go.name} | Kinematic: {kinematicCount} | Score: {score:F0}");
                    }
                }
            }
            
            // Final score and detection update
            _detectionScore = Mathf.Min(score, 100);
            float threshold = 50f; // Lade dies aus deiner Config
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
                LogDetection();
            }
        }
        
        /// <summary>
        /// Gibt an, ob ein Climb aufgrund einer Fly-Mod-Erkennung geflaggt werden soll.
        /// </summary>
        public static bool ShouldFlagClimb()
        {
            // Passe diese Werte entsprechend deiner Konfigurations-Klasse an.
            bool isEnabled = true; 
            bool shouldAutoFlag = true;

            return isEnabled && shouldAutoFlag && IsFlyDetected;
        }

        // *** NEU: Zentrale Funktion zur Prüfung von Spielmechanik-Ausnahmen ***
        /// <summary>
        /// Prüft, ob sich der Spieler in einem Zustand befindet, der hohe Geschwindigkeiten erlaubt.
        /// Deckt Klettern (Liane/Seil) und erzwungene Fallzustände (Wirbelsturm) ab.
        /// </summary>
        /// <param name="playerObject">Das GameObject des Spielers, das geprüft werden soll.</param>
        /// <returns>True, wenn die Geschwindigkeitsprüfung übersprungen werden soll.</returns>
        private static bool IsPlayerInExemptState(GameObject playerObject)
        {
            try
            {
                var characterComponent = playerObject.GetComponent("Character");
                if (characterComponent == null) return false;

                var dataField = characterComponent.GetType().GetField("data");
                if (dataField == null) return false;
                
                object dataObject = dataField.GetValue(characterComponent);
                if (dataObject == null) return false;

                // 1. Prüfung für Liane/Seil
                var isVineClimbingField = dataObject.GetType().GetField("isVineClimbing");
                var isRopeClimbingField = dataObject.GetType().GetField("isRopeClimbing");
                bool isClimbingVine = isVineClimbingField != null && (bool)isVineClimbingField.GetValue(dataObject);
                bool isClimbingRope = isRopeClimbingField != null && (bool)isRopeClimbingField.GetValue(dataObject);

                // 2. Prüfung für Wirbelsturm (erzwungener Fall)
                var fallSecondsField = dataObject.GetType().GetField("fallSeconds");
                bool isFallingForced = false;
                if (fallSecondsField != null)
                {
                    float fallSeconds = (float)fallSecondsField.GetValue(dataObject);
                    isFallingForced = fallSeconds > 0f;
                }

                // Alle Ausnahmen kombinieren: Wenn eine davon zutrifft, wird die Erkennung pausiert.
                return isClimbingVine || isClimbingRope || isFallingForced;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FlyDetection] Fehler beim Prüfen des Ausnahme-Status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setzt den Zustand der Erkennung beim Laden einer neuen Szene zurück.
        /// </summary>
        public static void OnSceneChanged(string sceneName)
        {
            _logger.LogInfo($"[FlyDetection] Scene changed to {sceneName}, resetting state.");
            
            _lastGravityStates.Clear();
            _consecutiveKinematicFrames = 0;
            _consecutiveSustainedVelocityFrames.Clear();
            _lastVelocities.Clear();

            _isInValidLevel = sceneName.StartsWith("Level_");
            
            if (!_isInValidLevel)
            {
                IsFlyDetected = false;
                _detectionScore = 0;
                _activeFlags.Clear();
                _gameStartTime = -1f;
            }
            else if (sceneName != _lastSceneName)
            {
                _gameStartTime = -1f; 
                _isInGracePeriod = true;
                IsFlyDetected = false;
                _detectionScore = 0;
                _activeFlags.Clear();
            }
            _lastSceneName = sceneName;
        }

        private static void LogDetection()
        {
            _logger.LogWarning("======================================");
            _logger.LogWarning(">>> FLY MOD DETECTED <<<");
            _logger.LogWarning($"Score: {_detectionScore}/100 | Reason: {_lastReason}");
            _logger.LogWarning("======================================");
        }
    }
}