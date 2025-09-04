using HarmonyLib;
using BepInEx.Logging;
using System.Reflection;
using System.Linq;
using Zorro.Core;
using FollowMePeak.Utils;

namespace FollowMePeak.Patches
{
    /// <summary>
    /// Patch for detecting helicopter ending at Peak (TheKiln segment) with multiple fallback approaches
    /// </summary>
    public static class EndGamePatch
    {
        // Use ModLogger.Instance instead of own logger
        private static bool _gameEndingDetected = false;
        
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                // Try multiple approaches to patch the end game
                
                // Approach 1: Try to patch Character.EndGame directly
                if (TryPatchCharacterEndGame(harmony))
                {
                    ModLogger.Instance?.Info("Successfully patched Character.EndGame");
                    return;
                }
                
                // Approach 2: Try to patch RunManager.EndGame as alternative
                if (TryPatchRunManagerEndGame(harmony))
                {
                    ModLogger.Instance?.Info("Successfully patched RunManager.EndGame");
                    return;
                }
                
                // Approach 3: Try to patch PeakSequence.CheckGameComplete as fallback
                if (TryPatchPeakSequence(harmony))
                {
                    ModLogger.Instance?.Info("Successfully patched PeakSequence.CheckGameComplete as fallback");
                    return;
                }
                
