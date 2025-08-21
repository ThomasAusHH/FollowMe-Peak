# 🔨 Build-Anleitung für FollowMe-Peak

## 🚀 Schnell-Build mit Batch-Dateien

### Für lokale Entwicklung (localhost:3000):
```batch
build-local.bat
```
**oder**
```bash
dotnet build --configuration Local
```

### Für Development-Tests (remote server):
```batch
build-dev.bat
```
**oder**
```bash
dotnet build --configuration Debug
```

### Für Production Release:
```batch
build-release.bat
```
**oder**
```bash
dotnet build --configuration Release
```

## 📋 Build-Konfigurationen im Detail

| Konfiguration | Server-URL | Debug | Zweck |
|---------------|------------|-------|-------|
| **Local** | `http://localhost:3000` | ✅ | Lokale Entwicklung mit eigenem Server |
| **Debug** | `https://followme-peak.ddns.net` | ✅ | Development mit Remote-Server |
| **Release** | `https://followme-peak.ddns.net` | ❌ | Production Release |

## 🖥️ Lokalen Server starten

Für die **Local**-Konfiguration müssen Sie den Server lokal laufen lassen:

```bash
cd server
npm install
npm start
```

Der lokale Server läuft dann auf: `http://localhost:3000`

## 📁 Output-Pfade

- **Local**: `src/bin/Local/netstandard2.1/FollowMePeak.dll`
- **Debug**: `src/bin/Debug/netstandard2.1/FollowMePeak.dll` 
- **Release**: `src/bin/Release/netstandard2.1/FollowMePeak.dll`

## 🔧 Compiler-Direktiven

Die Build-Konfiguration setzt automatisch diese Compiler-Direktiven:

- **Local**: `DEVELOPMENT` + `LOCAL_SERVER`
- **Debug**: `DEVELOPMENT` 
- **Release**: *(keine speziellen Direktiven)*

Diese können im Code mit `#if LOCAL_SERVER` oder `#if DEVELOPMENT` verwendet werden.