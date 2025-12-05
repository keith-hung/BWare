#!/bin/bash
# Package B-Ware macOS app for distribution (unsigned)
# Usage: ./scripts/package.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
APP_NAME="BWare"
BUILD_DIR="$PROJECT_DIR/.build/release"
OUTPUT_DIR="$PROJECT_DIR/dist"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"

echo "=== B-Ware Packaging Script ==="
echo ""

# Clean previous build
echo "1. Cleaning previous build..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Generate version info
echo "2. Generating version info..."
"$SCRIPT_DIR/generate-version.sh"

# Read version for Info.plist
SEMVER=$(cat "$PROJECT_DIR/VERSION" | tr -d '\n')
COMMIT_HASH=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
if [ -n "$(git status --porcelain 2>/dev/null)" ]; then
    COMMIT_HASH="${COMMIT_HASH}-dirty"
fi

# Build release
echo "3. Building release..."
cd "$PROJECT_DIR"
swift build -c release

# Create app bundle structure
echo "4. Creating app bundle..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy executable
cp "$BUILD_DIR/$APP_NAME" "$APP_BUNDLE/Contents/MacOS/"

# Copy and update Info.plist with version
cp "$PROJECT_DIR/Resources/Info.plist" "$APP_BUNDLE/Contents/"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $SEMVER" "$APP_BUNDLE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $COMMIT_HASH" "$APP_BUNDLE/Contents/Info.plist"

# Create PkgInfo
echo -n "APPL????" > "$APP_BUNDLE/Contents/PkgInfo"

# Create app icon (using SF Symbol as placeholder)
# For a real app, you'd want to create an .icns file
echo "5. Note: Using system default icon. Add AppIcon.icns to Resources/ for custom icon."

# Set executable permission
chmod +x "$APP_BUNDLE/Contents/MacOS/$APP_NAME"

# Remove quarantine attribute (for local testing)
xattr -cr "$APP_BUNDLE" 2>/dev/null || true

# Create versioned filenames (e.g., 0.1.0-abc1234 or 0.1.0-abc1234-dirty)
VERSION_STRING="${SEMVER}-${COMMIT_HASH}"

# Create ZIP for distribution
echo "6. Creating ZIP archive..."
cd "$OUTPUT_DIR"
zip -r -q "${APP_NAME}-${VERSION_STRING}.zip" "$APP_NAME.app"

# Create DMG (optional, nicer for distribution)
echo "7. Creating DMG..."
DMG_FINAL="$OUTPUT_DIR/${APP_NAME}-${VERSION_STRING}.dmg"

# Remove existing DMG files
rm -f "$DMG_FINAL"

# Create DMG directly from folder
hdiutil create -volname "$APP_NAME" -srcfolder "$APP_BUNDLE" -ov -format UDZO "$DMG_FINAL" -quiet

echo ""
echo "=== Packaging Complete ==="
echo ""
echo "Output files:"
echo "  App:  $APP_BUNDLE"
echo "  ZIP:  $OUTPUT_DIR/${APP_NAME}-${VERSION_STRING}.zip"
echo "  DMG:  $DMG_FINAL"
echo ""
echo "Distribution notes:"
echo "  - This app is NOT signed or notarized"
echo "  - Users must: Right-click > Open > Allow"
echo "  - Or: System Settings > Privacy & Security > Allow"
echo ""
