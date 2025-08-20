# FollowMe Peak Mod - Development Setup

## 📝 About
This repository contains **only the mod source code**. The server is private and hosted separately by the project maintainers.

## 🔧 Development Setup

### Prerequisites
- .NET SDK 6.0 or higher
- Unity Editor (for game integration)
- Git

### 🔐 API Key Configuration

**Important:** The mod uses an API key to authenticate with the private server. You need this key to build and test the mod.

#### Getting the API Key
- **Contributors:** Contact the project maintainers for the current API key
- **Testers:** Use the public test key provided in releases
- **Maintainers:** Use the production key from secure storage

#### Method 1: Environment Variable (Recommended)
```bash
# Set environment variable
export FOLLOWME_PEAK_API_KEY="your-api-key-here"

# On Windows PowerShell:
$env:FOLLOWME_PEAK_API_KEY="your-api-key-here"
```

#### Method 2: Build Script
```bash
# Run the key generation script
cd build
./generate-api-keys.sh --api-key "your-api-key-here"

# On Windows:
powershell -ExecutionPolicy Bypass -File generate-api-keys.ps1 -ApiKey "your-api-key-here"
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

## 🌐 Server Information

- **Server Location:** Hosted privately by project maintainers
- **API Endpoint:** `http://localhost:3000` (for local testing) or production URL
- **Authentication:** API key required for all requests
- **Rate Limits:** 50 uploads per hour per user

## 🚀 Quick Start for Contributors

1. **Clone repository:**
   ```bash
   git clone https://github.com/your-username/FollowMe-Peak.git
   cd FollowMe-Peak
   ```

2. **Get API key from maintainers**

3. **Generate development build:**
   ```bash
   cd build
   ./generate-api-keys.sh --api-key "provided-dev-key"
   ```

4. **Build and test:**
   ```bash
   dotnet build src/
   # Copy DLL to game and test
   ```

## 📁 Project Structure

```
FollowMe-Peak/
├── src/                    # Mod source code
│   ├── Config/
│   │   └── ApiKeys.cs.template  # Template for API keys
│   ├── Models/
│   ├── Services/
│   └── UI/
├── build/                  # Build scripts
│   ├── generate-api-keys.sh
│   └── generate-api-keys.ps1
└── README-MOD-SETUP.md    # This file
```

## ⚠️ Important Files (DO NOT COMMIT)

- `src/Config/ApiKeys.cs` - Generated API key file
- `build/api-config.json` - Local API key storage

These files are automatically ignored by git.

## 🐛 Troubleshooting

### "ApiKeys class not found"
Run the API key generation script first:
```bash
cd build && ./generate-api-keys.sh
```

### "API Key validation failed"
- Contact maintainers for current API key
- Ensure you're using the correct key for your environment (dev/test/prod)

### Build fails
1. Check that `ApiKeys.cs` exists and contains valid key
2. Ensure all dependencies are installed
3. Verify .NET SDK version compatibility

## 🤝 Contributing

1. **Fork the repository**
2. **Get API key from maintainers**
3. **Create feature branch**
4. **Build and test locally**
5. **Submit pull request**

**Note:** The server code is not included in this repository and is maintained separately.

## 📝 Release Process

1. **Test with dev API key**
2. **Build with production API key** (maintainers only)
3. **Package mod DLL**
4. **Create release with installation instructions**

---

For game installation instructions, see the main README.md file.