import AppKit

/// Constructs and manages the context menu for the status item.
class StatusMenu: NSObject {
    /// The NSMenu instance
    let menu: NSMenu

    /// Called when user selects Settings
    var onSettingsRequested: (() -> Void)?

    /// Called when user selects Quit
    var onQuitRequested: (() -> Void)?

    override init() {
        menu = NSMenu()

        super.init()

        setupMenu()
    }

    // MARK: - Private Methods

    private func setupMenu() {
        // Settings item
        let settingsItem = NSMenuItem(
            title: "Settings...",
            action: #selector(handleSettings),
            keyEquivalent: ","
        )
        settingsItem.keyEquivalentModifierMask = .command
        settingsItem.target = self
        menu.addItem(settingsItem)

        // Separator
        menu.addItem(NSMenuItem.separator())

        // Quit item
        let quitItem = NSMenuItem(
            title: "Quit B-Ware",
            action: #selector(handleQuit),
            keyEquivalent: "q"
        )
        quitItem.keyEquivalentModifierMask = .command
        quitItem.target = self
        menu.addItem(quitItem)
    }

    @objc private func handleSettings() {
        onSettingsRequested?()
    }

    @objc private func handleQuit() {
        onQuitRequested?()
    }
}
