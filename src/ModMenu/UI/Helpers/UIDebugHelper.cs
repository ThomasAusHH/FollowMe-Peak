using System.Linq;
using UnityEngine;
using FollowMePeak.Utils;

namespace FollowMePeak.ModMenu.UI.Helpers
{
    /// <summary>
    /// Helper class for UI debugging
    /// </summary>
    public static class UIDebugHelper
    {
        /// <summary>
        /// Logs the transform hierarchy for debugging
        /// </summary>
        public static void LogTransformHierarchy(Transform transform, int depth = 0, string prefix = "[ModMenuUI]")
        {
            string indent = new string(' ', depth * 2);
            
            // Log components on this transform
            Component[] components = transform.GetComponents<Component>();
            string componentList = string.Join(", ", components.Select(c => c?.GetType()?.Name ?? "null"));
            
            ModLogger.Instance?.Info($"{prefix} {indent}└─ {transform.name} (Active: {transform.gameObject.activeSelf}) [Components: {componentList}]");
            
            // Recursively log children
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                LogTransformHierarchy(child, depth + 1, prefix);
            }
        }
    }
}
