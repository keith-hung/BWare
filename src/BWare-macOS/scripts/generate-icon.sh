#!/bin/bash
# Generate B-Ware app icon: circle split diagonally (top-right to bottom-left)
# Green (top-left) / Red (bottom-right)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
ICONSET_DIR="$PROJECT_DIR/Resources/AppIcon.iconset"
ICNS_FILE="$PROJECT_DIR/Resources/AppIcon.icns"

# Colors
GREEN="#33CC66"
RED="#E64D4D"

echo "=== Generating B-Ware App Icon ==="

# Create iconset directory
rm -rf "$ICONSET_DIR"
mkdir -p "$ICONSET_DIR"

# Generate icon at specified size
generate_icon() {
    local size=$1
    local filename=$2

    # Create circle with diagonal split (top-right to bottom-left)
    # Green fills top-left, red fills bottom-right
    magick -size ${size}x${size} xc:none \
        -fill "$GREEN" -draw "polygon 0,0 ${size},0 0,${size}" \
        -fill "$RED" -draw "polygon ${size},0 ${size},${size} 0,${size}" \
        \( +clone -alpha extract -draw "fill black polygon 0,0 ${size},0 ${size},${size} 0,${size}" \
           -fill white -draw "circle $((size/2)),$((size/2)) $((size/2)),2" \) \
        -alpha off -compose CopyOpacity -composite \
        "$ICONSET_DIR/$filename"
}

# Standard sizes for macOS app icons
for size in 16 32 128 256 512; do
    echo "Generating ${size}x${size}..."
    generate_icon $size "icon_${size}x${size}.png"

    # @2x versions
    double=$((size * 2))
    generate_icon $double "icon_${size}x${size}@2x.png"
done

# Convert to icns
echo "Converting to icns..."
iconutil -c icns "$ICONSET_DIR" -o "$ICNS_FILE"

# Cleanup
rm -rf "$ICONSET_DIR"

echo ""
echo "Done! Icon created at: $ICNS_FILE"
