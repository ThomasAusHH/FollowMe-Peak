using HarmonyLib;

namespace FollowMePeak.Patches
{
    public class PluginPatches
    {
        [HarmonyPatch(typeof(MountainProgressHandler), nameof(MountainProgressHandler.TriggerReached))]
        [HarmonyPostfix]
        public static void StartNewSegment(MountainProgressHandler.ProgressPoint __0)
        {
            if(Plugin.Instance != null)
            {
                Plugin.Instance.OnNextClimbSegment(__0.title);
            }
        }

        [HarmonyPatch(typeof(PeakHandler), nameof(PeakHandler.SummonHelicopter))]
        [HarmonyPostfix]
        public static void SavePathAfterHelicopterSummoned()
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.OnClimbSegmentComplete();
            }
        }
    }
}