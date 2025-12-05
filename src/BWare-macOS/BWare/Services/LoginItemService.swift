import Foundation
import ServiceManagement

/// Manages Launch at Login functionality using SMAppService (macOS 13+).
class LoginItemService {

    /// Checks if the app is enabled as a login item.
    func isEnabled() -> Bool {
        if #available(macOS 13.0, *) {
            return SMAppService.mainApp.status == .enabled
        } else {
            // Fallback for older macOS - always return false
            return false
        }
    }

    /// Enables launch at login.
    func enableLaunchAtLogin() {
        if #available(macOS 13.0, *) {
            do {
                try SMAppService.mainApp.register()
            } catch {
                print("Failed to enable launch at login: \(error)")
            }
        }
    }

    /// Disables launch at login.
    func disableLaunchAtLogin() {
        if #available(macOS 13.0, *) {
            do {
                try SMAppService.mainApp.unregister()
            } catch {
                print("Failed to disable launch at login: \(error)")
            }
        }
    }
}
