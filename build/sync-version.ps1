# Syncs version from root VERSION file to all project files
$version = (Get-Content "$PSScriptRoot\..\VERSION" -Raw).Trim()

Write-Host "Syncing version $version to all project files..." -ForegroundColor Cyan

# 1. C# Project
$csproj = "$PSScriptRoot\..\src\FollowMePeak.csproj"
(Get-Content $csproj) -replace '<Version>.*?</Version>', "<Version>$version</Version>" | Set-Content $csproj -Encoding UTF8
Write-Host "  ✔ src/FollowMePeak.csproj"

# 2. Plugin.cs BepInPlugin attribute
$plugin = "$PSScriptRoot\..\src\Plugin.cs"
(Get-Content $plugin) -replace '\[BepInPlugin\("com\.thomasaushh\.followmepeak", "FollowMe-Peak", ".*?"\)\]', "[BepInPlugin(`"com.thomasaushh.followmepeak`", `"FollowMe-Peak`", `"$version`")]" | Set-Content $plugin -Encoding UTF8
Write-Host "  ✔ src/Plugin.cs"

# 3. Server package.json
$package = "$PSScriptRoot\..\server\package.json"
(Get-Content $package) -replace '"version": ".*?"', '"version": "' + $version + '"' | Set-Content $package -Encoding UTF8
Write-Host "  ✔ server/package.json"

# 4. Root manifest.json
$manifest = "$PSScriptRoot\..\manifest.json"
(Get-Content $manifest) -replace '"version_number": ".*?"', '"version_number": "' + $version + '"' | Set-Content $manifest -Encoding UTF8
Write-Host "  ✔ manifest.json"

# 5. Thunderstore manifest.json
$tsManifest = "$PSScriptRoot\..\thunderstore-package\manifest.json"
(Get-Content $tsManifest) -replace '"version_number": ".*?"', '"version_number": "' + $version + '"' | Set-Content $tsManifest -Encoding UTF8
Write-Host "  ✔ thunderstore-package/manifest.json"

Write-Host "`nDone! Version $version synced to all files." -ForegroundColor Green
Write-Host "Remember to rebuild the mod and server for changes to take effect." -ForegroundColor Yellow
