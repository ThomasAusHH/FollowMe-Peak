# FollowMe Peak - Setup Guide

## 🔧 Development Setup

### Prerequisites
- .NET SDK 6.0 or higher
- Unity Editor (for game integration)
- Node.js 18+ (for server)
- Git

### 🔐 API Key Configuration

**Important:** This project uses an API key for server authentication. You need to configure this before building.

#### Method 1: Environment Variable (Recommended)
```bash
# Set environment variable
export FOLLOWME_PEAK_API_KEY="your-secret-api-key-here"

# On Windows PowerShell:
$env:FOLLOWME_PEAK_API_KEY="your-secret-api-key-here"
```

#### Method 2: Build Script
```bash
# Run the key generation script
cd build
./generate-api-keys.sh --api-key "your-secret-api-key-here"

# On Windows:
powershell -ExecutionPolicy Bypass -File generate-api-keys.ps1 -ApiKey "your-secret-api-key-here"
```

#### Method 3: Interactive Setup
```bash
cd build
./generate-api-keys.sh
# Will prompt for API key
```

### 🏗️ Building the Mod

1. **Generate API Keys:**
   ```bash
   cd build
   ./generate-api-keys.sh
   ```

2. **Build the mod:**
   ```bash
   cd src
   dotnet build
   ```

3. **Copy to game:**
   ```bash
   # Copy FollowMePeak.dll to your BepInEx/plugins folder
   cp src/bin/Debug/netstandard2.1/FollowMePeak.dll /path/to/game/BepInEx/plugins/
   ```

### 🖥️ Server Setup

1. **Install dependencies:**
   ```bash
   cd server
   npm install
   ```

2. **Configure environment:**
   ```bash
   # Create .env file
   echo "FOLLOWME_PEAK_API_KEY=your-secret-api-key-here" > .env
   echo "PORT=3000" >> .env
   echo "NODE_ENV=production" >> .env
   ```

3. **Start server:**
   ```bash
   npm start
   ```

## 🔒 Security Notes

- **Never commit API keys to git**
- The `src/Config/ApiKeys.cs` file is auto-generated and ignored by git
- Server uses the same API key for authentication
- Keep your API key secret and unique per deployment

## 🚀 Quick Start for Contributors

1. **Clone repository:**
   ```bash
   git clone https://github.com/your-username/FollowMe-Peak.git
   cd FollowMe-Peak
   ```

2. **Generate development API key:**
   ```bash
   cd build
   ./generate-api-keys.sh --api-key "dev-$(openssl rand -hex 16)"
   ```

3. **Build everything:**
   ```bash
   dotnet build src/
   cd server && npm install && npm start
   ```

## 📁 Project Structure

```
FollowMe-Peak/
├── src/                    # Mod source code
│   ├── Config/
│   │   └── ApiKeys.cs.template  # Template for API keys
│   └── Models/
├── server/                 # API server
├── build/                  # Build scripts
│   ├── generate-api-keys.sh
│   └── generate-api-keys.ps1
└── README-SETUP.md        # This file
```

## ⚠️ Important Files (DO NOT COMMIT)

- `src/Config/ApiKeys.cs` - Generated API key file
- `build/api-config.json` - Local API key storage
- `server/.env` - Server environment variables
- `server/data/` - Database files

These files are automatically ignored by git.

## 🐛 Troubleshooting

### "ApiKeys class not found"
Run the API key generation script first:
```bash
cd build && ./generate-api-keys.sh
```

### "API Key validation failed"
Make sure the same API key is used in both mod and server:
- Mod: Generated in `src/Config/ApiKeys.cs`
- Server: Set in `server/.env` or environment variable

### Build fails
1. Check that `ApiKeys.cs` exists and contains valid key
2. Ensure all dependencies are installed
3. Verify .NET SDK version compatibility

## 📝 Creating Releases

1. Generate production API key
2. Build mod with production key
3. Package server with production environment
4. Test authentication between mod and server
5. Create release package

---

For more information, see the main README.md file.