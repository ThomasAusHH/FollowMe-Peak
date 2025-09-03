using HarmonyLib;
using BepInEx.Logging;
using System.Reflection;

namespace FollowMePeak.Patches
{
    /// <summary>
    /// Patch for RunManager to detect when a run starts
    /// </summary>
    public static class RunManagerPatch
    {
        private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("RunManagerPatch");
        
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                // Find RunManager type
                var runManagerType = System.Type.GetType("RunManager, Assembly-CSharp");
                if (runManagerType == null)
                {
                    Logger.LogError("Could not find RunManager type");
                    return;
                }
                
                // Find StartRun method
                var startRunMethod = runManagerType.GetMethod("StartRun", BindingFlags.Public | BindingFlags.Instance);
                if (startRunMethod == null)
                {
                    Logger.LogError("Could not find StartRun method in RunManager");
                    return;
                }
                
                // Apply postfix patch
                var postfixMethod = typeof(RunManagerPatch).GetMethod(nameof(StartRunPostfix), BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(startRunMethod, postfix: new HarmonyMethod(postfixMethod));
                
                Logger.LogInfo("Successfully patched RunManager.StartRun");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to patch RunManager: {ex}");
            }
        }
        
        public static void StartRunPostfix()
        {
            Logger.LogInfo("[RunManagerPatch] RunManager.StartRun called - RUN STARTED!");
            
            // Notify Plugin
            Plugin.Instance?.OnRunStartedFromPatch();
        }
    }
}