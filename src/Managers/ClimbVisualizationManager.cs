using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FollowMePeak.Models;
using FollowMePeak.Services;

namespace FollowMePeak.Managers
{
    public class ClimbVisualizationManager
    {
        private readonly ClimbDataService _climbDataService;
        private Dictionary<Guid, bool> _climbVisibility = new Dictionary<Guid, bool>();
        private Dictionary<Guid, GameObject> _climbVisualizerObjects = new Dictionary<Guid, GameObject>();

        public ClimbVisualizationManager(ClimbDataService climbDataService)
        {
            _climbDataService = climbDataService;
        }

        public bool IsClimbVisible(Guid climbId)
        {
            return _climbVisibility.ContainsKey(climbId) && _climbVisibility[climbId];
        }

        public void SetClimbVisibility(Guid climbId, bool visible)
        {
            _climbVisibility[climbId] = visible;
            UpdateVisuals(); // Use existing UpdateVisuals method
        }

        public void ToggleClimbVisibility(Guid climbId)
        {
            if (_climbVisibility.ContainsKey(climbId))
                _climbVisibility[climbId] = !_climbVisibility[climbId];
            else
                _climbVisibility[climbId] = true;
            
            UpdateVisuals();
        }

        public void ShowAllClimbs()
        {
            foreach (var climb in _climbDataService.GetAllClimbs())
                _climbVisibility[climb.Id] = true;
            UpdateVisuals();
        }

        public void HideAllClimbs()
        {
            foreach (var climb in _climbDataService.GetAllClimbs())
                _climbVisibility[climb.Id] = false;
            UpdateVisuals();
        }

        public void InitializeClimbVisibility()
        {
            _climbVisibility.Clear();
            foreach (var climbData in _climbDataService.GetAllClimbs())
            {
                _climbVisibility[climbData.Id] = false;
            }
        }

        public void UpdateVisuals()
        {
            foreach (var visualizer in _climbVisualizerObjects.Values)
            {
                if(visualizer != null) UnityEngine.Object.Destroy(visualizer);
            }
            _climbVisualizerObjects.Clear();

            foreach (var climbData in _climbDataService.GetAllClimbs())
            {
                if (_climbVisibility.ContainsKey(climbData.Id) && _climbVisibility[climbData.Id])
                {
                    CreateClimbVisualizer(climbData);
                }
            }
        }

        private void CreateClimbVisualizer(ClimbData climbData)
        {
            List<Vector3> points = climbData.Points.Select(p => p.ToVector3()).ToList();
            if (points.Count < 2) return;

            var climbObject = new GameObject($"ClimbVisualizer_{climbData.Id}");
            var lineRenderer = climbObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = Color.red;
            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;
            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
            
            _climbVisualizerObjects[climbData.Id] = climbObject;
        }

        public void ClearVisuals()
        {
            foreach (var visualizer in _climbVisualizerObjects.Values)
            {
                if(visualizer != null) UnityEngine.Object.Destroy(visualizer);
            }
            _climbVisualizerObjects.Clear();
            _climbVisibility.Clear();
        }
    }
}