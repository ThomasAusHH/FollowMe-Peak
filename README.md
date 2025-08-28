# FollowMe-Peak

Ein BepInEx-Plugin für Peak, das automatisch Pfade in Levels aufzeichnet und als visuelle Hilfe für andere Spieler anzeigt.

## Features

- **Automatische Pfadaufzeichnung**: Zeichnet deine Bewegungen in jedem Level automatisch auf
- **Visuelle Pfadanzeige**: Zeigt aufgezeichnete Pfade als sichtbare Linien im Spiel an
- **Biom-spezifische Pfade**: Speichert Pfade getrennt nach Biomen/Level-Bereichen
- **Cloud-Synchronisation**: Teile deine Pfade mit anderen Spielern über einen optionalen Server
- **Einfache Bedienung**: F1-Taste öffnet das Einstellungsmenü
- **Persistente Speicherung**: Pfade werden zwischen Spielsitzungen gespeichert

## Installation

1. Installiere [BepInEx](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) über den Mod Manager
2. Installiere FollowMe-Peak über deinen Mod Manager
3. Starte das Spiel

## Verwendung

- Das Plugin startet automatisch die Pfadaufzeichnung, wenn ein Level geladen wird
- Drücke **F1**, um das Einstellungsmenü zu öffnen/schließen
- Im Menü kannst du:
  - Die Sichtbarkeit der Pfade ein-/ausschalten
  - Cloud-Synchronisation konfigurieren
  - Pfade verwalten und löschen
- Pfade werden automatisch gespeichert, wenn du ein Lagerfeuer anzündest

## Cloud-Synchronisation (Optional)

Das Plugin unterstützt einen optionalen Server für das Teilen von Pfaden:
- Lade Pfade hoch, um sie mit anderen zu teilen
- Lade Pfade von anderen Spielern herunter
- Automatische Synchronisation beim Level-Start

Server-Setup ist optional und das Plugin funktioniert vollständig offline.

## Kompatibilität

- **Spiel**: Peak
- **Framework**: BepInEx 5.4.21+
- **Plattform**: Windows/Steam

## Problembehebung

Falls das Plugin nicht funktioniert:
1. Stelle sicher, dass BepInEx korrekt installiert ist
2. Überprüfe die BepInEx-Logs in `BepInEx/LogOutput.log`
3. Starte das Spiel neu nach der Installation

## Changelog

### v0.1.0
- Initiale Veröffentlichung
- Grundlegende Pfadaufzeichnung und -visualisierung
- Cloud-Synchronisation
- Benutzeroberfläche mit F1

## Support

Bei Problemen oder Fragen erstelle ein Issue im [GitHub Repository](https://github.com/ThomasAusHH/FollowMe-Peak).
