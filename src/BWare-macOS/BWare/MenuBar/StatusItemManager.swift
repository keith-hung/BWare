import AppKit
import Combine

/// Protocol for receiving alert state changes.
protocol AlertStateDelegate: AnyObject {
    func didReceiveAlertState(_ state: AlertState)
    func didChangeConnectionStatus(_ status: ConnectionStatus)
}

/// Manages the system menu bar status item and context menu.
class StatusItemManager: NSObject, AlertStateDelegate {
    /// The status bar item
    private let statusItem: NSStatusItem

    /// The context menu
    private var statusMenu: StatusMenu?

    /// Timer service for countdown management
    private let timerService = TimerService()

    /// Current alert state
    private var currentState: AlertState = .normal

    /// Current connection status
    private var connectionStatus: ConnectionStatus = .connecting

    /// Local alert state for offline handling
    private var localAlertState = LocalAlertState()

    /// Combine subscriptions
    private var cancellables = Set<AnyCancellable>()

    /// Click handler for alert triggering
    var onAlertTrigger: (() -> Void)?

    override init() {
        // Create status item with square length (standard for icons)
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)

        super.init()

        setupButton()
        setupMenu()
        setupTimerObserver()
    }

    deinit {
        statusItem.statusBar?.removeStatusItem(statusItem)
        cancellables.removeAll()
    }

    // MARK: - Public Methods

    /// Shows the status item in the menu bar.
    func show() {
        statusItem.isVisible = true
        updateIcon(for: .normal)
        updateTooltip()
    }

    /// Hides the status item from the menu bar.
    func hide() {
        statusItem.isVisible = false
    }

    /// Updates the icon based on alert status.
    func updateIcon(for status: AlertStatus) {
        guard let button = statusItem.button else { return }

        // Check connection status first
        if case .disconnected = connectionStatus {
            button.image = MenuBarIcons.disconnectedIcon
            return
        }

        switch status {
        case .normal:
            button.image = MenuBarIcons.normalIcon
        case .alert:
            button.image = MenuBarIcons.alertIcon
        }
    }

    /// Updates the tooltip with current status.
    func updateTooltip() {
        let tooltip: String

        if case .disconnected = connectionStatus {
            tooltip = "B-Ware: Disconnected"
        } else if currentState.status == .alert {
            let remaining = timerService.remainingSeconds
            tooltip = "B-Ware: Alert (\(remaining)s remaining)"
        } else {
            tooltip = "B-Ware: Normal"
        }

        statusItem.button?.toolTip = tooltip
    }

    // MARK: - AlertStateDelegate

    func didReceiveAlertState(_ state: AlertState) {
        currentState = state
        updateIcon(for: state.status)

        if state.status == .alert {
            // Sync timer with server expiration
            timerService.syncWithServer(expiresAt: state.expiresAt)
        } else {
            timerService.stop()
        }

        updateTooltip()
    }

    func didChangeConnectionStatus(_ status: ConnectionStatus) {
        connectionStatus = status
        updateIcon(for: currentState.status)
        updateTooltip()

        // If reconnected and we have a pending trigger, notify
        if status == .connected && localAlertState.pendingTrigger {
            onAlertTrigger?()
            localAlertState.reset()
        }
    }

    // MARK: - Private Methods

    private func setupButton() {
        guard let button = statusItem.button else { return }

        // Set default icon
        button.image = MenuBarIcons.normalIcon
    }

    private func setupMenu() {
        statusMenu = StatusMenu()
        statusMenu?.onSettingsRequested = { [weak self] in
            self?.openSettings()
        }
        statusMenu?.onQuitRequested = {
            NSApplication.shared.terminate(nil)
        }

        // Global monitor for clicks (works even without focus)
        NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] event in
            self?.handleGlobalClick(event)
        }

        // Local monitor for when app has focus
        NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] event in
            self?.handleGlobalClick(event)
            return event
        }
    }

    private func handleGlobalClick(_ event: NSEvent) {
        guard let button = statusItem.button,
              let window = button.window else { return }

        // Get mouse location in screen coordinates
        let mouseLocation = NSEvent.mouseLocation
        let buttonFrameInWindow = button.convert(button.bounds, to: nil)
        let buttonFrameInScreen = window.convertToScreen(buttonFrameInWindow)

        guard buttonFrameInScreen.contains(mouseLocation) else { return }

        if event.type == .leftMouseDown {
            print("[StatusItemManager] Left click detected on icon")
            handleAlertTrigger()
        } else if event.type == .rightMouseDown {
            print("[StatusItemManager] Right click detected on icon")
            showMenu()
        }
    }

    private func setupTimerObserver() {
        timerService.$remainingSeconds
            .receive(on: DispatchQueue.main)
            .sink { [weak self] _ in
                self?.updateTooltip()
            }
            .store(in: &cancellables)

        timerService.onExpired = { [weak self] in
            // Timer expired - server should handle the reset
            // but we update the local UI just in case
            self?.currentState = .normal
            self?.updateIcon(for: .normal)
            self?.updateTooltip()
        }
    }

    private func handleAlertTrigger() {
        print("[StatusItemManager] handleAlertTrigger called")

        // Only trigger if connected
        if connectionStatus == .connected {
            print("[StatusItemManager] Connected - calling onAlertTrigger")
            onAlertTrigger?()
        } else if case .disconnected = connectionStatus {
            print("[StatusItemManager] Disconnected - queuing locally")
            // Offline - queue the trigger locally
            localAlertState.queueTrigger()
            currentState = AlertState.alert(
                expiresAt: Int64(localAlertState.localExpiresAt?.timeIntervalSince1970 ?? 0),
                triggeredBy: ClientConfiguration.load()?.clientId ?? UUID().uuidString
            )
            updateIcon(for: .alert)
            timerService.start()
            updateTooltip()
        } else {
            print("[StatusItemManager] Still connecting - ignoring click")
        }
    }

    private func showMenu() {
        guard let menu = statusMenu?.menu,
              let button = statusItem.button,
              let window = button.window else { return }

        // Calculate position below the status item
        let buttonFrame = button.convert(button.bounds, to: nil)
        let screenFrame = window.convertToScreen(buttonFrame)

        // Show menu at status item location
        menu.popUp(positioning: nil, at: NSPoint(x: screenFrame.origin.x, y: screenFrame.origin.y), in: nil)
    }

    private func openSettings() {
        NotificationCenter.default.post(name: .showSettings, object: nil)
    }
}
