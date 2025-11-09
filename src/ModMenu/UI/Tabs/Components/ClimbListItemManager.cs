using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FollowMePeak.Models;
using FollowMePeak.Utils;

namespace FollowMePeak.ModMenu.UI.Tabs.Components
{
    public class ClimbListItemManager
    {
        private GameObject _itemTemplate;
        private Transform _contentContainer;
        private List<GameObject> _activeItems = new List<GameObject>();
        
        public ClimbListItemManager(GameObject itemTemplate, Transform contentContainer)
        {
            _itemTemplate = itemTemplate;
            _contentContainer = contentContainer;
        }
        
        public GameObject CreateClimbItem(ClimbData climb, Action<ClimbData, bool> onVisibilityChanged, bool isVisible)
        {
            if (_itemTemplate == null || _contentContainer == null) return null;
            
            GameObject newItem = UnityEngine.Object.Instantiate(_itemTemplate);
            newItem.transform.SetParent(_contentContainer, false);
            newItem.SetActive(true);
            newItem.name = $"Climb_{climb.Id}";
            
            // Preserve template size
            RectTransform rectTransform = newItem.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                RectTransform templateRect = _itemTemplate.GetComponent<RectTransform>();
                if (templateRect != null)
                {
                    rectTransform.sizeDelta = templateRect.sizeDelta;
                }
            }
            
            SetBiomeIcon(newItem, climb.BiomeName);
            SetClimbInfo(newItem, climb);
            SetupVisibilityToggle(newItem, climb, onVisibilityChanged, isVisible);
            SetupCopyButton(newItem, climb);
            
            _activeItems.Add(newItem);
            return newItem;
        }
        
        public void ClearAllItems()
        {
            foreach (var item in _activeItems)
            {
                if (item != null)
                    UnityEngine.Object.Destroy(item);
            }
            _activeItems.Clear();
        }
        
        public int ItemCount => _activeItems.Count;
        
        private void SetBiomeIcon(GameObject item, string biomeName)
        {
            Transform biomeIconArea = item.transform.Find("BiomeIconArea");
            if (biomeIconArea == null) return;
            
            // Hide all icons
            foreach (Transform child in biomeIconArea)
            {
                child.gameObject.SetActive(false);
            }
            
            // Show correct icon
            string normalizedBiome = biomeName?.ToLower() ?? "";
            Transform iconToShow = null;
            
            if (normalizedBiome.Contains("beach"))
                iconToShow = biomeIconArea.Find("BeachIcon");
            else if (normalizedBiome.Contains("tropic") || normalizedBiome.Contains("roots"))
                iconToShow = biomeIconArea.Find("TropicsIcon");
            else if (normalizedBiome.Contains("alpine") || normalizedBiome.Contains("mesa"))
                iconToShow = biomeIconArea.Find("AlpineMesaIcon");
            else if (normalizedBiome.Contains("caldera"))
                iconToShow = biomeIconArea.Find("CalderaIcon");
            else
                iconToShow = biomeIconArea.Find("BeachIcon"); // Default
            
            if (iconToShow != null)
                iconToShow.gameObject.SetActive(true);
        }
        
        private void SetClimbInfo(GameObject item, ClimbData climb)
        {
            // Set date
            var dateText = item.transform.Find("ClimbDate")?.GetComponent<TextMeshProUGUI>();
            if (dateText != null)
                dateText.text = climb.CreationTime.ToString("dd.MM.yyyy HH:mm");
            
            // Set duration
            var durationText = item.transform.Find("ClimbDuration")?.GetComponent<TextMeshProUGUI>();
            if (durationText != null)
            {
                int minutes = Mathf.FloorToInt(climb.DurationInSeconds / 60f);
                int seconds = Mathf.FloorToInt(climb.DurationInSeconds % 60f);
                durationText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // Set ascent level
            var ascentText = item.transform.Find("ClimbAscent")?.GetComponent<TextMeshProUGUI>();
            if (ascentText != null)
                ascentText.text = climb.AscentLevel.ToString();
            
            // Set share code
            var shareCodeText = item.transform.Find("ClimbShareCode")?.GetComponent<TextMeshProUGUI>();
            if (shareCodeText != null)
            {
                // Ensure share code is generated
                if (string.IsNullOrEmpty(climb.ShareCode))
                    climb.GenerateShareCode();
                shareCodeText.text = climb.ShareCode ?? "";
            }
        }
        
        private void SetupVisibilityToggle(GameObject item, ClimbData climb, 
            Action<ClimbData, bool> onVisibilityChanged, bool isVisible)
        {
            Transform visToggle = item.transform.Find("ClimbVisibilityToggle");
            if (visToggle == null) return;
            
            Toggle toggle = visToggle.GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.isOn = isVisible;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((bool value) => {
                    onVisibilityChanged?.Invoke(climb, value);
                });
            }
        }
        
        private void SetupCopyButton(GameObject item, ClimbData climb)
        {
            var copyButton = item.transform.Find("ClimbShareCodeCopyButton")?.GetComponent<Button>();
            if (copyButton != null)
            {
                // Ensure share code is generated
                if (string.IsNullOrEmpty(climb.ShareCode))
                    climb.GenerateShareCode();
                
                if (!string.IsNullOrEmpty(climb.ShareCode))
                {
                    copyButton.onClick.RemoveAllListeners();
                    copyButton.onClick.AddListener(() => {
                        GUIUtility.systemCopyBuffer = climb.ShareCode;
                        ModLogger.Instance?.Info($"[ClimbListItem] Copied share code: {climb.ShareCode}");
                    });
                    copyButton.gameObject.SetActive(true);
                }
                else
                {
                    copyButton.gameObject.SetActive(false);
                }
            }
        }
    }
}
