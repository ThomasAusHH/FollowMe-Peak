using BepInEx.Logging;

namespace FollowMePeak.Utils
{
    public enum LogLevel
    {
        None = 0,     // Keine Logs
        Error = 1,    // Nur kritische Fehler (DEFAULT)
        Warning = 2,  // Fehler + Warnungen  
        Info = 3,     // Standard-Informationen
        Debug = 4,    // Debug-Informationen
        Verbose = 5   // Alles inkl. Performance-Metriken
    }

    public class ModLogger
    {
        private readonly ManualLogSource _logger;
        private static LogLevel _currentLevel = LogLevel.Error;
        
        // Static instance for global access
        public static ModLogger Instance { get; set; }
        
        public static LogLevel CurrentLevel 
        { 
            get => _currentLevel;
            set => _currentLevel = value;
        }
        
        public ModLogger(ManualLogSource logger)
        {
            _logger = logger;
        }
        
        public void Error(string message)
        {
            if (_currentLevel >= LogLevel.Error)
                _logger.LogError(message);
        }
        
        public void Warning(string message)
        {
            if (_currentLevel >= LogLevel.Warning)
                _logger.LogWarning(message);
        }
        
        public void Info(string message)
        {
            if (_currentLevel >= LogLevel.Info)
                _logger.LogInfo(message);
        }
        
        public void Debug(string message)
        {
            if (_currentLevel >= LogLevel.Debug)
                _logger.LogInfo($"[DEBUG] {message}");
        }
        
        public void Verbose(string message)
        {
            if (_currentLevel >= LogLevel.Verbose)
                _logger.LogInfo($"[VERBOSE] {message}");
        }
    }
}