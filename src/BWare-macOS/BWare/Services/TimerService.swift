import Foundation
import Combine

/// Manages the 60-second countdown timer for alert state.
class TimerService: ObservableObject {
    /// Remaining seconds in the countdown
    @Published private(set) var remainingSeconds: Int = 0

    /// Whether the timer is currently running
    @Published private(set) var isRunning: Bool = false

    /// Called when the timer expires
    var onExpired: (() -> Void)?

    /// The countdown timer
    private var timer: Timer?

    /// Target expiration date
    private var expirationDate: Date?

    /// Default alert duration in seconds
    static let alertDuration: TimeInterval = 60

    deinit {
        stop()
    }

    // MARK: - Public Methods

    /// Starts a new 60-second countdown timer.
    func start() {
        let expiration = Date().addingTimeInterval(Self.alertDuration)
        startCountdown(until: expiration)
    }

    /// Stops the timer and resets the countdown.
    func stop() {
        timer?.invalidate()
        timer = nil
        expirationDate = nil
        remainingSeconds = 0
        isRunning = false
    }

    /// Resets the timer to 60 seconds from now.
    func reset() {
        stop()
        start()
    }

    /// Syncs the timer with a server-provided expiration timestamp.
    /// - Parameter expiresAt: Unix timestamp (seconds) when the alert expires
    func syncWithServer(expiresAt: Int64) {
        let expiration = Date(timeIntervalSince1970: TimeInterval(expiresAt))
        startCountdown(until: expiration)
    }

    // MARK: - Private Methods

    private func startCountdown(until expiration: Date) {
        stop()

        expirationDate = expiration
        updateRemainingSeconds()

        guard remainingSeconds > 0 else {
            onExpired?()
            return
        }

        isRunning = true

        // Create timer that fires every second
        timer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            self?.tick()
        }

        // Ensure timer runs even when menu is open
        RunLoop.current.add(timer!, forMode: .common)
    }

    private func tick() {
        updateRemainingSeconds()

        if remainingSeconds <= 0 {
            stop()
            onExpired?()
        }
    }

    private func updateRemainingSeconds() {
        guard let expiration = expirationDate else {
            remainingSeconds = 0
            return
        }

        let remaining = expiration.timeIntervalSinceNow
        remainingSeconds = max(0, Int(remaining.rounded()))
    }
}
