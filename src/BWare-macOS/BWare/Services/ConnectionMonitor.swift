import Foundation
import Network
import Combine
import AppKit

/// Monitors network connectivity and wake-from-sleep events.
class ConnectionMonitor: ObservableObject {
    /// Current network availability
    @Published private(set) var isNetworkAvailable: Bool = true

    /// Network path monitor
    private let monitor = NWPathMonitor()

    /// Dispatch queue for network monitoring
    private let queue = DispatchQueue(label: "com.bware.connection-monitor")

    /// Callback when network status changes
    var onNetworkStatusChanged: ((Bool) -> Void)?

    /// Callback when system wakes from sleep
    var onWakeFromSleep: (() -> Void)?

    init() {
        setupNetworkMonitor()
        setupWakeNotification()
    }

    deinit {
        monitor.cancel()
        NotificationCenter.default.removeObserver(self)
    }

    // MARK: - Private Methods

    private func setupNetworkMonitor() {
        monitor.pathUpdateHandler = { [weak self] path in
            DispatchQueue.main.async {
                let isAvailable = path.status == .satisfied
                self?.isNetworkAvailable = isAvailable
                self?.onNetworkStatusChanged?(isAvailable)
            }
        }
        monitor.start(queue: queue)
    }

    private func setupWakeNotification() {
        // Listen for wake from sleep
        NSWorkspace.shared.notificationCenter.addObserver(
            self,
            selector: #selector(handleWakeFromSleep),
            name: NSWorkspace.didWakeNotification,
            object: nil
        )
    }

    @objc private func handleWakeFromSleep() {
        // Give network a moment to reconnect after wake
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { [weak self] in
            self?.onWakeFromSleep?()
        }
    }
}
