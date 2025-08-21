# Changelog

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