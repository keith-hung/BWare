import SwiftUI
import AppKit

/// Configuration view for Firebase settings and app preferences.
/// Used for both first-run setup and settings access.
struct ConfigurationView: View {
    @State private var databaseUrl: String = ""
    @State private var launchAtLogin: Bool = false
    @State private var showingError: Bool = false
    @State private var errorMessage: String = ""
    @State private var clientId: String = ""
    @FocusState private var isUrlFieldFocused: Bool

    /// Whether this is the first-run setup (shows welcome message)
    let isFirstRun: Bool

    /// Called when configuration is saved successfully
    var onSave: (() -> Void)?

    private let loginItemService = LoginItemService()

    var body: some View {
        VStack(spacing: 20) {
            // Header
            if isFirstRun {
                Image(systemName: "circle.fill")
                    .font(.system(size: 48))
                    .foregroundColor(.green)

                Text("Welcome to B-Ware")
                    .font(.title)
                    .fontWeight(.semibold)

                Text("Configure your Firebase connection to get started.")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)
            } else {
                Text("B-Ware Settings")
                    .font(.title2)
                    .fontWeight(.semibold)
            }

            // Firebase URL input
            VStack(alignment: .leading, spacing: 8) {
                Text("Firebase Database URL")
                    .font(.headline)

                TextField("https://your-project.firebasedatabase.app/path.json", text: $databaseUrl)
                    .textFieldStyle(.roundedBorder)
                    .frame(maxWidth: 380)
                    .focused($isUrlFieldFocused)

                Text("Include path (e.g., /light.json)")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }

            // Client ID (read-only)
            VStack(alignment: .leading, spacing: 4) {
                Text("Client ID")
                    .font(.headline)

                HStack {
                    Text(clientId)
                        .font(.system(.body, design: .monospaced))
                        .foregroundColor(.secondary)
                        .textSelection(.enabled)

                    Spacer()

                    Button(action: copyClientId) {
                        Image(systemName: "doc.on.doc")
                    }
                    .buttonStyle(.borderless)
                    .help("Copy to clipboard")
                }
                .frame(maxWidth: 380)
            }

            // Launch at login toggle
            Toggle("Launch at Login", isOn: $launchAtLogin)
                .frame(maxWidth: 380, alignment: .leading)
                .onChange(of: launchAtLogin) { newValue in
                    updateLaunchAtLogin(newValue)
                }

            Spacer().frame(height: 10)

            // Save button
            Button(action: saveConfiguration) {
                Text(isFirstRun ? "Get Started" : "Save")
                    .frame(minWidth: 100)
            }
            .buttonStyle(.borderedProminent)
            .disabled(databaseUrl.trimmingCharacters(in: .whitespaces).isEmpty)
            .keyboardShortcut(.defaultAction)
        }
        .padding(40)
        .frame(width: 480, height: isFirstRun ? 450 : 400)
        .onAppear {
            loadConfiguration()
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                isUrlFieldFocused = true
            }
        }
        .alert("Invalid URL", isPresented: $showingError) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(errorMessage)
        }
    }

    private func loadConfiguration() {
        if let config = ClientConfiguration.load() {
            databaseUrl = config.databaseUrl
            launchAtLogin = config.launchAtLogin
            clientId = config.clientId
        } else {
            // Generate new client ID for display (will be saved on first save)
            clientId = UUID().uuidString
        }
        launchAtLogin = loginItemService.isEnabled()
    }

    private func copyClientId() {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(clientId, forType: .string)
    }

    private func saveConfiguration() {
        let trimmedUrl = databaseUrl.trimmingCharacters(in: .whitespacesAndNewlines)

        guard FirebaseConfig.isValidUrl(trimmedUrl) else {
            errorMessage = "Please enter a valid Firebase Realtime Database URL.\n\nExample: https://your-project.asia-southeast1.firebasedatabase.app/light.json"
            showingError = true
            return
        }

        // Save configuration (preserve the displayed clientId for new configs)
        var config: ClientConfiguration
        if let existingConfig = ClientConfiguration.load() {
            config = existingConfig
        } else {
            // First-time save: use the clientId that was displayed
            config = ClientConfiguration(
                databaseUrl: trimmedUrl,
                launchAtLogin: launchAtLogin,
                clientId: clientId,
                lastConnected: nil
            )
        }
        config.databaseUrl = trimmedUrl
        config.launchAtLogin = launchAtLogin
        config.save()

        // Notify settings changed
        NotificationCenter.default.post(name: .settingsChanged, object: nil)

        // Call completion handler
        onSave?()
    }

    private func updateLaunchAtLogin(_ enabled: Bool) {
        if enabled {
            loginItemService.enableLaunchAtLogin()
        } else {
            loginItemService.disableLaunchAtLogin()
        }
    }
}

// MARK: - Notification Names

extension Notification.Name {
    static let settingsChanged = Notification.Name("BWareSettingsChanged")
}

#Preview("First Run") {
    ConfigurationView(isFirstRun: true)
}

#Preview("Settings") {
    ConfigurationView(isFirstRun: false)
}
