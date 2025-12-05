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

# Build release
echo "2. Building release..."
cd "$PROJECT_DIR"
swift build -c release

# Create app bundle structure
echo "3. Creating app bundle..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy executable
cp "$BUILD_DIR/$APP_NAME" "$APP_BUNDLE/Contents/MacOS/"

# Copy Info.plist
cp "$PROJECT_DIR/Resources/Info.plist" "$APP_BUNDLE/Contents/"

# Create PkgInfo
echo -n "APPL????" > "$APP_BUNDLE/Contents/PkgInfo"

# Create app icon (using SF Symbol as placeholder)
# For a real app, you'd want to create an .icns file
echo "4. Note: Using system default icon. Add AppIcon.icns to Resources/ for custom icon."

# Set executable permission
chmod +x "$APP_BUNDLE/Contents/MacOS/$APP_NAME"

# Remove quarantine attribute (for local testing)
xattr -cr "$APP_BUNDLE" 2>/dev/null || true

# Create ZIP for distribution
echo "5. Creating ZIP archive..."
cd "$OUTPUT_DIR"
zip -r -q "$APP_NAME.zip" "$APP_NAME.app"

# Create DMG (optional, nicer for distribution)
echo "6. Creating DMG..."
DMG_TMP="$OUTPUT_DIR/tmp.dmg"
DMG_FINAL="$OUTPUT_DIR/$APP_NAME.dmg"

# Remove existing DMG files
rm -f "$DMG_TMP" "$DMG_FINAL"

# Create DMG directly from folder
hdiutil create -volname "$APP_NAME" -srcfolder "$APP_BUNDLE" -ov -format UDZO "$DMG_FINAL" -quiet

echo ""
echo "=== Packaging Complete ==="
echo ""
echo "Output files:"
echo "  App:  $APP_BUNDLE"
echo "  ZIP:  $OUTPUT_DIR/$APP_NAME.zip"
echo "  DMG:  $DMG_FINAL"
echo ""
echo "Distribution notes:"
echo "  - This app is NOT signed or notarized"
echo "  - Users must: Right-click > Open > Allow"
echo "  - Or: System Settings > Privacy & Security > Allow"
echo ""
