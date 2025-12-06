import Foundation
import Combine

/// Manages real-time synchronization of alert state with Firebase REST API.
class AlertSyncService: NSObject, ObservableObject, URLSessionDataDelegate {
    /// Current alert state from Firebase
    @Published private(set) var alertState: AlertState = .normal

    /// Current connection status
    @Published private(set) var connectionStatus: ConnectionStatus = .connecting

    /// Delegate for state change notifications
    weak var delegate: AlertStateDelegate?

    /// Base URL for Firebase REST API
    private let baseUrl: String

    /// Path to monitor (e.g., "light")
    private let path: String

    /// URLSession for SSE connection
    private var sseSession: URLSession?

    /// Current SSE task
    private var sseTask: URLSessionDataTask?

    /// Buffer for SSE data
    private var sseBuffer = Data()

    /// Client configuration
    private let clientId: String

    /// Retry timer
    private var retryTimer: Timer?

    /// Health check timer for detecting stale connections
    private var healthCheckTimer: Timer?

    /// Last time we received any data from SSE (including keep-alive)
    private var lastDataReceivedTime: Date?

    /// Connection timeout threshold (Firebase sends keep-alive every ~30s)
    private let connectionTimeoutSeconds: TimeInterval = 90

    /// Combine subscriptions
    private var cancellables = Set<AnyCancellable>()

    init(databaseUrl: String) {
        guard let parsed = FirebaseConfig.parseFullUrl(databaseUrl) else {
            self.baseUrl = ""
            self.path = "alerts"
            self.clientId = UUID().uuidString
            super.init()
            connectionStatus = .disconnected(reason: .authenticationFailed)
            return
        }

        self.baseUrl = parsed.rootUrl
        self.path = parsed.path
        self.clientId = ClientConfiguration.load()?.clientId ?? UUID().uuidString

        super.init()

        setupSSESession()
    }

    deinit {
        stopListening()
    }

    // MARK: - Public Methods

    /// Starts listening for alert state changes via SSE.
    func startListening() {
        guard !baseUrl.isEmpty else {
            print("[AlertSyncService] baseUrl is empty, cannot start listening")
            return
        }

        print("[AlertSyncService] Starting to listen, baseUrl: \(baseUrl), path: \(path)")
        connectionStatus = .connecting
        delegate?.didChangeConnectionStatus(connectionStatus)

        connectSSE()
    }

    /// Stops listening for alert state changes.
    func stopListening() {
        sseTask?.cancel()
        sseTask = nil
        retryTimer?.invalidate()
        retryTimer = nil
        healthCheckTimer?.invalidate()
        healthCheckTimer = nil
    }

    /// Triggers an alert (sets status to alert with 60s expiration).
    func triggerAlert() {
        print("[AlertSyncService] triggerAlert called")
        let now = Int64(Date().timeIntervalSince1970)
        let expiresAt = now + Int64(TimerService.alertDuration)

        let alertData: [String: Any] = [
            "status": AlertStatus.alert.rawValue,
            "expiresAt": expiresAt,
            "triggeredBy": clientId,
            "triggeredAt": now
        ]

        print("[AlertSyncService] Writing alert data to \(baseUrl)/\(path).json")
        writeData(alertData)
    }

    /// Resets the alert to normal state.
    func resetToNormal() {
        let normalData: [String: Any] = [
            "status": AlertStatus.normal.rawValue,
            "expiresAt": 0
        ]

        writeData(normalData)
    }

    // MARK: - Private Methods

    private func setupSSESession() {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = TimeInterval(INT_MAX)
        config.timeoutIntervalForResource = TimeInterval(INT_MAX)
        sseSession = URLSession(configuration: config, delegate: self, delegateQueue: nil)
    }

    private func connectSSE() {
        let urlString = "\(baseUrl)/\(path).json"
        guard let url = URL(string: urlString) else {
            connectionStatus = .disconnected(reason: .authenticationFailed)
            delegate?.didChangeConnectionStatus(connectionStatus)
            return
        }

        var request = URLRequest(url: url)
        request.setValue("text/event-stream", forHTTPHeaderField: "Accept")
        request.setValue("no-cache", forHTTPHeaderField: "Cache-Control")

        sseBuffer = Data()
        sseTask = sseSession?.dataTask(with: request)
        sseTask?.resume()
    }

    private func writeData(_ data: [String: Any]) {
        let urlString = "\(baseUrl)/\(path).json"
        guard let url = URL(string: urlString) else { return }

        var request = URLRequest(url: url)
        request.httpMethod = "PUT"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        do {
            request.httpBody = try JSONSerialization.data(withJSONObject: data)
        } catch {
            print("Failed to serialize data: \(error)")
            return
        }

        URLSession.shared.dataTask(with: request) { [weak self] _, response, error in
            if let error = error {
                print("Write error: \(error)")
                return
            }

            if let httpResponse = response as? HTTPURLResponse,
               httpResponse.statusCode != 200 {
                print("Write failed with status: \(httpResponse.statusCode)")
            }
        }.resume()
    }

    private func handleSSEData(_ data: Data) {
        guard let text = String(data: data, encoding: .utf8) else { return }

        // Parse SSE format: "event: ...\ndata: ...\n\n"
        let lines = text.components(separatedBy: "\n")

        for line in lines {
            if line.hasPrefix("data: ") {
                let jsonString = String(line.dropFirst(6))
                parseAlertData(jsonString)
            }
        }
    }

