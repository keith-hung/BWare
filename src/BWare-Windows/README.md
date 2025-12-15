# B-Ware Windows - Quick Installation

## Installation (No installer needed!)

1. **Create a folder** (recommended location):
   ```
   %LOCALAPPDATA%\BWare
   ```
   To open this location: Press `Win+R`, type `%LOCALAPPDATA%` and press Enter, then create a `BWare` folder.

2. **Copy `BWare.exe`** to this folder

3. **Run `BWare.exe`** - First launch will show setup wizard

4. **Done!** The app will run in the system tray

## Optional: Auto-start on Windows login

If you want B-Ware to start automatically when you log in:

### Method 1: Manual (Quick)
1. Press `Win+R` and type: `shell:startup`
2. Right-click in the Startup folder → New → Shortcut
3. Browse to where you put `BWare.exe` (e.g., `%LOCALAPPDATA%\BWare\BWare.exe`)
4. Click Finish

### Method 2: Using Command (Advanced)
Open PowerShell and run:
```powershell
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\B-Ware.lnk")
$Shortcut.TargetPath = "$env:LOCALAPPDATA\BWare\BWare.exe"
$Shortcut.Save()
```

## Moving the app?

If you move `BWare.exe` to a different location:
1. The app will continue to work (settings and logs are stored separately)
2. But you'll need to recreate the startup shortcut if you set one up

## Uninstallation

1. Right-click tray icon → Exit
2. Delete `BWare.exe`
3. (Optional) Delete settings and logs: Press `Win+R`, type `%APPDATA%\BWare` and delete the folder
4. (Optional) Remove startup shortcut from `shell:startup` folder
