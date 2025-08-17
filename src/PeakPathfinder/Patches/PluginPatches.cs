using System;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace PeakPathfinder.Patches
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
                Debug.Log($"[PeakPathfinder] Biome erfasst: {BiomeNameOfCompletedSegment}");
            }
        }

        [HarmonyPatch(typeof(Campfire), "Light_Rpc")]
        [HarmonyPostfix]
        public static void SavePathAfterCampfireLit()
        {
            if(Plugin.Instance != null)
            {
                Debug.Log("[PeakPathfinder] Campfire.Light_Rpc() beendet. Speichern wird ausgelöst.");
                Plugin.Instance.OnCampfireLit(BiomeNameOfCompletedSegment);
            }
        }
    }
}