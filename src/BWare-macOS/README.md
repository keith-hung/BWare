# B-Ware for macOS

A macOS menu bar application that displays a shared status indicator, synchronized with the Windows version via Firebase Realtime Database.

## Features

- **Menu Bar Icon**: Green (normal), Red (alert), Gray (disconnected)
- **60-Second Alert Timer**: Click to trigger, auto-resets after 60 seconds
- **Cross-Platform Sync**: Real-time synchronization with Windows version
- **Offline Support**: Queues triggers when disconnected, syncs on reconnect
- **Launch at Login**: Optional auto-start on macOS login
- **Dark Mode**: Full support for macOS dark mode

## Requirements

- macOS 12.0 (Monterey) or later
- Xcode 15.0 or later
- Swift 5.9 or later
- Firebase Realtime Database project

## Building

### Using Swift Package Manager

```bash
cd src/BWare-macOS
swift build
```

### Using Xcode

1. Open `Package.swift` in Xcode
2. Wait for Swift Package Manager to resolve dependencies
3. Select the `BWare` scheme
4. Build and run (⌘R)

## Firebase Setup

1. Create a Firebase project at [console.firebase.google.com](https://console.firebase.google.com)
2. Enable Realtime Database
3. Set database rules:

```json
{
  "rules": {
    "alerts": {
      ".read": true,
      ".write": true
    }
  }
}
```

4. Copy your database URL (e.g., `https://your-project.firebaseio.com`)
5. Enter the URL when B-Ware prompts on first launch

## Usage

- **Left-click**: Trigger alert (turns red for 60 seconds)
- **Right-click**: Open context menu (Settings, Quit)
- **Hover**: View current status and remaining time

## Project Structure

```
BWare-macOS/
├── Package.swift           # Swift Package Manager config
├── Resources/
│   ├── Info.plist         # App configuration
│   └── BWare.entitlements # Keychain sharing
└── BWare/
    ├── BWareApp.swift     # App entry point
    ├── AppDelegate.swift  # Lifecycle management
    ├── Models/
    │   ├── AlertState.swift    # Firebase alert model
    │   └── AppSettings.swift   # Configuration models
    ├── Firebase/
    │   ├── FirebaseConfig.swift   # URL parsing
    │   ├── FirebaseManager.swift  # SDK initialization
    │   └── AlertSyncService.swift # Real-time sync
    ├── MenuBar/
    │   ├── StatusItemManager.swift # NSStatusItem
    │   ├── StatusMenu.swift        # Context menu
    │   └── MenuBarIcons.swift      # SF Symbol icons
    ├── Services/
    │   ├── TimerService.swift      # 60s countdown
    │   ├── ConnectionMonitor.swift # Network status
    │   └── LoginItemService.swift  # Launch at login
    └── UI/
        ├── SetupView.swift      # First-run setup
        └── SettingsWindow.swift # Preferences
```

## Cross-Platform Compatibility

This macOS version is fully compatible with the Windows version of B-Ware. Both platforms:

- Share the same Firebase Realtime Database
- Use identical alert state schema
- Sync in real-time (< 2 second latency)
- Use the same 60-second timer duration

## License

See the main project LICENSE file.
