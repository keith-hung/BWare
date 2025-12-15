using BWare.Models;
using BWare.Services;
using BWare.UI;

namespace BWare;

/// <summary>
/// Application entry point for B-Ware Windows client.
/// </summary>
internal static class Program
{
    private static TrayIcon? _trayIcon;
    private static AlertSyncService? _alertSyncService;
    private static TimerService? _timerService;
    private static HotkeyService? _hotkeyService;
    private static ClientConfiguration? _configuration;
    private static ApplicationContext? _appContext;

    [STAThread]
    static void Main()
    {
        // Initialize logger
        Logger.Initialize();
        Logger.Info($"Settings path: {ClientConfiguration.SettingsPath}");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Load or create configuration
        Logger.Info("Loading configuration...");
        _configuration = ClientConfiguration.Load();

        if (_configuration == null)
        {
            Logger.Info("No configuration found - showing setup wizard");
            // First run - show setup wizard
            var setupWindow = new SetupWindow();
            var result = setupWindow.ShowDialog();

            if (result != true || !setupWindow.SetupCompleted || setupWindow.Configuration == null)
            {
                Logger.Info("Setup cancelled by user");
                // User cancelled setup
                Logger.Close();
                return;
            }

            _configuration = setupWindow.Configuration;
        }

        Logger.Info($"Configuration loaded:");
        Logger.Info($"  Database URL: {_configuration.DatabaseUrl}");
        Logger.Info($"  Client ID: {_configuration.ClientId}");

        // Parse and validate Firebase URL
        var parsedUrl = FirebaseConfig.Parse(_configuration.DatabaseUrl);
        if (parsedUrl == null)
        {
            Logger.Error("Invalid Firebase URL!");
            MessageBox.Show(
                "Invalid Firebase URL in settings.\n\nPlease check: " + ClientConfiguration.SettingsPath,
                "B-Ware Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Logger.Close();
            return;
        }

        Logger.Info($"Parsed Firebase URL:");
        Logger.Info($"  SSE: {parsedUrl.SseUrl}");
        Logger.Info($"  REST: {parsedUrl.RestUrl}");

        // Initialize services
        Logger.Info("Initializing services...");
        _alertSyncService = new AlertSyncService(parsedUrl, _configuration.ClientId);
        _timerService = new TimerService();
        _trayIcon = new TrayIcon();
        _hotkeyService = new HotkeyService();
        _hotkeyService.Initialize();

        // Wire up events
        WireEvents();

        // Register hotkeys
        RegisterHotkeys(_configuration.Hotkeys);

        // Start Firebase connection
        Logger.Info("Starting Firebase connection...");
        _ = _alertSyncService.StartAsync();

        // Run the application message loop with context
        // This keeps the application running even without visible windows
        _appContext = new ApplicationContext();
        Application.Run(_appContext);
    }

    private static void RegisterHotkeys(HotkeyConfiguration hotkeys)
    {
        if (_hotkeyService == null) return;

        // Register trigger alert hotkey
        if (hotkeys.TriggerAlert.Enabled && hotkeys.TriggerAlert.HasValidKey)
        {
            var success = _hotkeyService.RegisterHotkey(
                HotkeyService.HOTKEY_TRIGGER_ALERT,
                hotkeys.TriggerAlert.Modifiers,
                hotkeys.TriggerAlert.Key);

            if (!success)
            {
                Logger.Warning($"Failed to register trigger alert hotkey: {hotkeys.TriggerAlert.DisplayString}");
            }
        }
    }

    private static void WireEvents()
    {
        if (_alertSyncService == null || _trayIcon == null || _timerService == null)
            return;

        // Alert state changes
        _alertSyncService.AlertStateChanged += (s, state) =>
        {
            _trayIcon.UpdateAlertState(state);

            // Start or stop timer based on alert state
            if (state.Status == AlertStatus.Alert && state.RemainingSeconds > 0)
            {
                _timerService.Start(state.ExpiresAt);
            }
            else
            {
                _timerService.Stop();
            }
        };

        // Connection state changes
        _alertSyncService.ConnectionStateChanged += (s, state) =>
        {
            _trayIcon.UpdateConnectionState(state);
        };

        // Timer tick for countdown
        _timerService.Tick += (s, remaining) =>
        {
            _trayIcon.UpdateCountdown(remaining);
        };

        // Timer expired
        _timerService.Expired += async (s, e) =>
        {
            // Reset to normal when timer expires
            if (_alertSyncService != null)
            {
                await _alertSyncService.ResetToNormalAsync();
            }
        };

        // Tray icon left click - trigger alert
        _trayIcon.LeftClicked += async (s, e) =>
        {
            Logger.Info("Event: Left click received, triggering alert");
            if (_alertSyncService != null)
            {
                await _alertSyncService.TriggerAlertAsync();
            }
        };

        // Settings requested
        _trayIcon.SettingsRequested += (s, e) =>
        {
            Logger.Info("Event: Settings requested");
            if (_configuration == null) return;

            var settingsWindow = new SettingsWindow(_configuration);
            var result = settingsWindow.ShowDialog();

            if (result == true && settingsWindow.SettingsSaved && settingsWindow.UpdatedConfiguration != null)
            {
                _configuration = settingsWindow.UpdatedConfiguration;

                // Reconnect if URL changed
                if (settingsWindow.UrlChanged)
                {
                    Logger.Info("Settings: URL changed, reconnecting...");
                    var newParsedUrl = FirebaseConfig.Parse(_configuration.DatabaseUrl);
                    if (newParsedUrl != null)
                    {
                        _alertSyncService?.Reconnect(newParsedUrl);
                    }
                }

                // Re-register hotkeys if changed
                if (settingsWindow.HotkeysChanged)
                {
                    Logger.Info("Settings: Hotkeys changed, re-registering...");
                    _hotkeyService?.UnregisterAll();
                    RegisterHotkeys(_configuration.Hotkeys);
                }
            }
        };

        // Exit requested
        _trayIcon.ExitRequested += (s, e) =>
        {
            Logger.Info("Event: Exit requested");
            Shutdown();
        };

        // Hotkey pressed
        if (_hotkeyService != null)
        {
            _hotkeyService.HotkeyPressed += async (s, e) =>
            {
                if (e.HotkeyId == HotkeyService.HOTKEY_TRIGGER_ALERT)
                {
                    Logger.Info("Event: Trigger alert hotkey pressed");
                    if (_alertSyncService != null)
                    {
                        await _alertSyncService.TriggerAlertAsync();
                    }
                }
            };
        }
    }

    private static void Shutdown()
    {
        Logger.Info("Application shutting down...");
        _hotkeyService?.Dispose();
        _alertSyncService?.Stop();
        _timerService?.Dispose();
        _trayIcon?.Dispose();
        Logger.Close();
        _appContext?.ExitThread();
        Application.Exit();
    }
}
