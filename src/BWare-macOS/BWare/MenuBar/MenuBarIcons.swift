import AppKit

/// Menu bar icon assets.
/// Uses SF Symbols with template rendering for automatic dark mode support.
struct MenuBarIcons {
    /// Normal state icon (green circle)
    static var normalIcon: NSImage? {
        let image = NSImage(systemSymbolName: "circle.fill", accessibilityDescription: "Normal")
        image?.isTemplate = false
        return image?.tinted(with: .systemGreen)
    }

    /// Alert state icon (red circle)
    static var alertIcon: NSImage? {
        let image = NSImage(systemSymbolName: "circle.fill", accessibilityDescription: "Alert")
        image?.isTemplate = false
        return image?.tinted(with: .systemRed)
    }

    /// Disconnected state icon (gray circle)
    static var disconnectedIcon: NSImage? {
        let image = NSImage(systemSymbolName: "circle.fill", accessibilityDescription: "Disconnected")
        image?.isTemplate = false
        return image?.tinted(with: .systemGray)
    }

    /// Creates a colored circle icon programmatically
    static func circleIcon(color: NSColor, size: CGFloat = 18) -> NSImage {
        let image = NSImage(size: NSSize(width: size, height: size))
        image.lockFocus()

        // Draw filled circle
        let rect = NSRect(x: 2, y: 2, width: size - 4, height: size - 4)
        let path = NSBezierPath(ovalIn: rect)
        color.setFill()
        path.fill()

        image.unlockFocus()
        image.isTemplate = false
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
