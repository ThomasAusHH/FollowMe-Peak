@echo off
echo Building FollowMe-Peak for PRODUCTION RELEASE...
cd src
dotnet build --configuration Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Build successful! 
    echo 🌐 Server URL: https://followme-peak.ddns.net
    echo 📁 Output: src\bin\Release\netstandard2.1\FollowMePeak.dll
    echo.
    echo Production release build - optimized and ready for distribution
) else (
    echo ❌ Build failed!
)
pause