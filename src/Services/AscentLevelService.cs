using System;
using BepInEx.Logging;

namespace FollowMePeak.Services
{
    public class AscentLevelService
    {
        private readonly ManualLogSource _logger;
        private int _currentAscentLevel = 0;
        
        public AscentLevelService(ManualLogSource logger)
        {
            _logger = logger;
        }
        
        public int CurrentAscentLevel
        {
            get => _currentAscentLevel;
            private set
            {
                if (_currentAscentLevel != value)
                {
                    _currentAscentLevel = value;
                    _logger.LogInfo($"Ascent level updated to: {_currentAscentLevel}");
                }
            }
        }
        
        public void UpdateAscentLevel(int ascentLevel)
        {
            CurrentAscentLevel = ascentLevel;
        }
        
        public void Reset()
        {
            CurrentAscentLevel = 0;
        }
        
        // Try to determine ascent level from current game state
        public void DetectCurrentAscentLevel()
        {
            try
            {
                _logger.LogInfo("Attempting to detect current ascent level from game state");
                
                // Method 1: Try to find Ascents class (primary method)
                if (TryGetAscentFromAscentData())
                    return;
                    
                // Method 2: Try to find AscentUI class (fallback)
                if (TryGetAscentFromAscentUI())
                    return;
                    
                // Method 3: Try to find AscentInstanceData (fallback)
                if (TryGetAscentFromInstanceData())
                    return;
                    
                // Fallback: Default to 0
                _logger.LogWarning("Could not detect ascent level from game state, defaulting to 0");
                CurrentAscentLevel = 0;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error detecting ascent level: {e.Message}");
                CurrentAscentLevel = 0;
            }
        }
        
