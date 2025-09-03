using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;

namespace FollowMePeak.Patches
{
    public class PlayerDeathPatch
    {
        private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("PlayerDeathPatch");
        
        // Try to patch the RPCA_Die method using reflection
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                // Try to find Character class
                var characterType = Type.GetType("Character, Assembly-CSharp") ?? 
                                  AccessTools.TypeByName("Character");
                
                if (characterType != null)
                {
                    // Try to find RPCA_Die method
                    var rpca_die_method = AccessTools.Method(characterType, "RPCA_Die");
                    if (rpca_die_method != null)
                    {
                        var postfix = AccessTools.Method(typeof(PlayerDeathPatch), nameof(OnPlayerDied_Postfix));
                        harmony.Patch(rpca_die_method, postfix: new HarmonyMethod(postfix));
                        Logger.LogInfo("Successfully patched RPCA_Die method for death detection");
                    }
                    else
                    {
                        Logger.LogWarning("Could not find RPCA_Die method");
                    }
                    
                    // Also try to patch Die method
                    var die_method = AccessTools.Method(characterType, "Die");
                    if (die_method != null)
                    {
                        var postfix = AccessTools.Method(typeof(PlayerDeathPatch), nameof(OnPlayerDied_Postfix));
                        harmony.Patch(die_method, postfix: new HarmonyMethod(postfix));
                        Logger.LogInfo("Successfully patched Die method for death detection");
                    }
                }
                else
                {
                    Logger.LogInfo("Could not find Character type for patching - using direct detection instead");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply death detection patches: {ex.Message}");
            }
        }
        
        // This will be called after player dies
        private static void OnPlayerDied_Postfix(object __instance)
        {
            try
            {
                // Check if this is the local player
                // Try to get the photonView from the character instance
                var photonViewProperty = __instance.GetType().GetProperty("photonView") ?? 
                                         __instance.GetType().GetProperty("view");
                
                if (photonViewProperty != null)
                {
                    var photonView = photonViewProperty.GetValue(__instance);
                    if (photonView != null)
                    {
                        // Check IsMine property
                        var isMineProperty = photonView.GetType().GetProperty("IsMine");
                        if (isMineProperty != null)
                        {
                            var isMine = (bool)isMineProperty.GetValue(photonView);
                            if (!isMine)
                            {
                                // This is not the local player, ignore
                                return;
                            }
                        }
                    }
                }
                
                // Alternative check: Compare with localCharacter
                var localCharacterField = __instance.GetType().GetField("localCharacter", 
                    BindingFlags.Public | BindingFlags.Static);
                if (localCharacterField != null)
                {
                    var localCharacter = localCharacterField.GetValue(null);
                    if (localCharacter != null && localCharacter != __instance)
                    {
                        // This is not the local player, ignore
                        return;
                    }
                }
                
                Logger.LogInfo("[Death] Local player death detected via Harmony patch");
                
                // Notify the recording manager only for local player
                if (Plugin.Instance != null)
                {
                    var recordingManager = Plugin.Instance.GetRecordingManager();
                    recordingManager?.OnPlayerDeath();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in death postfix: {ex.Message}");
            }
        }
    }
}