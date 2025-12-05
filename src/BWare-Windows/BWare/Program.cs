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
    private static ClientConfiguration? _configuration;

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Load or create configuration
        _configuration = ClientConfiguration.Load();

        if (_configuration == null)
        {
            // First run - show setup wizard
            var setupWindow = new SetupWindow();
            var result = setupWindow.ShowDialog();

            if (result != true || !setupWindow.SetupCompleted || setupWindow.Configuration == null)
            {
                // User cancelled setup
                return;
            }

            _configuration = setupWindow.Configuration;
        }

        // Parse and validate Firebase URL
        var parsedUrl = FirebaseConfig.Parse(_configuration.DatabaseUrl);
        if (parsedUrl == null)
        {
            MessageBox.Show(
                "Invalid Firebase URL in settings.\n\nPlease check: " + ClientConfiguration.SettingsPath,
                "B-Ware Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Initialize services
        _alertSyncService = new AlertSyncService(parsedUrl, _configuration.ClientId);
        _timerService = new TimerService();
        _trayIcon = new TrayIcon();

        // Wire up events
        WireEvents();

        // Start Firebase connection
        _ = _alertSyncService.StartAsync();

        // Run the application message loop
        Application.Run();
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
            if (_alertSyncService != null)
            {
                await _alertSyncService.TriggerAlertAsync();
            }
        };

        // Settings requested
        _trayIcon.SettingsRequested += (s, e) =>
        {
            if (_configuration == null) return;

            var settingsWindow = new SettingsWindow(_configuration);
            var result = settingsWindow.ShowDialog();

            if (result == true && settingsWindow.SettingsSaved && settingsWindow.UpdatedConfiguration != null)
            {
                _configuration = settingsWindow.UpdatedConfiguration;

                // Reconnect if URL changed
                if (settingsWindow.UrlChanged)
                {
                    var newParsedUrl = FirebaseConfig.Parse(_configuration.DatabaseUrl);
                    if (newParsedUrl != null)
                    {
                        _alertSyncService?.Reconnect(newParsedUrl);
                    }
                }
            }
        };

        // Exit requested
        _trayIcon.ExitRequested += (s, e) =>
        {
            Shutdown();
        };
    }

    private static void Shutdown()
    {
        _alertSyncService?.Stop();
        _timerService?.Dispose();
        _trayIcon?.Dispose();
        Application.Exit();
    }
}
