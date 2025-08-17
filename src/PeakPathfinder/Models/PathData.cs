using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakPathfinder.Models
{
    public class PathData
    {
        public Guid Id { get; set; }
        public DateTime CreationTime { get; set; }
        public string BiomeName { get; set; }
        public float DurationInSeconds { get; set; }
        public List<SerializableVector3> Points { get; set; }
    }

    public struct SerializableVector3
    {
        public float X, Y, Z;
        public SerializableVector3(Vector3 vec) { X = vec.x; Y = vec.y; Z = vec.z; }
        public Vector3 ToVector3() { return new Vector3(X, Y, Z); }
    }
}