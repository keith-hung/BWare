# Package B-Ware Windows app for distribution
# Usage: .\scripts\package.ps1
#
# Output: Single self-contained .exe file

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$AppName = "BWare"
$OutputDir = Join-Path $ProjectDir "dist"
$CsProj = Join-Path $ProjectDir "BWare\BWare.csproj"

Write-Host "=== B-Ware Windows Packaging Script ===" -ForegroundColor Cyan
Write-Host ""

# Check for .NET SDK
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: .NET SDK not found. Please install .NET 8.0 SDK." -ForegroundColor Red
    exit 1
}

# Clean previous build
Write-Host "1. Cleaning previous build..."
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Get version info
Write-Host "2. Getting version info..."
$VersionFile = Join-Path $ProjectDir "..\..\VERSION"
if (Test-Path $VersionFile) {
    $SemVer = (Get-Content $VersionFile -Raw).Trim()
} else {
    $SemVer = "1.0.0"
}

try {
    $CommitHash = (git rev-parse --short HEAD 2>$null)
    $IsDirty = (git status --porcelain 2>$null)
    if ($IsDirty) {
        $CommitHash = "$CommitHash-dirty"
    }
} catch {
    $CommitHash = "unknown"
}
$VersionString = "$SemVer-$CommitHash"

Write-Host "   Version: $VersionString" -ForegroundColor Green

# Build release
Write-Host "4. Building release (win-x64)..."
Push-Location (Join-Path $ProjectDir "BWare")
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$OutputDir\publish"
Pop-Location

# Rename executable with version
Write-Host "5. Organizing output..."
$ExeName = "$AppName-$VersionString.exe"
Move-Item "$OutputDir\publish\$AppName.exe" "$OutputDir\$ExeName"

# Clean up publish folder
Remove-Item -Recurse -Force "$OutputDir\publish"

# Create ZIP for distribution
Write-Host "6. Creating ZIP archive..."
$ZipName = "$AppName-$VersionString-win-x64.zip"
Compress-Archive -Path "$OutputDir\$ExeName" -DestinationPath "$OutputDir\$ZipName"

# Calculate checksum
Write-Host "7. Calculating checksum..."
$Hash = Get-FileHash "$OutputDir\$ExeName" -Algorithm SHA256
"$($Hash.Hash.ToLower())  $ExeName" | Out-File -FilePath "$OutputDir\$AppName-$VersionString-win-x64.sha256" -Encoding ascii

$FileSize = (Get-Item "$OutputDir\$ExeName").Length / 1MB

Write-Host ""
Write-Host "=== Packaging Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output files:" -ForegroundColor Yellow
Write-Host "  EXE:      $OutputDir\$ExeName"
Write-Host "  ZIP:      $OutputDir\$ZipName"
Write-Host "  Checksum: $OutputDir\$AppName-$VersionString-win-x64.sha256"
Write-Host ""
Write-Host "Distribution notes:" -ForegroundColor Yellow
Write-Host "  - Single self-contained executable (no .NET runtime required)"
Write-Host "  - Windows 10 (1809+) / Windows 11 supported"
Write-Host "  - Users may see SmartScreen warning on first run"
Write-Host "  - Size: $([math]::Round($FileSize, 1)) MB"
Write-Host ""
