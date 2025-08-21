@echo off
echo Building FollowMe-Peak for LOCAL development (localhost:3000)...
cd src
dotnet build --configuration Local
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Build successful! 
    echo 🌐 Server URL: http://localhost:3000
    echo 📁 Output: src\bin\Local\netstandard2.1\FollowMePeak.dll
    echo.
    echo Make sure your local server is running on port 3000
) else (
    echo ❌ Build failed!
)
pause