using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FollowMePeak.Models
{
    public class ClimbData
    {
        public Guid Id { get; set; }
        public DateTime CreationTime { get; set; }
        public string BiomeName { get; set; }
        public float DurationInSeconds { get; set; }
        public List<Vector3> Points { get; set; }
        
        // Extended properties for sharing and identification
        public string SaveName { get; set; } = "";
        public string PlayerName { get; set; } = "Local";
        public bool IsFromCloud { get; set; } = false;
        public string ShareCode { get; set; } = "";
        // public List<string> Tags { get; set; } = new List<string>();
        
        // Generate user-friendly save name if empty
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(SaveName))
                return SaveName;
                
            // Auto-generate based on biome and time
            TimeSpan time = TimeSpan.FromSeconds(DurationInSeconds);
            return $"{BiomeName} - {time.Minutes:D2}m{time.Seconds:D2}s";
        }
        
        // Generate short Gipfelcode for easy sharing
        public void GenerateShareCode()
        {
            if (string.IsNullOrEmpty(ShareCode))
            {
                // Use first 8 characters of GUID for user-friendly sharing
                ShareCode = Id.ToString("N").Substring(0, 8).ToUpper();
            }
        }
        
        // Get tags as display string
        /* DISABLED FOR NOW - Will be enabled in future release
        public string GetTagsDisplay()
        {
            if (Tags == null || Tags.Count == 0)
                return "";
            return string.Join(", ", Tags);
        }
        
        // Add tag if not already present
        public void AddTag(string tag)
        {
            if (Tags == null) Tags = new List<string>();
            if (!string.IsNullOrEmpty(tag) && !Tags.Contains(tag))
                Tags.Add(tag);
        }
        
        // Remove tag
        public void RemoveTag(string tag)
        {
            if (Tags != null && Tags.Contains(tag))
                Tags.Remove(tag);
        }
        
        // Check if climb has specific tag
        public bool HasTag(string tag)
        {
            return Tags != null && Tags.Contains(tag);
        }
        */
    }
    
    // Available tags for climbs
    /* DISABLED FOR NOW - Will be enabled in future release
    public static class ClimbTags
    {
        public static readonly string[] AvailableTags = {
            "Schnell",      // Fast route
            "Sicher",       // Safe route  
            "Versteckt",    // Hidden route
            "Schwer",       // Difficult route
            "Einfach",      // Easy route
            "Shortcut",     // Shortcut route
            "Umweg",        // Detour route
            "Scenic",       // Scenic route
            "Geheim",       // Secret route
            "Optimal"       // Optimal route
        };
        
        public static string[] GetTagOptions()
        {
            return AvailableTags;
        }
    }
    */
}