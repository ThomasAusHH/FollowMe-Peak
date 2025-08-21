# Build Configurations

## Available Build Modes

### 🏠 Local Development
**Server:** `http://localhost:3000`  
**Use for:** Testing against your local development server

**Commands:**
```bash
# Using script
./build-local.sh        # Linux/Mac
build-local.bat         # Windows

# Direct command
dotnet build --configuration Local
```

### 🧪 Development
**Server:** `https://followme-peak.ddns.net`  
**Use for:** Testing against production server

**Commands:**
```bash
# Using script  
./build-dev.sh          # Linux/Mac
build-dev.bat           # Windows

# Direct command
dotnet build --configuration Debug
```

### 🚀 Production
**Server:** `https://followme-peak.ddns.net`  
**Use for:** Final builds for release

**Commands:**
```bash
# Using script
./build-production.sh   # Linux/Mac  
build-production.bat    # Windows

# Direct command
dotnet build --configuration Release
```

## Build Configuration Details

The build configurations use C# preprocessor directives to switch server URLs:

- **Local**: `LOCAL_SERVER` + `DEVELOPMENT` flags
- **Debug**: `DEVELOPMENT` flag only
- **Release**: No special flags (production)

## Output Locations

- Local: `src/bin/Local/netstandard2.1/FollowMePeak.dll`
- Debug: `src/bin/Debug/netstandard2.1/FollowMePeak.dll`  
- Release: `src/bin/Release/netstandard2.1/FollowMePeak.dll`

## Local Server Setup

To use Local builds, make sure your development server is running:

```bash
cd server
npm install
npm start
```

The server should be accessible at `http://localhost:3000`