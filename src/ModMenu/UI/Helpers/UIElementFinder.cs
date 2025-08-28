using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FollowMePeak.ModMenu.UI.Helpers
{
    /// <summary>
    /// Helper class to find and cache UI elements
    /// </summary>
    public static class UIElementFinder
    {
        public static T FindComponent<T>(Transform root, string path) where T : Component
        {
            if (root == null) return null;
            
            Transform element = root.Find(path);
            if (element != null)
            {
                return element.GetComponent<T>();
            }
            
            return null;
        }
        
        public static GameObject FindGameObject(Transform root, string path)
        {
            if (root == null) return null;
            
            Transform element = root.Find(path);
            return element?.gameObject;
        }
        
        public static Transform FindTransform(Transform root, string path)
        {
            if (root == null) return null;
            return root.Find(path);
        }
        
        /// <summary>
        /// Recursively finds a child transform by name
        /// </summary>
        public static Transform FindChildRecursive(Transform parent, string name)
        {
            // Check direct children first
            Transform result = parent.Find(name);
            if (result != null) return result;
            
            // Then check all descendants
            foreach (Transform child in parent)
            {
                result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// Finds a child that contains the specified string in its name
        /// </summary>
        public static Transform FindChildWithName(Transform parent, string nameContains)
        {
            // Check immediate children first
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.ToLower().Contains(nameContains.ToLower()))
                {
                    return child;
                }
            }
            
            // Then search recursively
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildWithName(parent.GetChild(i), nameContains);
                if (result != null) return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the full transform path from root
        /// </summary>
        public static string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }
    }
}