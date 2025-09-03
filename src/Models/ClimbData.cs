using System;
using System.Collections.Generic;
using System.IO;
using FollowMePeak.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace FollowMePeak.Models
{
    [JsonConverter(typeof(ClimbDataConverter))]
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
        public int AscentLevel { get; set; } = 0; // Ascent level (-1 to 8+, -1 = not started, 0-8+ = progress)
        // public List<string> Tags { get; set; } = new List<string>();
        
        // Fly Detection flags - persisted with climb
        public bool WasFlagged { get; set; } = false;
        public float FlaggedScore { get; set; } = 0f;
        public string FlaggedReason { get; set; } = "";
        
        // Death climb flag - climb where player died  
        public bool WasDeathClimb { get; set; } = false;
        
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

    public class ClimbDataConverter : JsonConverter<ClimbData>
    {
        public override void WriteJson(JsonWriter writer, ClimbData value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("Id");
            serializer.Serialize(writer, value.Id);

            writer.WritePropertyName("CreationTime");
            serializer.Serialize(writer, value.CreationTime);

            writer.WritePropertyName("BiomeName");
            writer.WriteValue(value.BiomeName);

            writer.WritePropertyName("DurationInSeconds");
            writer.WriteValue(value.DurationInSeconds);

            writer.WritePropertyName("PointData");
            using (MemoryStream stream = new())
            {
                ClimbDataCrusher.WriteClimbData(stream, value);
                writer.WriteValue(Convert.ToBase64String(stream.ToArray()));
            }

            writer.WritePropertyName("SaveName");
            writer.WriteValue(value.SaveName);

            writer.WritePropertyName("PlayerName");
            writer.WriteValue(value.PlayerName);

            writer.WritePropertyName("IsFromCloud");
            writer.WriteValue(value.IsFromCloud);

            writer.WritePropertyName("ShareCode");
            writer.WriteValue(value.ShareCode);
            
            writer.WritePropertyName("WasFlagged");
            writer.WriteValue(value.WasFlagged);
            
            writer.WritePropertyName("FlaggedScore");
            writer.WriteValue(value.FlaggedScore);
            
            writer.WritePropertyName("FlaggedReason");
            writer.WriteValue(value.FlaggedReason);
            
            writer.WritePropertyName("WasDeathClimb");
            writer.WriteValue(value.WasDeathClimb);

            /*
            // Tags property (disabled for now)
            writer.WritePropertyName("Tags");
            serializer.Serialize(writer, value.Tags);
            */

            writer.WriteEndObject();
        }

        public override ClimbData ReadJson(JsonReader reader, Type objectType, ClimbData existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var climbData = new ClimbData();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = (string)reader.Value;
                    reader.Read();

                    switch (propertyName)
                    {
                        case "Id":
                            climbData.Id = serializer.Deserialize<Guid>(reader);
                            break;
                        case "CreationTime":
                            climbData.CreationTime = serializer.Deserialize<DateTime>(reader);
                            break;
                        case "BiomeName":
                            climbData.BiomeName = serializer.Deserialize<string>(reader);
                            break;
                        case "DurationInSeconds":
                            climbData.DurationInSeconds = serializer.Deserialize<float>(reader);
                            break;
                        case "Points":
                            climbData.Points = serializer.Deserialize<List<Vector3>>(reader);
                            break;
                        case "PointData":
                        {
                            if (reader.Value is string value)
                                ClimbDataCrusher.ReadClimbData(Convert.FromBase64String(value), climbData);
                            break;
                        }
                        case "SaveName":
                            climbData.SaveName = serializer.Deserialize<string>(reader);
                            break;
                        case "PlayerName":
                            climbData.PlayerName = serializer.Deserialize<string>(reader);
                            break;
                        case "IsFromCloud":
                            climbData.IsFromCloud = serializer.Deserialize<bool>(reader);
                            break;
                        case "ShareCode":
                            climbData.ShareCode = serializer.Deserialize<string>(reader);
                            break;
                        case "WasFlagged":
                            climbData.WasFlagged = serializer.Deserialize<bool>(reader);
                            break;
                        case "FlaggedScore":
                            climbData.FlaggedScore = serializer.Deserialize<float>(reader);
                            break;
                        case "FlaggedReason":
                            climbData.FlaggedReason = serializer.Deserialize<string>(reader);
                            break;
                        case "WasDeathClimb":
                            climbData.WasDeathClimb = serializer.Deserialize<bool>(reader);
                            break;
                        /*
                        // Tags property (disabled for now)
                        case "Tags":
                            climbData.Tags = serializer.Deserialize<List<string>>(reader);
                            break;
                        */
                    }
                }
            }
            return climbData;
        }
    }
}