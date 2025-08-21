@echo off
echo Building FollowMe-Peak for PRODUCTION release...
cd src
dotnet build --configuration Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo  Build successful! 
    echo < Server URL: https://followme-peak.ddns.net
    echo =Á Output: src\bin\Release\netstandard2.1\FollowMePeak.dll
    echo.
    echo Ready for production deployment
) else (
    echo L Build failed!
)
pause