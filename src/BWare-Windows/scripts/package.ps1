# B-Ware Windows Packaging Script
# Usage: .\scripts\package.ps1

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$ProjectDir = Split-Path $ScriptDir
$RepoRoot = Split-Path (Split-Path $ProjectDir)
$AppName = "BWare"
$BuildDir = "$ProjectDir\BWare\bin\Release\net8.0-windows\win-x64\publish"
$OutputDir = "$ProjectDir\dist"

Write-Host "=== B-Ware Windows Packaging Script ===" -ForegroundColor Cyan
Write-Host ""

# Clean previous build
Write-Host "1. Cleaning previous build..." -ForegroundColor Green
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Read version from repository root (single source of truth)
Write-Host "2. Reading version info..." -ForegroundColor Green
$Semver = (Get-Content "$RepoRoot\VERSION" -Raw).Trim()

# Get git commit info
Push-Location $ProjectDir
try {
    $CommitHash = git rev-parse --short HEAD 2>$null
    if (-not $CommitHash) {
        $CommitHash = "unknown"
    }

    # Check if working directory is dirty
    $GitStatus = git status --porcelain 2>$null
    if ($GitStatus) {
        $CommitHash = "$CommitHash-dirty"
    }
} catch {
    $CommitHash = "unknown"
} finally {
    Pop-Location
}

Write-Host "   Version: $Semver" -ForegroundColor Gray
Write-Host "   Commit:  $CommitHash" -ForegroundColor Gray

# Build release
Write-Host "3. Building release..." -ForegroundColor Green
Push-Location "$ProjectDir\BWare"
try {
    dotnet publish BWare.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

# Create versioned filenames (e.g., 1.0.0-abc1234 or 1.0.0-abc1234-dirty)
$VersionString = "$Semver-$CommitHash"

# Copy executable with versioned name
Write-Host "4. Copying executable..." -ForegroundColor Green
$ExeName = "$AppName.exe"
$VersionedExeName = "$AppName-$VersionString.exe"
Copy-Item "$BuildDir\$ExeName" "$OutputDir\$VersionedExeName"
Write-Host "   Created: $VersionedExeName" -ForegroundColor Gray

# Copy README
Write-Host "5. Copying documentation..." -ForegroundColor Green
if (Test-Path "$ProjectDir\README.md") {
    Copy-Item "$ProjectDir\README.md" "$OutputDir\README.txt"
    Write-Host "   Copied: README.txt" -ForegroundColor Gray
}

# Create ZIP archive
Write-Host "6. Creating ZIP archive..." -ForegroundColor Green
$ZipName = "$AppName-Windows-$VersionString.zip"
$ZipPath = "$OutputDir\$ZipName"

# Create temporary directory for clean ZIP structure
$TempZipDir = "$OutputDir\temp-zip"
New-Item -ItemType Directory -Path $TempZipDir | Out-Null
Copy-Item "$OutputDir\$VersionedExeName" "$TempZipDir\$ExeName"
if (Test-Path "$OutputDir\README.txt") {
    Copy-Item "$OutputDir\README.txt" "$TempZipDir\"
}

# Use 7z if available (handles file locks better), otherwise fall back to Compress-Archive
$7zPath = Get-Command 7z -ErrorAction SilentlyContinue
if ($7zPath) {
    Push-Location $TempZipDir
    & 7z a -tzip $ZipPath * | Out-Null
    Pop-Location
} else {
    # Add retry logic for Compress-Archive due to potential file locks (antivirus, indexer)
    $maxRetries = 3
    $retryDelay = 2
    for ($i = 1; $i -le $maxRetries; $i++) {
        try {
            Start-Sleep -Seconds $retryDelay
            Compress-Archive -Path "$TempZipDir\*" -DestinationPath $ZipPath -Force
            break
        } catch {
            if ($i -eq $maxRetries) {
                throw "Failed to create ZIP after $maxRetries attempts: $_"
            }
            Write-Host "   Retry $i/$maxRetries (file may be locked by antivirus)..." -ForegroundColor Yellow
        }
    }
}
Remove-Item -Recurse -Force $TempZipDir

Write-Host "   Created: $ZipName" -ForegroundColor Gray

# Calculate file sizes
$ExeSize = [math]::Round((Get-Item "$OutputDir\$VersionedExeName").Length / 1MB, 2)
$ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)

Write-Host ""
Write-Host "=== Packaging Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output files:" -ForegroundColor White
Write-Host "  Executable: $OutputDir\$VersionedExeName ($ExeSize MB)" -ForegroundColor Gray
Write-Host "  ZIP:        $OutputDir\$ZipName ($ZipSize MB)" -ForegroundColor Gray
Write-Host ""
Write-Host "Distribution notes:" -ForegroundColor White
Write-Host "  - Upload $ZipName to GitHub Releases" -ForegroundColor Gray
Write-Host "  - ZIP contains: $ExeName (unversioned for user convenience) + README.txt" -ForegroundColor Gray
Write-Host ""
Write-Host "To create GitHub release:" -ForegroundColor Yellow
Write-Host "  gh release create v$Semver dist\$ZipName --title \"B-Ware Windows v$Semver\" --notes \"Release v$Semver ($CommitHash)\"" -ForegroundColor Gray
Write-Host ""
