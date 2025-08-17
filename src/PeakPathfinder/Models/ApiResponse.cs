using System;
using System.Collections.Generic;
using System.Linq;

namespace PeakPathfinder.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }

    public class PathUploadResponse
    {
        public string PathId { get; set; }
        public string LevelId { get; set; }
        public long UploadTime { get; set; }
    }

    public class PathListResponse
    {
        public List<ServerPathData> Data { get; set; }
        public PathListMeta Meta { get; set; }
    }

    public class PathSearchResponse
    {
        public bool Success { get; set; }
        public ServerPathData Data { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }

    public class PathListMeta
    {
        public string Level_Id { get; set; }
        public int Count { get; set; }
        public int Limit { get; set; }
        public int Offset { get; set; }
    }

    public class ServerPathData
    {
        public string Id { get; set; }
        public string Level_Id { get; set; }
        public string Player_Name { get; set; }
        public string Biome_Name { get; set; }
        public float Duration { get; set; }
        public List<ApiVector3> Points { get; set; }
        public bool Is_Successful { get; set; }
        public List<string> Tags { get; set; }
        public string Share_Code { get; set; }
        public long Upload_Time { get; set; }
        public string Created_At { get; set; }

        // Convert to local PathData format
        public PathData ToPathData()
        {
            var localPoints = Points?.Select(p => p.ToSerializableVector3()).ToList() ?? new List<SerializableVector3>();
            
            var pathData = new PathData
            {
                Id = Guid.Parse(Id),
                CreationTime = DateTime.Parse(Created_At),
                BiomeName = Biome_Name ?? "Unknown",
                DurationInSeconds = Duration,
                Points = localPoints,
                PlayerName = Player_Name ?? "Unknown",
                IsFromCloud = true // Mark as downloaded from cloud
            };
            
            // Use share code from server or generate if not available
            if (!string.IsNullOrEmpty(Share_Code))
            {
                pathData.ShareCode = Share_Code;
            }
            else
            {
                pathData.GenerateShareCode();
            }
            
            return pathData;
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
        public int TotalPaths { get; set; }
        public int TotalLevels { get; set; }
    }
}