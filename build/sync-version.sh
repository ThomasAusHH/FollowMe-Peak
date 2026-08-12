#!/bin/bash
# Syncs version from root VERSION file to all project files

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION=$(cat "$SCRIPT_DIR/../VERSION" | tr -d '[:space:]')

echo -e "\033[36mSyncing version $VERSION to all project files...\033[0m"

# 1. C# Project
sed -i "s|<Version>.*</Version>|<Version>$VERSION</Version>|g" "$SCRIPT_DIR/../src/FollowMePeak.csproj"
echo "  ✔ src/FollowMePeak.csproj"

# 2. Plugin.cs BepInPlugin attribute
sed -i "s|\[BepInPlugin(\"com.thomasaushh.followmepeak\", \"FollowMe-Peak\", \".*\")\]|[BepInPlugin(\"com.thomasaushh.followmepeak\", \"FollowMe-Peak\", \"$VERSION\")]|g" "$SCRIPT_DIR/../src/Plugin.cs"
echo "  ✔ src/Plugin.cs"

# 3. Server package.json
sed -i "s|\"version\": \".*\"|\"version\": \"$VERSION\"|g" "$SCRIPT_DIR/../server/package.json"
echo "  ✔ server/package.json"

# 4. Root manifest.json
sed -i "s|\"version_number\": \".*\"|\"version_number\": \"$VERSION\"|g" "$SCRIPT_DIR/../manifest.json"
echo "  ✔ manifest.json"

# 5. Thunderstore manifest.json
sed -i "s|\"version_number\": \".*\"|\"version_number\": \"$VERSION\"|g" "$SCRIPT_DIR/../thunderstore-package/manifest.json"
echo "  ✔ thunderstore-package/manifest.json"

echo -e "\n\033[32mDone! Version $VERSION synced to all files.\033[0m"
echo -e "\033[33mRemember to rebuild the mod and server for changes to take effect.\033[0m"
