using System;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace FollowMePeak.Patches
{
    public class PluginPatches
    {
        public static string BiomeNameOfCompletedSegment { get; set; }

        [HarmonyPatch(typeof(Campfire), "Light_Rpc")]
        [HarmonyPrefix]
        public static void CaptureBiomeNameBeforeCompletion()
        {
            if (Singleton<MapHandler>.Instance != null)
            {
                Segment currentSegmentEnum = Singleton<MapHandler>.Instance.GetCurrentSegment();
                BiomeNameOfCompletedSegment = Enum.GetName(typeof(Segment), currentSegmentEnum);
                Debug.Log($"[FollowMe-Peak] Biome captured: {BiomeNameOfCompletedSegment}");
            }
        }

        [HarmonyPatch(typeof(Campfire), "Light_Rpc")]
        [HarmonyPostfix]
        public static void SavePathAfterCampfireLit()
        {
            if (Plugin.Instance != null)
            {
                Debug.Log("[FollowMe-Peak] Campfire.Light_Rpc() completed. Saving triggered.");
                Plugin.Instance.OnCampfireLit(BiomeNameOfCompletedSegment);
            }
        }
    }
    
}