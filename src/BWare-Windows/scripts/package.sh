#!/bin/bash
# Package B-Ware Windows app for distribution
# Usage: ./scripts/package.sh
#
# Can run on macOS to cross-compile for Windows (requires .NET SDK)
# Output: Single self-contained .exe file

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
APP_NAME="BWare"
OUTPUT_DIR="$PROJECT_DIR/dist"
CSPROJ="$PROJECT_DIR/BWare/BWare.csproj"

echo "=== B-Ware Windows Packaging Script ==="
echo ""

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK not found. Please install .NET 8.0 SDK."
    exit 1
fi

# Clean previous build
echo "1. Cleaning previous build..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Get version info
echo "2. Getting version info..."
# Try to read from root VERSION file, fallback to csproj
if [ -f "$PROJECT_DIR/../../VERSION" ]; then
    SEMVER=$(cat "$PROJECT_DIR/../../VERSION" | tr -d '\n')
else
    SEMVER=$(grep -oP '(?<=<Version>)[^<]+' "$CSPROJ" 2>/dev/null || echo "1.0.0")
fi

COMMIT_HASH=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
if [ -n "$(git status --porcelain 2>/dev/null)" ]; then
    COMMIT_HASH="${COMMIT_HASH}-dirty"
fi
VERSION_STRING="${SEMVER}-${COMMIT_HASH}"

echo "   Version: $VERSION_STRING"

# Update version in csproj
echo "3. Updating version in project file..."
if command -v sed &> /dev/null; then
    sed -i.bak "s|<Version>.*</Version>|<Version>$SEMVER</Version>|g" "$CSPROJ"
    rm -f "$CSPROJ.bak"
fi

# Build release
echo "4. Building release (win-x64)..."
cd "$PROJECT_DIR/BWare"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$OUTPUT_DIR/publish"

# Rename executable with version
echo "5. Organizing output..."
EXE_NAME="${APP_NAME}-${VERSION_STRING}.exe"
mv "$OUTPUT_DIR/publish/$APP_NAME.exe" "$OUTPUT_DIR/$EXE_NAME"

# Clean up publish folder (keep only the exe)
rm -rf "$OUTPUT_DIR/publish"

# Create ZIP for distribution
echo "6. Creating ZIP archive..."
cd "$OUTPUT_DIR"
zip -q "${APP_NAME}-${VERSION_STRING}-win-x64.zip" "$EXE_NAME"

# Calculate checksums
echo "7. Calculating checksums..."
if command -v sha256sum &> /dev/null; then
    sha256sum "$EXE_NAME" > "${APP_NAME}-${VERSION_STRING}-win-x64.sha256"
elif command -v shasum &> /dev/null; then
    shasum -a 256 "$EXE_NAME" > "${APP_NAME}-${VERSION_STRING}-win-x64.sha256"
fi

echo ""
echo "=== Packaging Complete ==="
echo ""
echo "Output files:"
echo "  EXE:      $OUTPUT_DIR/$EXE_NAME"
echo "  ZIP:      $OUTPUT_DIR/${APP_NAME}-${VERSION_STRING}-win-x64.zip"
echo "  Checksum: $OUTPUT_DIR/${APP_NAME}-${VERSION_STRING}-win-x64.sha256"
echo ""
echo "Distribution notes:"
echo "  - Single self-contained executable (no .NET runtime required)"
echo "  - Windows 10 (1809+) / Windows 11 supported"
echo "  - Users may see SmartScreen warning on first run"
echo "  - Size: ~$(du -h "$OUTPUT_DIR/$EXE_NAME" | cut -f1)"
echo ""
