# B-Ware

A lightweight menu bar app for macOS that syncs status across multiple devices using Firebase Realtime Database. Perfect for teams who need a simple "heads up" notification system.

## Features

- **Menu bar status indicator** - Green (normal), Red (alert), Gray (disconnected)
- **One-click alerts** - Left-click to trigger a 60-second alert visible to all connected devices
- **Real-time sync** - Instant status updates via Firebase Server-Sent Events
- **Offline support** - Queues alerts locally when disconnected
- **Launch at Login** - Optional auto-start with macOS
- **Minimal footprint** - Runs quietly in your menu bar

## System Requirements

- **macOS 12 Monterey** or later
- A Firebase Realtime Database (free tier works fine)

## Installation

1. Download the latest `BWare.dmg` from [Releases](../../releases)
2. Open the DMG and drag `BWare.app` to your Applications folder
3. First launch: Right-click the app > Open > Open (required for unsigned apps)
4. Configure your Firebase Database URL when prompted

## Firebase Setup

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create a new project (or use existing)
3. Navigate to **Realtime Database** > **Create Database**
4. Choose your region and start in **test mode** (or configure rules)
5. Copy your database URL (looks like `https://your-project.firebaseio.com`)
6. In B-Ware settings, enter: `https://your-project.firebaseio.com/light.json`

### Database Structure

B-Ware automatically manages this structure:

```json
{
  "status": "normal",
  "expiresAt": 0,
  "triggeredBy": "client-uuid",
  "triggeredAt": 1234567890
}
```

## Usage

| Action | Result |
|--------|--------|
| **Left-click** | Trigger alert (turns red for 60 seconds) |
| **Right-click** | Open settings menu |
| **Hover** | Show current status and countdown |

## Icon States

| Color | Meaning |
|-------|---------|
| Green | Normal - connected and ready |
| Red | Alert - someone triggered the alert |
| Gray | Disconnected or connecting |

## Building from Source

Requires Xcode 15+ and Swift 5.9+

```bash
cd src/BWare-macOS
./scripts/generate-version.sh  # Generate version info
swift build                     # Debug build
./scripts/package.sh            # Create distributable .app/.dmg
```

## License

MIT License - See [LICENSE](LICENSE) for details.
