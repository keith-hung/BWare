import AppKit

/// Menu bar icon assets.
/// Uses programmatic drawing for consistent cross-version compatibility.
struct MenuBarIcons {
    /// Standard menu bar icon size
    private static let iconSize: CGFloat = 18

    /// Normal state icon (green circle)
    static var normalIcon: NSImage {
        print("[MenuBarIcons] Creating normalIcon (green)")
        return circleIcon(color: .systemGreen)
    }

    /// Alert state icon (red circle)
    static var alertIcon: NSImage {
        print("[MenuBarIcons] Creating alertIcon (red)")
        return circleIcon(color: .systemRed)
    }

    /// Disconnected state icon (gray circle)
    static var disconnectedIcon: NSImage {
        print("[MenuBarIcons] Creating disconnectedIcon (gray)")
        return circleIcon(color: .systemGray)
    }

    /// Creates a colored circle icon programmatically
    static func circleIcon(color: NSColor, size: CGFloat = 18) -> NSImage {
        print("[MenuBarIcons] circleIcon() creating \(size)x\(size) image")
        let image = NSImage(size: NSSize(width: size, height: size))
        image.lockFocus()

        // Draw filled circle
        let rect = NSRect(x: 2, y: 2, width: size - 4, height: size - 4)
        let path = NSBezierPath(ovalIn: rect)
        color.setFill()
        path.fill()

        image.unlockFocus()
        image.isTemplate = false
        print("[MenuBarIcons] circleIcon() created, size: \(image.size), isValid: \(image.isValid)")
        return image
    }
}

// MARK: - NSImage Extension for Tinting

extension NSImage {
    /// Creates a copy of the image with the specified tint color.
    func tinted(with color: NSColor) -> NSImage {
        let image = self.copy() as! NSImage
        image.lockFocus()

        color.set()

        let imageRect = NSRect(origin: .zero, size: image.size)
        imageRect.fill(using: .sourceAtop)

        image.unlockFocus()
        image.isTemplate = false
        return image
    }
}
