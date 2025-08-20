# PowerShell script to generate ApiKeys.cs from template with actual API key
# This should be run before building the mod

param(
    [Parameter(Mandatory=$false)]
    [string]$ApiKey,
    
    [Parameter(Mandatory=$false)]
    [string]$TemplateFile = "..\src\Config\ApiKeys.cs.template",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "..\src\Config\ApiKeys.cs"
)

# Try to get API key from various sources
if (-not $ApiKey) {
    # 1. Environment variable
    $ApiKey = $env:FOLLOWME_PEAK_API_KEY
}

if (-not $ApiKey) {
    # 2. Local config file (not in git)
    $configFile = "api-config.json"
    if (Test-Path $configFile) {
        $config = Get-Content $configFile | ConvertFrom-Json
        $ApiKey = $config.apiKey
    }
}

if (-not $ApiKey) {
    # 3. Prompt user
    $ApiKey = Read-Host "Enter API Key for FollowMe Peak"
}

if (-not $ApiKey -or $ApiKey.Length -lt 10) {
    Write-Error "Invalid or missing API key. API key must be at least 10 characters long."
    exit 1
}

# Check if template exists
if (-not (Test-Path $TemplateFile)) {
    Write-Error "Template file not found: $TemplateFile"
    exit 1
}

# Read template
$template = Get-Content $TemplateFile -Raw

# Replace placeholder with actual API key
$content = $template -replace '\{\{API_KEY_PLACEHOLDER\}\}', $ApiKey

# Ensure output directory exists
$outputDir = Split-Path $OutputFile -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force
}

# Write output file
$content | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "✅ ApiKeys.cs generated successfully with API key"
Write-Host "⚠️  WARNING: Do not commit the generated ApiKeys.cs file to git!"

# Optional: Create a local config file for future builds
$saveConfig = Read-Host "Save API key locally for future builds? (y/N)"
if ($saveConfig -eq 'y' -or $saveConfig -eq 'Y') {
    $configData = @{
        apiKey = $ApiKey
        generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    }
    $configData | ConvertTo-Json | Out-File -FilePath "api-config.json" -Encoding UTF8
    Write-Host "✅ API key saved to api-config.json (excluded from git)"
}