    private func parseAlertData(_ jsonString: String) {
        guard let data = jsonString.data(using: .utf8) else { return }

        do {
            // Firebase SSE sends: {"path": "/", "data": {...}}
            if let wrapper = try JSONSerialization.jsonObject(with: data) as? [String: Any],
               let alertData = wrapper["data"] as? [String: Any] {
                processAlertData(alertData)
            } else if let alertData = try JSONSerialization.jsonObject(with: data) as? [String: Any] {
                // Direct data format
                processAlertData(alertData)
            }
        } catch {
            // Might be "null" for empty data
            if jsonString.trimmingCharacters(in: .whitespaces) == "null" {
                DispatchQueue.main.async { [weak self] in
                    self?.alertState = .normal
                    self?.delegate?.didReceiveAlertState(.normal)
                }
            }
        }
    }

    private func processAlertData(_ data: [String: Any]) {
        print("[AlertSyncService] Processing alert data: \(data)")
        DispatchQueue.main.async { [weak self] in
            guard let self = self else { return }

            guard let statusString = data["status"] as? String,
                  let status = AlertStatus(rawValue: statusString) else {
                print("[AlertSyncService] Invalid or missing status, defaulting to normal")
                self.alertState = .normal
                self.delegate?.didReceiveAlertState(.normal)
                return
            }

            let expiresAt = (data["expiresAt"] as? Int64) ?? 0
            let triggeredBy = data["triggeredBy"] as? String
            let triggeredAt = data["triggeredAt"] as? Int64

            // Check if alert has expired
            let now = Int64(Date().timeIntervalSince1970)
            print("[AlertSyncService] Expiry check: now=\(now), expiresAt=\(expiresAt), expired=\(expiresAt <= now)")
            if status == .alert && expiresAt <= now {
                print("[AlertSyncService] Alert expired, resetting to normal")
                self.resetToNormal()
                return
            }

            self.alertState = AlertState(
                status: status,
                expiresAt: expiresAt,
                triggeredBy: triggeredBy,
                triggeredAt: triggeredAt
            )

            print("[AlertSyncService] Alert state updated: status=\(status.rawValue), expiresAt=\(expiresAt)")
            self.delegate?.didReceiveAlertState(self.alertState)
        }
    }

    private func scheduleRetry() {
        retryTimer?.invalidate()
        retryTimer = Timer.scheduledTimer(withTimeInterval: 5.0, repeats: false) { [weak self] _ in
            self?.connectSSE()
        }
    }

    private func startHealthCheck() {
        healthCheckTimer?.invalidate()
        // Check connection health every 30 seconds
        healthCheckTimer = Timer.scheduledTimer(withTimeInterval: 30.0, repeats: true) { [weak self] _ in
            self?.checkConnectionHealth()
        }
        print("[AlertSyncService] Health check timer started (interval: 30s, timeout: \(connectionTimeoutSeconds)s)")
    }

    private func checkConnectionHealth() {
        guard connectionStatus == .connected else { return }

        guard let lastReceived = lastDataReceivedTime else {
            // No data ever received but marked as connected - reconnect
            print("[AlertSyncService] Health check: No data ever received, reconnecting...")
            reconnectSSE()
            return
        }

        let timeSinceLastData = Date().timeIntervalSince(lastReceived)
        print("[AlertSyncService] Health check: \(Int(timeSinceLastData))s since last data")

        if timeSinceLastData > connectionTimeoutSeconds {
            print("[AlertSyncService] Health check: Connection stale (\(Int(timeSinceLastData))s > \(Int(connectionTimeoutSeconds))s), reconnecting...")
            reconnectSSE()
        }
    }

    private func reconnectSSE() {
        // Stop current connection
        sseTask?.cancel()
        sseTask = nil
        healthCheckTimer?.invalidate()
        healthCheckTimer = nil

        // Update status
        connectionStatus = .connecting
        delegate?.didChangeConnectionStatus(connectionStatus)

        // Reconnect
        connectSSE()
    }

    // MARK: - URLSessionDataDelegate

    func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive data: Data) {
        // Update last data received time for health monitoring
        lastDataReceivedTime = Date()

        // Connected successfully
        DispatchQueue.main.async { [weak self] in
            if self?.connectionStatus != .connected {
                print("[AlertSyncService] SSE connected successfully")
                self?.connectionStatus = .connected
                self?.delegate?.didChangeConnectionStatus(.connected)
                self?.startHealthCheck()
            }
        }

        print("[AlertSyncService] Received SSE data: \(data.count) bytes")
        sseBuffer.append(data)

        // Process complete SSE messages (end with \n\n)
        if let text = String(data: sseBuffer, encoding: .utf8),
           text.contains("\n\n") {
            handleSSEData(sseBuffer)
            sseBuffer = Data()
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        DispatchQueue.main.async { [weak self] in
            if let error = error as NSError?, error.code != NSURLErrorCancelled {
                print("[AlertSyncService] SSE connection error: \(error.localizedDescription)")
                self?.connectionStatus = .disconnected(reason: .networkUnavailable)
                self?.delegate?.didChangeConnectionStatus(.disconnected(reason: .networkUnavailable))
                self?.scheduleRetry()
            } else if let error = error {
                print("[AlertSyncService] SSE connection cancelled")
            }
        }
    }
}
