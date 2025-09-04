using HarmonyLib;
using BepInEx.Logging;
using System.Reflection;
using FollowMePeak.Utils;

namespace FollowMePeak.Patches
{
    /// <summary>
    /// Patch for RunManager to detect when a run starts
    /// </summary>
    public static class RunManagerPatch
    {
        // Use ModLogger.Instance instead of own logger
        
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                // Find RunManager type
                var runManagerType = System.Type.GetType("RunManager, Assembly-CSharp");
                if (runManagerType == null)
                {
                    ModLogger.Instance?.Error("Could not find RunManager type");
                    return;
                }
                
                // Find StartRun method
                var startRunMethod = runManagerType.GetMethod("StartRun", BindingFlags.Public | BindingFlags.Instance);
                if (startRunMethod == null)
                {
                    ModLogger.Instance?.Error("Could not find StartRun method in RunManager");
                    return;
                }
                
                // Apply postfix patch
                var postfixMethod = typeof(RunManagerPatch).GetMethod(nameof(StartRunPostfix), BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(startRunMethod, postfix: new HarmonyMethod(postfixMethod));
                
                ModLogger.Instance?.Info("Successfully patched RunManager.StartRun");
            }
            catch (System.Exception ex)
            {
                ModLogger.Instance?.Error($"Failed to patch RunManager: {ex}");
            }
        }
        
        public static void StartRunPostfix()
        {
            ModLogger.Instance?.Info("[RunManagerPatch] RunManager.StartRun called - RUN STARTED!");
            
            // Notify Plugin
            Plugin.Instance?.OnRunStartedFromPatch();
        }
    }
}