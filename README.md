# FollowMe-Peak

Ein BepInEx-Plugin für Content Warning, das automatisch Pfade in Levels aufzeichnet und als visuelle Hilfe für andere Spieler anzeigt.

## Features

- **Automatische Pfadaufzeichnung**: Zeichnet deine Bewegungen in jedem Level automatisch auf
- **Visuelle Pfadanzeige**: Zeigt aufgezeichnete Pfade als sichtbare Linien im Spiel an
- **Biom-spezifische Pfade**: Speichert Pfade getrennt nach Biomen/Level-Bereichen
- **Einfache Bedienung**: F1-Taste öffnet das Einstellungsmenü
- **Persistente Speicherung**: Pfade werden zwischen Spielsitzungen gespeichert

## Installation

1. Stelle sicher, dass [BepInEx](https://github.com/BepInEx/BepInEx) installiert ist
2. Lade die neueste Version von FollowMe-Peak herunter
3. Extrahiere das Plugin in den `BepInEx/plugins` Ordner deines Content Warning-Verzeichnisses
4. Starte das Spiel

## Verwendung

- Das Plugin startet automatisch die Pfadaufzeichnung, wenn ein Level geladen wird
- Drücke **F1**, um das Einstellungsmenü zu öffnen/schließen
- Im Menü kannst du die Sichtbarkeit der Pfade ein-/ausschalten
- Pfade werden automatisch gespeichert, wenn du ein Lagerfeuer anzündest

## Technische Details

- **Framework**: .NET Standard 2.1
- **Abhängigkeiten**: BepInEx, Harmony, Unity Engine
- **Kompatibilität**: Content Warning

## Entwicklung

Das Projekt ist in C# geschrieben und nutzt:
- BepInEx für Plugin-Integration
- Harmony für Code-Patching
- Unity Engine für 3D-Visualisierung
- Newtonsoft.Json für Datenspeicherung

## Lizenz

Dieses Projekt steht unter der MIT-Lizenz.
