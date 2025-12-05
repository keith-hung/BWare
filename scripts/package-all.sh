#!/bin/bash
# Package B-Ware for all platforms
# Usage: ./scripts/package-all.sh [macos|windows|all]
#
# Platforms:
#   macos   - Build macOS app (.app, .dmg, .zip)
#   windows - Build Windows app (.exe, .zip) - cross-compile from macOS
#   all     - Build both platforms (default)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
DIST_DIR="$ROOT_DIR/dist"

# Parse arguments
PLATFORM="${1:-all}"

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║                B-Ware Multi-Platform Build                   ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""

# Get version info
if [ -f "$ROOT_DIR/VERSION" ]; then
    SEMVER=$(cat "$ROOT_DIR/VERSION" | tr -d '\n')
else
    SEMVER="1.0.0"
fi
COMMIT_HASH=$(git rev-parse --short HEAD 2>/dev/null || echo "unknown")
if [ -n "$(git status --porcelain 2>/dev/null)" ]; then
    COMMIT_HASH="${COMMIT_HASH}-dirty"
fi
VERSION_STRING="${SEMVER}-${COMMIT_HASH}"

echo "Version: $VERSION_STRING"
echo "Platform: $PLATFORM"
echo ""

# Create unified dist directory
mkdir -p "$DIST_DIR"

build_macos() {
    echo "┌────────────────────────────────────────────────────────────────┐"
    echo "│  Building macOS                                                │"
    echo "└────────────────────────────────────────────────────────────────┘"

    if [ ! -d "$ROOT_DIR/src/BWare-macOS" ]; then
        echo "  Skipping: BWare-macOS not found"
        return 0
    fi

    # Check for Swift
    if ! command -v swift &> /dev/null; then
        echo "  Skipping: Swift not available (not on macOS?)"
        return 0
    fi

    cd "$ROOT_DIR/src/BWare-macOS"
    ./scripts/package.sh

    # Copy outputs to unified dist
    echo "  Copying to unified dist..."
    cp -r dist/*.app "$DIST_DIR/" 2>/dev/null || true
    cp dist/*.dmg "$DIST_DIR/" 2>/dev/null || true
    cp dist/*.zip "$DIST_DIR/" 2>/dev/null || true

    echo "  ✓ macOS build complete"
    echo ""
}

build_windows() {
    echo "┌────────────────────────────────────────────────────────────────┐"
    echo "│  Building Windows (cross-compile)                             │"
    echo "└────────────────────────────────────────────────────────────────┘"

    if [ ! -d "$ROOT_DIR/src/BWare-Windows" ]; then
        echo "  Skipping: BWare-Windows not found"
        return 0
    fi

    # Check for .NET SDK
    if ! command -v dotnet &> /dev/null; then
        echo "  Skipping: .NET SDK not available"
        echo "  Install: https://dotnet.microsoft.com/download"
        return 0
    fi

    cd "$ROOT_DIR/src/BWare-Windows"
    ./scripts/package.sh

    # Copy outputs to unified dist
    echo "  Copying to unified dist..."
    cp dist/*.exe "$DIST_DIR/" 2>/dev/null || true
    cp dist/*.zip "$DIST_DIR/" 2>/dev/null || true
    cp dist/*.sha256 "$DIST_DIR/" 2>/dev/null || true

    echo "  ✓ Windows build complete"
    echo ""
}

# Execute builds
case "$PLATFORM" in
    macos)
        build_macos
        ;;
    windows)
        build_windows
        ;;
    all)
        build_macos
        build_windows
        ;;
    *)
        echo "Unknown platform: $PLATFORM"
        echo "Usage: $0 [macos|windows|all]"
        exit 1
        ;;
esac

# Summary
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║                    Build Complete                             ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""
echo "Output directory: $DIST_DIR"
echo ""
echo "Files:"
ls -la "$DIST_DIR" 2>/dev/null | grep -v "^total" | grep -v "^d" || echo "  (no files)"
echo ""
