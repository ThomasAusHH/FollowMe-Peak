#!/bin/bash
echo "Building FollowMe-Peak for PRODUCTION release..."
cd src
dotnet build --configuration Release
if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Build successful!"
    echo "🌐 Server URL: https://followme-peak.ddns.net"
    echo "📁 Output: src/bin/Release/netstandard2.1/FollowMePeak.dll"
    echo ""
    echo "Ready for production deployment"
else
    echo "❌ Build failed!"
fi