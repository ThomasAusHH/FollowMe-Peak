# Changelog

## [1.0.9] - 2026-08-14

### New Features
- **Community rating system** - Rate cloud climbs with 1-5 stars directly in the mod menu. The average rating and vote count are shown on every climb, and your own vote can be changed at any time.
- **Community cheat reporting** - Report suspected cheated climbs via the report button (with confirmation dialog). Climbs with 3 or more community reports get a warning badge for everyone.
- **Best climbs first** - The climb list is now sorted by community rating by default, so the best routes appear at the top. Sorting by duration via the menu still works as before.

### Fixes
- **Fixed uploads failing permanently after rate limiting (HTTP 429)** - Uploads that hit the server rate limit are now retried automatically with an increasing delay instead of being dropped.

## [1.0.8] - 2026-08-12

### Fixes
- **Fixed micro-stuttering during gameplay** - The fly-mod detection scanned every object in the scene twice per second. It now only checks the local player using cached data, eliminating the recurring performance spikes.
- Fixed false-positive fly detections caused by remote players in multiplayer.

## [1.0.7] - 2026-08-12

### New Features
- **New level preview image** - Added the image for the new level to the mod menu (updated modui AssetBundle).

## [1.0.6] - 2026-06-27

### New Features
- **UI hotkey mapping** - Added UI elements to map the shortcut key for toggling visibility of the last selected climbs (Fixes #27).

### Fixes
- Synchronized version numbers across all manifest files, assemblies and code (Fixes #32).

## [1.0.5] - 2025-11-09

### Fixes
- Fixed version number.

## [1.0.4] - 2025-11-09

### New Features
- **Added support for the new 'Roots' biome** - Full integration of the new 'Roots' biome, which alternates with 'Tropics'.

### Fixes
- Fixed a critical error (`MissingFieldException`) caused by a recent game update, which led to crashes upon player death or at the end of a run.
- Resolved a file access conflict that occurred when saving the upload queue, preventing climbs from being saved correctly after a successful upload.

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