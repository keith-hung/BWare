import Foundation

/// Alert status enumeration matching Firebase schema.
enum AlertStatus: String, Codable, CaseIterable {
    case normal = "normal"
    case alert = "alert"
}

/// Shared alert state synchronized via Firebase Realtime Database.
/// Schema: {"status":"normal","expiresAt":0} or {"status":"alert","expiresAt":1234567890,"triggeredBy":"uuid","triggeredAt":1234567890}
struct AlertState: Codable, Equatable {
    /// Current status: normal (green) or alert (red)
    let status: AlertStatus

    /// Unix timestamp (seconds) when alert expires. 0 when status is normal.
    let expiresAt: Int64

    /// UUID of the client that triggered the alert (optional)
    let triggeredBy: String?

    /// Unix timestamp (seconds) when alert was triggered (optional)
    let triggeredAt: Int64?

    /// Default normal state
    static let normal = AlertState(
        status: .normal,
        expiresAt: 0,
        triggeredBy: nil,
        triggeredAt: nil
    )

    /// Creates a new alert state
    static func alert(expiresAt: Int64, triggeredBy: String) -> AlertState {
        AlertState(
            status: .alert,
            expiresAt: expiresAt,
            triggeredBy: triggeredBy,
            triggeredAt: Int64(Date().timeIntervalSince1970)
        )
    }

    /// Calculates remaining seconds until expiration
    var remainingSeconds: Int {
        guard status == .alert else { return 0 }
        let now = Int64(Date().timeIntervalSince1970)
        return max(0, Int(expiresAt - now))
    }

    /// Whether the alert has expired
    var isExpired: Bool {
        guard status == .alert else { return false }
        return remainingSeconds <= 0
    }
}

// MARK: - Firebase Dictionary Conversion

extension AlertState {
    /// Initialize from Firebase dictionary
    init?(dictionary: [String: Any]) {
        guard let statusString = dictionary["status"] as? String,
              let status = AlertStatus(rawValue: statusString) else {
            return nil
        }

        // Handle "expiresAt" field (can be Int or Int64)
        let expiresAt: Int64
        if let value = dictionary["expiresAt"] as? Int64 {
            expiresAt = value
        } else if let value = dictionary["expiresAt"] as? Int {
            expiresAt = Int64(value)
        } else {
            expiresAt = 0
        }

        self.status = status
        self.expiresAt = expiresAt
        self.triggeredBy = dictionary["triggeredBy"] as? String

        if let value = dictionary["triggeredAt"] as? Int64 {
            self.triggeredAt = value
        } else if let value = dictionary["triggeredAt"] as? Int {
            self.triggeredAt = Int64(value)
        } else {
            self.triggeredAt = nil
        }
    }

    /// Convert to Firebase dictionary
    var dictionary: [String: Any] {
        var dict: [String: Any] = [
            "status": status.rawValue,
            "expiresAt": expiresAt
        ]
        if let triggeredBy = triggeredBy {
            dict["triggeredBy"] = triggeredBy
        }
        if let triggeredAt = triggeredAt {
            dict["triggeredAt"] = triggeredAt
        }
        return dict
    }
}
