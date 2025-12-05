import AppKit

/// Menu bar icon rendering using SF Symbols.
struct MenuBarIcons {
    /// Normal state (green)
    static func applyNormalIcon(to button: NSStatusBarButton) {
        print("[MenuBarIcons] Applying normalIcon (green)")
        applySymbol(to: button, color: .systemGreen)
    }

    /// Alert state (red)
    static func applyAlertIcon(to button: NSStatusBarButton) {
        print("[MenuBarIcons] Applying alertIcon (red)")
        applySymbol(to: button, color: .systemRed)
    }

    /// Disconnected state (gray)
    static func applyDisconnectedIcon(to button: NSStatusBarButton) {
        print("[MenuBarIcons] Applying disconnectedIcon (gray)")
        applySymbol(to: button, color: .systemGray)
    }

    private static func applySymbol(to button: NSStatusBarButton, color: NSColor) {
        guard let baseImage = NSImage(systemSymbolName: "circle.fill", accessibilityDescription: nil) else {
            print("[MenuBarIcons] ERROR: Failed to create SF Symbol")
            return
        }

        let config = NSImage.SymbolConfiguration(pointSize: 12, weight: .regular)
        let configuredImage = baseImage.withSymbolConfiguration(config) ?? baseImage

        // Tint the image
        let tintedImage = NSImage(size: configuredImage.size, flipped: false) { rect in
            configuredImage.draw(in: rect)
            color.set()
            rect.fill(using: .sourceAtop)
            return true
        }
        tintedImage.isTemplate = false

        button.image = tintedImage
        button.attributedTitle = NSAttributedString(string: "")

        // Force redraw
        button.needsDisplay = true
        button.window?.display()

        print("[MenuBarIcons] Applied SF Symbol with color: \(color)")
    }
}
