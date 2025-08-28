using System;
using System.Collections.Generic;
using UnityEngine;

namespace FollowMePeak.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }

    public class ClimbUploadResponse
    {
        public string ClimbId { get; set; }
        public string LevelId { get; set; }
        public long UploadTime { get; set; }
    }

    public class ClimbListResponse
    {
        public List<ServerClimbData> Data { get; set; }
        public ClimbListMeta Meta { get; set; }
    }

    public class ClimbSearchResponse
    {
        public bool Success { get; set; }
        public ServerClimbData Data { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }

    public class ClimbListMeta
    {
        public string Level_Id { get; set; }
        public int Count { get; set; }
        public int Total { get; set; }
        public int Limit { get; set; }
        public int Offset { get; set; }
        public bool Has_More { get; set; }
    }

    public class ServerClimbData
    {
        public string Id { get; set; }
        public string Level_Id { get; set; }
        public string LevelId { get; set; } // New camelCase format
        public string Player_Name { get; set; }
        public string PlayerName { get; set; } // New camelCase format
        public string Biome_Name { get; set; }
        public string BiomeName { get; set; } // New camelCase format
        public float Duration { get; set; }
        public List<Vector3> Points { get; set; } // Legacy format
        public string PointData { get; set; } // New compressed format
        public int CompressionVersion { get; set; } = 0;
        public int PointCount { get; set; } = 0;
        public bool Is_Successful { get; set; }
        public bool IsSuccessful { get; set; } // New camelCase format
        // public List<string> Tags { get; set; }
        public string Share_Code { get; set; }
        public string ShareCode { get; set; } // New camelCase format
        public int Ascent_Level { get; set; } = 0; // Ascent level from server
        public int AscentLevel { get; set; } = 0; // New camelCase format
        public long Upload_Time { get; set; }
        public long UploadTime { get; set; } // New camelCase format
        public string Created_At { get; set; }
        public string CreatedAt { get; set; } // New camelCase format

        // Convert to local ClimbData format
        public ClimbData ToClimbData()
        {
            var climbData = new ClimbData
            {
                Id = Guid.Parse(Id),
                IsFromCloud = true // Mark as downloaded from cloud
            };
            
            // Handle both old snake_case and new camelCase formats
            climbData.CreationTime = DateTime.Parse(Created_At ?? CreatedAt ?? DateTime.UtcNow.ToString());
            climbData.BiomeName = Biome_Name ?? BiomeName ?? "Unknown";
            climbData.PlayerName = Player_Name ?? PlayerName ?? "Unknown";
            climbData.DurationInSeconds = Duration;
            climbData.AscentLevel = Ascent_Level != 0 ? Ascent_Level : AscentLevel;
            
            // Handle points - decompress if compressed format
            if (!string.IsNullOrEmpty(PointData))
            {
                // New compressed format
                try
                {
                    var compressedData = Convert.FromBase64String(PointData);
                    // ReadClimbData will create and populate the Points list
                    Utils.ClimbDataCrusher.ReadClimbData(compressedData, climbData);
                    
                    // Debug output
                    System.Diagnostics.Debug.WriteLine($"Decompressed climb: {climbData.Points?.Count ?? 0} points from {compressedData.Length} bytes");
                    
                    // Verify we got points
                    if (climbData.Points == null || climbData.Points.Count == 0)
                    {
                        // Fall back to legacy format if available
                        if (Points != null && Points.Count > 0)
                        {
                            climbData.Points = Points;
                            System.Diagnostics.Debug.WriteLine($"Fell back to legacy format: {Points.Count} points");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Fall back to legacy format if decompression fails
                    System.Diagnostics.Debug.WriteLine($"Decompression failed for climb {Id}: {ex.Message}");
                    // Try to use legacy points if available
                    if (Points != null && Points.Count > 0)
                    {
                        climbData.Points = Points;
                        System.Diagnostics.Debug.WriteLine($"Using legacy fallback: {Points.Count} points");
                    }
                    else
                    {
                        climbData.Points = new List<Vector3>();
                        System.Diagnostics.Debug.WriteLine($"No legacy points available, using empty list");
                    }
                }
            }
            else if (Points != null)
            {
                // Legacy format
                climbData.Points = Points;
            }
            else
            {
                climbData.Points = new List<Vector3>();
            }
            
            // Use share code from server or generate if not available
            var shareCode = Share_Code ?? ShareCode;
            if (!string.IsNullOrEmpty(shareCode))
            {
                climbData.ShareCode = shareCode;
            }
            else
            {
                climbData.GenerateShareCode();
            }
            
            return climbData;
        }
    }

    public class HealthResponse
    {
        public string Status { get; set; }
        public string Timestamp { get; set; }
        public string Database { get; set; }
        public HealthStats Stats { get; set; }
        public double Uptime { get; set; }
        public string Version { get; set; }
    }

    public class HealthStats
    {
        public int TotalClimbs { get; set; }
        public int TotalLevels { get; set; }
    }
}