        private bool TryGetAscentFromAscentData()
        {
            try
            {
                // First try the exact Ascents class we found in dnSpy
                var ascentsType = FindTypeByName("Ascents");
                if (ascentsType != null && TryGetAscentFromAscentsClass(ascentsType))
                    return true;
                
                // Look for AscentData type in all loaded assemblies (fallback)
                var ascentDataType = FindTypeByName("AscentData");
                if (ascentDataType == null) return false;
                
                // Try to find an instance of AscentData
                var ascentDataComponent = UnityEngine.Object.FindFirstObjectByType(ascentDataType);
                if (ascentDataComponent == null) 
                {
                    _logger.LogInfo("No AscentData component found in scene, trying static access");
                    return TryGetAscentFromStaticMembers(ascentDataType, "AscentData");
                }
                
                // Try common property names for ascent level
                string[] propertyNames = { "CurrentLevel", "CurrentAscent", "Level", "Ascent", "AscentLevel", "currentLevel", "ascent" };
                
                foreach (string propertyName in propertyNames)
                {
                    var property = ascentDataType.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (property != null && (property.PropertyType == typeof(int) || property.PropertyType == typeof(float)))
                    {
                        object value = property.GetValue(ascentDataComponent);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via AscentData.{propertyName}: {ascentLevel}");
                        CurrentAscentLevel = ascentLevel;
                        return true;
                    }
                }
                
                // Try common field names if properties don't work
                string[] fieldNames = { "currentLevel", "currentAscent", "level", "ascent", "ascentLevel", "m_currentLevel", "_ascentLevel" };
                
                foreach (string fieldName in fieldNames)
                {
                    var field = ascentDataType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null && (field.FieldType == typeof(int) || field.FieldType == typeof(float)))
                    {
                        object value = field.GetValue(ascentDataComponent);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via AscentData.{fieldName}: {ascentLevel}");
                        CurrentAscentLevel = ascentLevel;
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Failed to get ascent from AscentData: {e.Message}");
                return false;
            }
        }
        
        private bool TryGetAscentFromAscentsClass(Type ascentsType)
        {
            try
            {
                _logger.LogInfo("Found Ascents class, trying to access currentAscent property");
                
                // Try to get the currentAscent property (static)
                var currentAscentProperty = ascentsType.GetProperty("currentAscent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (currentAscentProperty != null && (currentAscentProperty.PropertyType == typeof(int)))
                {
                    object value = currentAscentProperty.GetValue(null);
                    int ascentLevel = Convert.ToInt32(value);
                    _logger.LogInfo($"Found ascent level via Ascents.currentAscent: {ascentLevel}");
                    CurrentAscentLevel = ascentLevel;
                    return true;
                }
                
                // Try alternative property names in case of obfuscation
                string[] propertyNames = { "currentAscent", "currescent", "_currentAscent", "CurrentAscent" };
                
                foreach (string propertyName in propertyNames)
                {
                    var property = ascentsType.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (property != null && (property.PropertyType == typeof(int)))
                    {
                        object value = property.GetValue(null);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via Ascents.{propertyName}: {ascentLevel}");
                        CurrentAscentLevel = ascentLevel;
                        return true;
                    }
                }
                
                // Try fields as well
                string[] fieldNames = { "_currentAscent", "currentAscent", "currescent", "CurrentAscent" };
                
                foreach (string fieldName in fieldNames)
                {
                    var field = ascentsType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (field != null && (field.FieldType == typeof(int)))
                    {
                        object value = field.GetValue(null);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via Ascents.{fieldName}: {ascentLevel}");
                        CurrentAscentLevel = ascentLevel;
                        return true;
                    }
                }
                
                _logger.LogWarning("Found Ascents class but could not access currentAscent member");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Failed to get ascent from Ascents class: {e.Message}");
                return false;
            }
        }
        
        private bool TryGetAscentFromAscentUI()
        {
            try
            {
                var ascentUIType = FindTypeByName("AscentUI");
                if (ascentUIType == null) return false;
                
                var ascentUIComponent = UnityEngine.Object.FindFirstObjectByType(ascentUIType);
                if (ascentUIComponent == null) 
                {
                    _logger.LogInfo("No AscentUI component found in scene, trying static access");
                    return TryGetAscentFromStaticMembers(ascentUIType, "AscentUI");
                }
                
                // Common property/field names for UI components
                string[] memberNames = { "currentAscent", "ascentLevel", "level", "currentLevel", "displayedLevel" };
                
                foreach (string memberName in memberNames)
                {
                    // Try property first
                    var property = ascentUIType.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (property != null && (property.PropertyType == typeof(int) || property.PropertyType == typeof(float)))
                    {
                        object value = property.GetValue(ascentUIComponent);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via AscentUI.{memberName}: {ascentLevel}");
                        CurrentAscentLevel = ascentLevel;
                        return true;
                    }
                    
                    // Try field
                    var field = ascentUIType.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null && (field.FieldType == typeof(int) || field.FieldType == typeof(float)))
                    {
                        object value = field.GetValue(ascentUIComponent);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via AscentUI.{memberName}: {ascentLevel}");
                        CurrentAscentLevel = ascentLevel;
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Failed to get ascent from AscentUI: {e.Message}");
                return false;
            }
        }
        
        private bool TryGetAscentFromInstanceData()
        {
            try
            {
                var instanceDataType = FindTypeByName("AscentInstanceData");
                if (instanceDataType == null) return false;
                
                // AscentInstanceData is likely a data class, not a MonoBehaviour
                // Try to find static instances or properties
                
                // Method 1: Look for static properties/fields
                string[] memberNames = { "Current", "Instance", "CurrentAscent", "AscentLevel" };
                
                foreach (string memberName in memberNames)
                {
                    var property = instanceDataType.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (property != null)
                    {
                        object instance = property.GetValue(null);
                        if (instance != null)
                        {
                            int? ascentLevel = TryGetAscentFromInstance(instance, instanceDataType, memberName);
                            if (ascentLevel.HasValue)
                            {
                                CurrentAscentLevel = ascentLevel.Value;
                                return true;
                            }
                        }
                    }
                    
                    var field = instanceDataType.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (field != null)
                    {
                        object instance = field.GetValue(null);
                        if (instance != null)
                        {
                            int? ascentLevel = TryGetAscentFromInstance(instance, instanceDataType, memberName);
                            if (ascentLevel.HasValue)
                            {
                                CurrentAscentLevel = ascentLevel.Value;
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Failed to get ascent from AscentInstanceData: {e.Message}");
                return false;
            }
        }
        
        private int? TryGetAscentFromInstance(object instance, Type instanceType, string sourceMember)
        {
            try
            {
                string[] ascentMemberNames = { "ascentLevel", "level", "currentLevel", "ascent", "AscentLevel", "CurrentLevel", "Ascent" };
                
                foreach (string memberName in ascentMemberNames)
                {
                    var property = instanceType.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (property != null && (property.PropertyType == typeof(int) || property.PropertyType == typeof(float)))
                    {
                        object value = property.GetValue(instance);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via {instanceType.Name}.{sourceMember}.{memberName}: {ascentLevel}");
                        return ascentLevel;
                    }
                    
                    var field = instanceType.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null && (field.FieldType == typeof(int) || field.FieldType == typeof(float)))
                    {
                        object value = field.GetValue(instance);
                        int ascentLevel = Convert.ToInt32(value);
                        _logger.LogInfo($"Found ascent level via {instanceType.Name}.{sourceMember}.{memberName}: {ascentLevel}");
                        return ascentLevel;
                    }
                }
                
                return null;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Failed to extract ascent from instance: {e.Message}");
                return null;
            }
        }
        
        private bool TryGetAscentFromStaticMembers(Type type, string typeName)
        {
            try
            {
                _logger.LogInfo($"Searching for static members in {typeName}");
                
                // Try static properties and fields for ascent level
                string[] staticMemberNames = { "CurrentLevel", "CurrentAscent", "Level", "Ascent", "AscentLevel", "currentLevel", "ascent", "Instance", "Current" };
                
                foreach (string memberName in staticMemberNames)
                {
                    // Try static property
                    var property = type.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (property != null)
                    {
                        if (property.PropertyType == typeof(int) || property.PropertyType == typeof(float))
                        {
                            object value = property.GetValue(null);
                            int ascentLevel = Convert.ToInt32(value);
                            _logger.LogInfo($"Found ascent level via static {typeName}.{memberName}: {ascentLevel}");
                            CurrentAscentLevel = ascentLevel;
                            return true;
                        }
                        else
                        {
                            // This might be an instance property, try to get the instance
                            object instance = property.GetValue(null);
                            if (instance != null)
                            {
                                int? ascentLevel = TryGetAscentFromInstance(instance, instance.GetType(), $"static {memberName}");
                                if (ascentLevel.HasValue)
                                {
                                    CurrentAscentLevel = ascentLevel.Value;
                                    return true;
                                }
                            }
                        }
                    }
                    
                    // Try static field
                    var field = type.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        if (field.FieldType == typeof(int) || field.FieldType == typeof(float))
                        {
                            object value = field.GetValue(null);
                            int ascentLevel = Convert.ToInt32(value);
                            _logger.LogInfo($"Found ascent level via static {typeName}.{memberName}: {ascentLevel}");
                            CurrentAscentLevel = ascentLevel;
                            return true;
                        }
                        else
                        {
                            // This might be an instance field, try to get the instance
                            object instance = field.GetValue(null);
                            if (instance != null)
                            {
                                int? ascentLevel = TryGetAscentFromInstance(instance, instance.GetType(), $"static {memberName}");
                                if (ascentLevel.HasValue)
                                {
                                    CurrentAscentLevel = ascentLevel.Value;
                                    return true;
                                }
                            }
                        }
                    }
                }
                
                _logger.LogInfo($"No suitable static members found in {typeName}");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Failed to get ascent from static members of {typeName}: {e.Message}");
                return false;
            }
        }
        
        private Type FindTypeByName(string typeName)
        {
            try
            {
                // Search in all loaded assemblies
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == typeName)
                            {
                                _logger.LogInfo($"Found type {typeName} in assembly {assembly.GetName().Name}");
                                return type;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Skip assemblies that can't be reflected
                        continue;
                    }
                }
                
                _logger.LogWarning($"Type {typeName} not found in any loaded assembly");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Error searching for type {typeName}: {e.Message}");
                return null;
            }
        }
    }
}