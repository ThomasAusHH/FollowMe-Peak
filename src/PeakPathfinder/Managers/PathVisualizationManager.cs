using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PeakPathfinder.Models;
using PeakPathfinder.Services;

namespace PeakPathfinder.Managers
{
    public class PathVisualizationManager
    {
        private readonly PathDataService _pathDataService;
        private Dictionary<Guid, bool> _pathVisibility = new Dictionary<Guid, bool>();
        private Dictionary<Guid, GameObject> _pathVisualizerObjects = new Dictionary<Guid, GameObject>();

        public PathVisualizationManager(PathDataService pathDataService)
        {
            _pathDataService = pathDataService;
        }

        public bool IsPathVisible(Guid pathId)
        {
            return _pathVisibility.ContainsKey(pathId) && _pathVisibility[pathId];
        }

        public void TogglePathVisibility(Guid pathId)
        {
            if (_pathVisibility.ContainsKey(pathId))
                _pathVisibility[pathId] = !_pathVisibility[pathId];
            else
                _pathVisibility[pathId] = true;
            
            UpdateVisuals();
        }

        public void ShowAllPaths()
        {
            foreach (var path in _pathDataService.GetAllPaths())
                _pathVisibility[path.Id] = true;
            UpdateVisuals();
        }

        public void HideAllPaths()
        {
            foreach (var path in _pathDataService.GetAllPaths())
                _pathVisibility[path.Id] = false;
            UpdateVisuals();
        }

        public void InitializePathVisibility()
        {
            _pathVisibility.Clear();
            foreach (var pathData in _pathDataService.GetAllPaths())
            {
                _pathVisibility[pathData.Id] = false;
            }
        }

        public void UpdateVisuals()
        {
            foreach (var visualizer in _pathVisualizerObjects.Values)
            {
                if(visualizer != null) UnityEngine.Object.Destroy(visualizer);
            }
            _pathVisualizerObjects.Clear();

            foreach (var pathData in _pathDataService.GetAllPaths())
            {
                if (_pathVisibility.ContainsKey(pathData.Id) && _pathVisibility[pathData.Id])
                {
                    CreatePathVisualizer(pathData);
                }
            }
        }

        private void CreatePathVisualizer(PathData pathData)
        {
            List<Vector3> points = pathData.Points.Select(p => p.ToVector3()).ToList();
            if (points.Count < 2) return;

            var pathObject = new GameObject($"PathVisualizer_{pathData.Id}");
            var lineRenderer = pathObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = Color.red;
            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;
            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
            
            _pathVisualizerObjects[pathData.Id] = pathObject;
        }

        public void ClearVisuals()
        {
            foreach (var visualizer in _pathVisualizerObjects.Values)
            {
                if(visualizer != null) UnityEngine.Object.Destroy(visualizer);
            }
            _pathVisualizerObjects.Clear();
            _pathVisibility.Clear();
        }
    }
}