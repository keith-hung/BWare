import AppKit
import SwiftUI

/// Application delegate handling lifecycle and menu bar initialization.
class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    /// Status item manager - retained to prevent deallocation
    private var statusItemManager: StatusItemManager?

    /// Alert sync service for Firebase communication
    private var alertSyncService: AlertSyncService?

    /// Activity token to prevent App Nap
    private var activityToken: NSObjectProtocol?

    /// Settings window
    private var settingsWindow: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        print("[AppDelegate] Application did finish launching")

        // Register for settings notification
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(showSettingsWindow),
            name: .showSettings,
            object: nil
        )
        // Prevent App Nap to maintain real-time sync
        activityToken = ProcessInfo.processInfo.beginActivity(
            options: .userInitiatedAllowingIdleSystemSleep,
            reason: "Maintaining real-time status sync"
        )

        // Initialize menu bar item
        print("[AppDelegate] Initializing status item manager")
        statusItemManager = StatusItemManager()
        statusItemManager?.show()
        print("[AppDelegate] Status item manager initialized and shown")

        // Hide any windows on launch
        NSApplication.shared.windows.forEach { $0.close() }

        // Check for first-run setup
        if ClientConfiguration.load() == nil {
            print("[AppDelegate] No configuration found, showing first-run setup")
            showConfigurationWindow(isFirstRun: true)
        } else {
            print("[AppDelegate] Configuration found, initializing Firebase")
            initializeFirebase()
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        // Keep running when windows close - we're a menu bar app
        return false
    }

    func applicationWillTerminate(_ notification: Notification) {
        // Cleanup
        if let token = activityToken {
            ProcessInfo.processInfo.endActivity(token)
        }
        alertSyncService = nil
        statusItemManager = nil
    }

    // MARK: - Private Methods

    private func showConfigurationWindow(isFirstRun: Bool) {
        // Temporarily become regular app to accept keyboard input
        NSApp.setActivationPolicy(.regular)

        let configView = ConfigurationView(isFirstRun: isFirstRun, onSave: { [weak self] in
            DispatchQueue.main.async {
                self?.settingsWindow?.close()
                self?.settingsWindow = nil
                // Return to accessory mode
                NSApp.setActivationPolicy(.accessory)
                // Initialize or reinitialize Firebase
                self?.initializeFirebase()
            }
        })

        let hostingController = NSHostingController(rootView: configView)
        let window = NSWindow(contentViewController: hostingController)
        window.title = isFirstRun ? "B-Ware Setup" : "B-Ware Settings"
        window.styleMask = [.titled, .closable]
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()

        settingsWindow = window
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    private func initializeFirebase() {
        guard let config = ClientConfiguration.load() else { return }

        // Initialize Firebase with database URL
        alertSyncService = AlertSyncService(databaseUrl: config.databaseUrl)
        alertSyncService?.delegate = statusItemManager

        // Wire up alert trigger handler
        statusItemManager?.onAlertTrigger = { [weak self] in
            self?.alertSyncService?.triggerAlert()
        }

        // Start listening for alerts
        alertSyncService?.startListening()
    }

    @objc func showSettingsWindow() {
        // Reuse configuration window for settings
        showConfigurationWindow(isFirstRun: false)
    }

    // MARK: - NSWindowDelegate

    func windowWillClose(_ notification: Notification) {
        // Return to accessory mode when settings window closes
        NSApp.setActivationPolicy(.accessory)
    }
}

// MARK: - Notification Names

extension Notification.Name {
    static let showSettings = Notification.Name("BWareShowSettings")
}
