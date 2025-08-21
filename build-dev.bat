@echo off
echo Building FollowMe-Peak for DEVELOPMENT (production server)...
cd src
dotnet build --configuration Debug
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Build successful! 
    echo 🌐 Server URL: https://followme-peak.ddns.net
    echo 📁 Output: src\bin\Debug\netstandard2.1\FollowMePeak.dll
    echo.
    echo Using production server for development testing
) else (
    echo ❌ Build failed!
)
pause