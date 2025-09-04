# Changelog

## [1.0.3] - 2025-09-04

### New Features
- **Complete climb tracking from kiln to peak** - Track your entire journey from the starting kiln all the way to the peak, fulfilling the dream of peaking!
- **Improved climb timing** - Climbs now start precisely with the "RUN STARTED" sequence for better accuracy
- **Death tracking** - Climbs with deaths can now be saved (configurable in settings)
  - Death climbs are marked with a small death icon
  - Death climbs have no share code and won't be uploaded to the cloud
- **Global mod logger** - Implemented centralized logging system with configurable log levels in BepInEx config

### Improvements
- Climb tracking automatically stops on death or return to home
- Better climb start/stop detection for more accurate path recording
- Enhanced debugging capabilities through the new logging system

## [1.0.2] - 2025-09-02

### New Features
- **Improved flymod detection** - Now only displays valid climbs without false positives
- **Update notification system** - Displays in-game notifications when a new mod version is available
- **New settings menu** - Customizable hotkey configuration for toggling the menu

### Improvements
- Better climb validation to ensure only legitimate climbs are shown
- Push notification capability for mod updates

## [1.0.1] - 2025-09-01

### Fix
- Fixed a rendering issue that caused the User Interface (UI) to display pink boxes instead of text when using Vulkan (e.g., on Linux/Steam Deck). All text is now rendering correctly.

## [1.0.0] - 2025-08-31

### New Features
- Completely redesigned UI that matches Peak's visual style
- Difficulty tracking implemented - Ascent level is now saved and can be used as a filter to display climbs
- Advanced search and filtering functionality for climbs
- Optimized climb storage with reduced memory usage locally and on the server
- Non-blocking route display that doesn't obstruct the game view

### Fixes & Optimizations
- Fixed lag spikes when activating campfires
- Limited to 25 climbs per filter for better performance (previously loaded all climbs)
- Automatic list updates with visible climbs always appearing at the top
- Improved sorting system for cloud and local climbs by duration

### Technical Improvements
- Memory optimization for climb data storage
- Improved rendering performance for path visualization
- Better UI responsiveness and user interaction

## [0.1.0] - 2025-01-20

### Added
- Initiale Veröffentlichung des FollowMe-Peak Plugins
- Automatische Pfadaufzeichnung in Content Warning Levels
- Visuelle Pfadanzeige mit konfigurierbarer Sichtbarkeit
- Biom-spezifische Pfadspeicherung
- Cloud-Synchronisation für das Teilen von Pfaden
- F1-Benutzeroberfläche für Einstellungen und Pfadverwaltung
- Automatische Pfadspeicherung beim Anzünden von Lagerfeuern
- Persistente lokale Speicherung von Pfaden
- Server-Integration für Upload/Download von Community-Pfaden

### Features
- Pfadaufzeichnung startet automatisch beim Level-Load
- Pfade werden nach Biom/Level-Bereich getrennt gespeichert
- Optionale Cloud-Synchronisation (funktioniert auch komplett offline)
- Intuitive Benutzeroberfläche zugänglich über F1
- Kompatibel mit BepInEx 5.4.21+

### Technical
- .NET Standard 2.1 Framework
- Integration über BepInEx Plugin-System
- Harmony-basierte Code-Patches
- Unity Engine Integration für 3D-Visualisierung
- JSON-basierte Datenspeicherung