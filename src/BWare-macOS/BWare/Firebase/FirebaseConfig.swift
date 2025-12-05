import Foundation

/// Parsed Firebase URL components
struct ParsedFirebaseUrl {
    let rootUrl: String
    let projectId: String
    let region: String?
    let path: String
}

/// Firebase configuration and initialization.
struct FirebaseConfig {
    /// Parses a Firebase Realtime Database URL and extracts all components
    /// Input: https://PROJECT_ID.REGION.firebasedatabase.app/path.json
    /// Returns: rootUrl, projectId, region, path (without .json)
    static func parseFullUrl(_ url: String) -> ParsedFirebaseUrl? {
        var normalized = url.trimmingCharacters(in: .whitespacesAndNewlines)

        // Add https:// if missing
        if !normalized.hasPrefix("http://") && !normalized.hasPrefix("https://") {
            normalized = "https://\(normalized)"
        }

        // Upgrade http to https
        if normalized.hasPrefix("http://") {
            normalized = normalized.replacingOccurrences(of: "http://", with: "https://")
        }

        guard let urlComponents = URLComponents(string: normalized),
              let host = urlComponents.host else {
            return nil
        }

        // Extract path (remove leading / and trailing .json)
        var path = urlComponents.path
        if path.hasPrefix("/") {
            path = String(path.dropFirst())
        }
        if path.hasSuffix(".json") {
            path = String(path.dropLast(5))
        }
        // Default to "alerts" if no path specified
        if path.isEmpty {
            path = "alerts"
        }

        let rootUrl = "https://\(host)"

        // Format 1: PROJECT_ID.firebaseio.com (default US region)
        if host.hasSuffix(".firebaseio.com") {
            let projectId = host.replacingOccurrences(of: ".firebaseio.com", with: "")
            return ParsedFirebaseUrl(rootUrl: rootUrl, projectId: projectId, region: nil, path: path)
        }

        // Format 2: PROJECT_ID.REGION.firebasedatabase.app
        if host.hasSuffix(".firebasedatabase.app") {
            let parts = host.replacingOccurrences(of: ".firebasedatabase.app", with: "").split(separator: ".")
            if parts.count >= 2 {
                return ParsedFirebaseUrl(rootUrl: rootUrl, projectId: String(parts[0]), region: String(parts[1]), path: path)
            } else if parts.count == 1 {
                return ParsedFirebaseUrl(rootUrl: rootUrl, projectId: String(parts[0]), region: nil, path: path)
            }
        }

        return nil
    }

    /// Legacy: Parses a Firebase Realtime Database URL (returns projectId and region only)
    static func parseUrl(_ url: String) -> (projectId: String, region: String?)? {
        guard let parsed = parseFullUrl(url) else { return nil }
        return (parsed.projectId, parsed.region)
    }

    /// Validates a Firebase database URL
    static func isValidUrl(_ url: String) -> Bool {
        parseFullUrl(url) != nil
    }

    /// Normalizes the database URL (returns root URL only, for SDK initialization)
    static func normalizeUrl(_ url: String) -> String {
        parseFullUrl(url)?.rootUrl ?? url
    }

    /// Extracts the path from the URL (e.g., "light" from "/light.json")
    static func extractPath(_ url: String) -> String {
        parseFullUrl(url)?.path ?? "alerts"
    }
}
