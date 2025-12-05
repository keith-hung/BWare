import Foundation

/// Connection status to Firebase.
enum ConnectionStatus: Equatable {
    case connecting
    case connected
    case disconnected(reason: DisconnectReason)

    var isConnected: Bool {
        if case .connected = self { return true }
        return false
    }

    var displayText: String {
        switch self {
        case .connecting:
            return "Connecting..."
        case .connected:
            return "Connected"
        case .disconnected(let reason):
            return "Disconnected: \(reason.displayText)"
        }
    }
}

/// Reason for disconnection.
enum DisconnectReason: Equatable {
    case networkUnavailable
    case authenticationFailed
    case serverError
    case unknown

    var displayText: String {
        switch self {
        case .networkUnavailable:
            return "No network"
        case .authenticationFailed:
            return "Auth failed"
        case .serverError:
            return "Server error"
        case .unknown:
            return "Unknown"
        }
    }
}

/// Local client configuration stored in UserDefaults.
struct ClientConfiguration: Codable {
    /// Firebase Realtime Database URL
    var databaseUrl: String

    /// Whether to launch app at macOS login
    var launchAtLogin: Bool

    /// Unique client identifier (UUID)
    let clientId: String

    /// Last successful connection timestamp
    var lastConnected: Date?

    /// UserDefaults key for storage
    private static let storageKey = "BWareClientConfiguration"

    /// Creates a new client configuration
    static func newClient(databaseUrl: String) -> ClientConfiguration {
        ClientConfiguration(
            databaseUrl: databaseUrl,
            launchAtLogin: false,
            clientId: UUID().uuidString,
            lastConnected: nil
        )
    }

    /// Loads configuration from UserDefaults
    static func load() -> ClientConfiguration? {
        guard let data = UserDefaults.standard.data(forKey: storageKey) else {
            return nil
        }
        return try? JSONDecoder().decode(ClientConfiguration.self, from: data)
    }

    /// Saves configuration to UserDefaults
    func save() {
        if let data = try? JSONEncoder().encode(self) {
            UserDefaults.standard.set(data, forKey: Self.storageKey)
        }
    }

    /// Removes configuration from UserDefaults
    static func remove() {
        UserDefaults.standard.removeObject(forKey: storageKey)
    }
}

/// Locally queued alert trigger for offline support.
struct LocalAlertState {
    /// Whether there's a pending trigger to sync
    var pendingTrigger: Bool = false

    /// Local expiration time (for UI countdown when offline)
    var localExpiresAt: Date?

    /// When the trigger was queued
    var queuedAt: Date?

    /// Resets the local state
    mutating func reset() {
        pendingTrigger = false
        localExpiresAt = nil
        queuedAt = nil
    }

    /// Queues a new trigger
    mutating func queueTrigger() {
        pendingTrigger = true
        localExpiresAt = Date().addingTimeInterval(60)
        queuedAt = Date()
    }
}
