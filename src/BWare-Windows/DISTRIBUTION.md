# B-Ware Windows Distribution Guide

## Automated Packaging (Recommended)

Use the packaging script that matches macOS workflow:

```powershell
cd src\BWare-Windows
.\scripts\package.ps1
```

This script will:
1. Read version from `VERSION` file in repo root
2. Get current git commit hash
3. Build the release
4. Create versioned output files in `dist/`:
   - `BWare-{VERSION}-{COMMIT}.exe` (e.g., `BWare-1.0.0-91e9293.exe`)
   - `BWare-Windows-{VERSION}-{COMMIT}.zip` (contains unversioned `BWare.exe` + README.txt)

**Example output:**
```
dist/
├── BWare-1.0.0-91e9293.exe
├── BWare-Windows-1.0.0-91e9293.zip
└── README.txt
```

## Manual Build (If needed)

If you need to build manually:

```bash
cd BWare
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

**Output:** `BWare\bin\Release\net8.0-windows\win-x64\publish\BWare.exe` (~70MB)

## Uploading to GitHub Releases

After running `scripts\package.ps1`, upload the ZIP file:

```bash
# Read version from VERSION file
$version = Get-Content ..\..\VERSION -Raw
$version = $version.Trim()

# Create release (adjust ZIP filename with your commit hash)
gh release create v$version dist\BWare-Windows-$version-*.zip `
  --title "B-Ware Windows v$version" `
  --notes "Release v$version"
```

## What Users Get

Users download `BWare.exe` and follow instructions in `README.md`:
1. Place `BWare.exe` in any folder (recommended: `%LOCALAPPDATA%\BWare`)
2. Run the application
3. Optionally set up auto-start

## File Locations

- **Executable:** User chooses (portable)
- **Settings:** `%APPDATA%\BWare\settings.json`
- **Logs:** `%APPDATA%\BWare\logs\`

Settings and logs are stored separately, so the executable can be moved freely.

## Updating

Users can update by simply replacing `BWare.exe` with the new version. Settings and logs are preserved.