                ModLogger.Instance?.Error("Failed to apply any EndGame patches - Peak ending detection will not work");
            }
            catch (System.Exception ex)
            {
                ModLogger.Instance?.Error($"Failed to patch EndGame: {ex}");
            }
        }
        
        private static bool TryPatchCharacterEndGame(Harmony harmony)
        {
            try
            {
                var characterType = System.Type.GetType("Character, Assembly-CSharp");
                if (characterType == null)
                {
                    ModLogger.Instance?.Warning("Could not find Character type");
                    return false;
                }
                
                // Log all methods for debugging
                var allMethods = characterType.GetMethods(
                    BindingFlags.Public | 
                    BindingFlags.NonPublic | 
                    BindingFlags.Instance | 
                    BindingFlags.Static
                );
                
                ModLogger.Instance?.Info($"Found {allMethods.Length} methods in Character class");
                
                // Look for EndGame with various binding flags
                var endGameMethod = allMethods.FirstOrDefault(m => m.Name == "EndGame");
                
                if (endGameMethod == null)
                {
                    // Log some method names for debugging
                    var methodNames = allMethods.Select(m => m.Name).Distinct().Take(20);
                    ModLogger.Instance?.Warning($"EndGame not found. Sample methods: {string.Join(", ", methodNames)}");
                    return false;
                }
                
                ModLogger.Instance?.Info($"Found EndGame method: {endGameMethod.Name}, IsPublic: {endGameMethod.IsPublic}");
                
                var prefixMethod = typeof(EndGamePatch).GetMethod(
                    nameof(CharacterEndGamePrefix), 
                    BindingFlags.Static | BindingFlags.Public
                );
                
                // Use lower priority to ensure death detection runs first
                var harmonyMethod = new HarmonyMethod(prefixMethod) { priority = Priority.Low };
                harmony.Patch(endGameMethod, prefix: harmonyMethod);
                return true;
            }
            catch (System.Exception ex)
            {
                ModLogger.Instance?.Error($"TryPatchCharacterEndGame failed: {ex}");
                return false;
            }
        }
        
        private static bool TryPatchRunManagerEndGame(Harmony harmony)
        {
            try
            {
                var runManagerType = System.Type.GetType("RunManager, Assembly-CSharp");
                if (runManagerType == null)
                {
                    ModLogger.Instance?.Warning("Could not find RunManager type");
                    return false;
                }
                
                // Try to find EndGame method in RunManager
                var endGameMethod = runManagerType.GetMethod(
                    "EndGame",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );
                
                if (endGameMethod == null)
                {
                    ModLogger.Instance?.Warning("Could not find RunManager.EndGame method");
                    return false;
                }
                
                ModLogger.Instance?.Info("Found RunManager.EndGame method");
                
                var prefixMethod = typeof(EndGamePatch).GetMethod(
                    nameof(RunManagerEndGamePrefix),
                    BindingFlags.Static | BindingFlags.Public
                );
                
                // Use lower priority to ensure death detection runs first
                var harmonyMethod = new HarmonyMethod(prefixMethod) { priority = Priority.Low };
                harmony.Patch(endGameMethod, prefix: harmonyMethod);
                return true;
            }
            catch (System.Exception ex)
            {
                ModLogger.Instance?.Error($"TryPatchRunManagerEndGame failed: {ex}");
                return false;
            }
        }
        
        private static bool TryPatchPeakSequence(Harmony harmony)
        {
            try
            {
                var peakSequenceType = System.Type.GetType("PeakSequence, Assembly-CSharp");
                if (peakSequenceType == null)
                {
                    ModLogger.Instance?.Warning("Could not find PeakSequence type");
                    return false;
                }
                
                // Find CheckGameComplete method where EndGame is called
                var checkGameCompleteMethod = peakSequenceType.GetMethod(
                    "CheckGameComplete",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                
                if (checkGameCompleteMethod == null)
                {
                    ModLogger.Instance?.Warning("Could not find CheckGameComplete method");
                    return false;
                }
                
                ModLogger.Instance?.Info("Found CheckGameComplete method");
                
                var postfixMethod = typeof(EndGamePatch).GetMethod(
                    nameof(PeakSequenceCheckGameCompletePostfix),
                    BindingFlags.Static | BindingFlags.Public
                );
                
                // Use lower priority to ensure death detection runs first
                var harmonyMethod = new HarmonyMethod(postfixMethod) { priority = Priority.Low };
                harmony.Patch(checkGameCompleteMethod, postfix: harmonyMethod);
                return true;
            }
            catch (System.Exception ex)
            {
                ModLogger.Instance?.Error($"TryPatchPeakSequence failed: {ex}");
                return false;
            }
        }
        
        public static void CharacterEndGamePrefix()
        {
            // Check if we're actually at TheKiln segment (helicopter ending)
            try
            {
                // First check if player has already died this session
                if (Managers.ClimbRecordingManager.PlayerDiedThisSession)
                {
                    ModLogger.Instance?.Info("[EndGamePatch] EndGame called but player already died - not a helicopter ending");
                    return;
                }
                
                // Check if player is actually dead (additional safety check)
                if (Character.localCharacter != null && Character.localCharacter.data.dead)
                {
                    ModLogger.Instance?.Info("[EndGamePatch] EndGame called but player is dead - not a helicopter ending");
                    return;
                }
                
                var mapHandler = Zorro.Core.Singleton<MapHandler>.Instance;
                if (mapHandler != null)
                {
                    var currentSegment = mapHandler.GetCurrentSegment();
                    ModLogger.Instance?.Info($"[EndGamePatch] Character.EndGame called in segment: {currentSegment}");
                    
                    // Helicopter ending occurs in TheKiln segment AND player must be alive
                    if (currentSegment == Segment.TheKiln)
                    {
                        ModLogger.Instance?.Info("[EndGamePatch] Helicopter ending detected at TheKiln (Peak)!");
                        NotifyPlugin();
                    }
                    else
                    {
                        ModLogger.Instance?.Info($"[EndGamePatch] EndGame in {currentSegment} - not at Peak");
                    }
                }
                else
                {
                    ModLogger.Instance?.Warning("[EndGamePatch] MapHandler not available, cannot verify segment");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Instance?.Error($"[EndGamePatch] Error checking segment: {ex.Message}");
            }
        }
        
        public static void RunManagerEndGamePrefix()
        {
            // Check if we're actually at TheKiln segment (helicopter ending)
            try
            {
                // First check if player has already died this session
                if (Managers.ClimbRecordingManager.PlayerDiedThisSession)
                {
                    ModLogger.Instance?.Info("[EndGamePatch] RunManager.EndGame called but player already died - not a helicopter ending");
                    return;
                }
                
                // Check if player is actually dead (additional safety check)
                if (Character.localCharacter != null && Character.localCharacter.data.dead)
                {
                    ModLogger.Instance?.Info("[EndGamePatch] RunManager.EndGame called but player is dead - not a helicopter ending");
                    return;
                }
                
                var mapHandler = Zorro.Core.Singleton<MapHandler>.Instance;
                if (mapHandler != null)
                {
                    var currentSegment = mapHandler.GetCurrentSegment();
                    ModLogger.Instance?.Info($"[EndGamePatch] RunManager.EndGame called in segment: {currentSegment}");
                    
                    // Helicopter ending occurs in TheKiln segment AND player must be alive
                    if (currentSegment == Segment.TheKiln)
                    {
                        ModLogger.Instance?.Info("[EndGamePatch] Helicopter ending detected at TheKiln (Peak)!");
                        NotifyPlugin();
                    }
                    else
                    {
                        ModLogger.Instance?.Info($"[EndGamePatch] EndGame in {currentSegment} - not at Peak");
                    }
                }
                else
                {
                    ModLogger.Instance?.Warning("[EndGamePatch] MapHandler not available, cannot verify segment");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Instance?.Error($"[EndGamePatch] Error checking segment: {ex.Message}");
            }
        }
        
        public static void PeakSequenceCheckGameCompletePostfix(ref bool ___endingGame)
        {
            // Check if game just ended
            if (___endingGame && !_gameEndingDetected)
            {
                // First check if player has already died this session
                if (Managers.ClimbRecordingManager.PlayerDiedThisSession)
                {
                    ModLogger.Instance?.Info("[EndGamePatch] PeakSequence ending but player already died - not a helicopter ending");
                    return;
                }
                
                // Check if player is actually dead (additional safety check)
                if (Character.localCharacter != null && Character.localCharacter.data.dead)
                {
                    ModLogger.Instance?.Info("[EndGamePatch] PeakSequence ending but player is dead - not a helicopter ending");
                    return;
                }
                
                // Since this is PeakSequence, we know we're at Peak, but let's verify anyway
                try
                {
                    var mapHandler = Zorro.Core.Singleton<MapHandler>.Instance;
                    if (mapHandler != null)
                    {
                        var currentSegment = mapHandler.GetCurrentSegment();
                        ModLogger.Instance?.Info($"[EndGamePatch] PeakSequence ending in segment: {currentSegment}");
                        
                        // Helicopter ending occurs in TheKiln segment AND player must be alive
                        if (currentSegment == Segment.TheKiln)
                        {
                            _gameEndingDetected = true;
                            ModLogger.Instance?.Info("[EndGamePatch] PeakSequence helicopter ending confirmed at TheKiln!");
                            NotifyPlugin();
                        }
                    }
                    else
                    {
                        // If MapHandler not available but we're in PeakSequence and player is alive, assume it's valid
                        _gameEndingDetected = true;
                        ModLogger.Instance?.Info("[EndGamePatch] PeakSequence ending detected (MapHandler unavailable)");
                        NotifyPlugin();
                    }
                }
                catch (System.Exception ex)
                {
                    ModLogger.Instance?.Error($"[EndGamePatch] Error in PeakSequence check: {ex.Message}");
                    // If error but we're in PeakSequence and player is alive, assume it's valid
                    _gameEndingDetected = true;
                    NotifyPlugin();
                }
            }
        }
        
        private static void NotifyPlugin()
        {
            Plugin.Instance?.OnHelicopterEnding();
        }
    }
}