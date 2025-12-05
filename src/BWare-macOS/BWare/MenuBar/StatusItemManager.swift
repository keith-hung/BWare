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

    /// Event monitors for click handling
    private var globalMonitor: Any?
    private var localMonitor: Any?

    /// Click handler for alert triggering
    var onAlertTrigger: (() -> Void)?

    override init() {
        print("[StatusItemManager] init() started")
        // Create status item with square length (standard for icons)
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        print("[StatusItemManager] statusItem created, button exists: \(statusItem.button != nil)")

        super.init()

        setupButton()
        setupMenu()
        setupTimerObserver()
        setupButtonAction()
        print("[StatusItemManager] init() completed")
    }

    deinit {
        // Remove event monitors
        if let monitor = globalMonitor {
            NSEvent.removeMonitor(monitor)
        }
        if let monitor = localMonitor {
            NSEvent.removeMonitor(monitor)
        }
        statusItem.statusBar?.removeStatusItem(statusItem)
        cancellables.removeAll()
    }

    // MARK: - Public Methods

    /// Shows the status item in the menu bar.
    func show() {
        print("[StatusItemManager] show() called")
        statusItem.isVisible = true
        print("[StatusItemManager] statusItem.isVisible set to true")
        updateIcon(for: currentState.status)
        updateTooltip()

        // Diagnostic info
        if let button = statusItem.button {
            print("[StatusItemManager] === DIAGNOSTIC INFO ===")
            print("[StatusItemManager] button.frame: \(button.frame)")
            print("[StatusItemManager] button.bounds: \(button.bounds)")
            print("[StatusItemManager] button.isHidden: \(button.isHidden)")
            print("[StatusItemManager] button.alphaValue: \(button.alphaValue)")
            print("[StatusItemManager] button.image: \(String(describing: button.image))")
            print("[StatusItemManager] button.image?.size: \(String(describing: button.image?.size))")
            print("[StatusItemManager] button.image?.isValid: \(String(describing: button.image?.isValid))")
            if let window = button.window {
                print("[StatusItemManager] button.window exists: true")
                print("[StatusItemManager] window.isVisible: \(window.isVisible)")
                print("[StatusItemManager] window.frame: \(window.frame)")
                let screenFrame = window.convertToScreen(button.frame)
                print("[StatusItemManager] button screen position: \(screenFrame)")
            } else {
                print("[StatusItemManager] button.window exists: false  ⚠️ NO WINDOW!")
            }
            print("[StatusItemManager] === END DIAGNOSTIC ===")
        }
        print("[StatusItemManager] show() completed, button exists: \(statusItem.button != nil)")
    }

    /// Hides the status item from the menu bar.
    func hide() {
        statusItem.isVisible = false
    }

    /// Updates the icon based on alert status.
    func updateIcon(for status: AlertStatus) {
        print("[StatusItemManager] updateIcon called, status: \(status.rawValue)")
        guard let button = statusItem.button else {
            print("[StatusItemManager] ERROR: button is nil in updateIcon!")
            return
        }

        // Check connection status first - show gray for connecting or disconnected
        switch connectionStatus {
        case .connecting, .disconnected:
            print("[StatusItemManager] Setting disconnected icon (gray)")
            button.image = MenuBarIcons.disconnectedIcon
            return
        case .connected:
            break
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

        switch connectionStatus {
        case .connecting:
            tooltip = "B-Ware: Connecting..."
        case .disconnected:
            tooltip = "B-Ware: Disconnected"
        case .connected:
            if currentState.status == .alert {
                let remaining = timerService.remainingSeconds
                tooltip = "B-Ware: Alert (\(remaining)s remaining)"
            } else {
                tooltip = "B-Ware: Normal"
            }
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
        guard let button = statusItem.button else {
            print("[StatusItemManager] Error: statusItem.button is nil")
            return
        }

        // Set default icon
        let icon = MenuBarIcons.normalIcon
        print("[StatusItemManager] Setting initial icon, size: \(icon.size)")
        button.image = icon
    }

    private func setupMenu() {
        statusMenu = StatusMenu()
        statusMenu?.onSettingsRequested = { [weak self] in
            self?.openSettings()
        }
        statusMenu?.onQuitRequested = {
            NSApplication.shared.terminate(nil)
        }
    }

    private func setupButtonAction() {
        // Use event monitors for click handling (works reliably on macOS 12+)
        // Global monitor: captures clicks when other apps have focus
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] event in
            self?.handleClick(event)
        }

        // Local monitor: captures clicks when this app has focus
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] event in
            self?.handleClick(event)
            return event
        }
        print("[StatusItemManager] Event monitors configured")
    }

    private func handleClick(_ event: NSEvent) {
        guard let button = statusItem.button,
              let window = button.window else { return }

        // Check if click is within status item bounds
        let mouseLocation = NSEvent.mouseLocation
        let buttonFrameInWindow = button.convert(button.bounds, to: nil)
        let buttonFrameInScreen = window.convertToScreen(buttonFrameInWindow)

        guard buttonFrameInScreen.contains(mouseLocation) else { return }

        if event.type == .leftMouseDown {
            print("[StatusItemManager] Left click detected")
            handleAlertTrigger()
        } else if event.type == .rightMouseDown {
            print("[StatusItemManager] Right click detected")
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
        print("[StatusItemManager] handleAlertTrigger called, currentState: \(currentState.status.rawValue)")

        // Ignore if already in alert state
        if currentState.status == .alert {
            print("[StatusItemManager] Already in alert state - ignoring click")
            return
        }

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
