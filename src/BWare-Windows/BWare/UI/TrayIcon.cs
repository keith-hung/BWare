using System.Drawing;
using BWare.Models;
using BWare.Resources;

namespace BWare.UI;

/// <summary>
/// Manages the system tray (notification area) icon and its interactions.
/// </summary>
public class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _greenIcon;
    private readonly Icon _redIcon;
    private readonly Icon _grayIcon;

    private AlertState _currentAlertState = AlertState.Normal;
    private ConnectionState _currentConnectionState = ConnectionState.Connecting;
    private bool _disposed;

    /// <summary>
    /// Raised when the user left-clicks the tray icon.
    /// </summary>
    public event EventHandler? LeftClicked;

    /// <summary>
    /// Raised when the user requests to open settings.
    /// </summary>
    public event EventHandler? SettingsRequested;

    /// <summary>
    /// Raised when the user requests to exit the application.
    /// </summary>
    public event EventHandler? ExitRequested;

    public TrayIcon()
    {
        Services.Logger.Debug("TrayIcon: Initializing...");

        // Generate icons dynamically
        _greenIcon = IconGenerator.CreateGreenIcon();
        _redIcon = IconGenerator.CreateRedIcon();
        _grayIcon = IconGenerator.CreateGrayIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _grayIcon,
            Text = "B-Ware: Connecting...",
            Visible = true
            // Don't set ContextMenuStrip here - we'll handle it manually
        };

        // Handle mouse events manually for better control
        _notifyIcon.MouseDown += OnMouseDown;

        Services.Logger.Info("TrayIcon: Initialized and visible");
    }

    /// <summary>
    /// Updates the icon and tooltip based on the current alert and connection states.
    /// </summary>
    /// <param name="alertState">The current alert state.</param>
    /// <param name="connectionState">The current connection state.</param>
    public void UpdateState(AlertState alertState, ConnectionState connectionState)
    {
        _currentAlertState = alertState;
        _currentConnectionState = connectionState;
        UpdateIconAndTooltip();
    }

    /// <summary>
    /// Updates the alert state and refreshes the display.
    /// </summary>
    /// <param name="alertState">The new alert state.</param>
    public void UpdateAlertState(AlertState alertState)
    {
        _currentAlertState = alertState;
        UpdateIconAndTooltip();
    }

    /// <summary>
    /// Updates the connection state and refreshes the display.
    /// </summary>
    /// <param name="connectionState">The new connection state.</param>
    public void UpdateConnectionState(ConnectionState connectionState)
    {
        _currentConnectionState = connectionState;
        UpdateIconAndTooltip();
    }

    /// <summary>
    /// Updates the tooltip to show remaining seconds during an active alert.
    /// </summary>
    /// <param name="remainingSeconds">Seconds until alert expires.</param>
    public void UpdateCountdown(int remainingSeconds)
    {
        if (_currentAlertState.Status == AlertStatus.Alert && _currentConnectionState.IsConnected)
        {
            _notifyIcon.Text = $"B-Ware: Alert ({remainingSeconds}s remaining)";
        }
    }

    private void UpdateIconAndTooltip()
    {
        // Determine icon based on connection and alert state
        if (!_currentConnectionState.IsConnected)
        {
            _notifyIcon.Icon = _grayIcon;
            _notifyIcon.Text = _currentConnectionState.Status == ConnectionStatus.Connecting
                ? "B-Ware: Connecting..."
                : "B-Ware: Disconnected";
        }
        else if (_currentAlertState.Status == AlertStatus.Alert)
        {
            _notifyIcon.Icon = _redIcon;
            var remaining = _currentAlertState.RemainingSeconds;
            _notifyIcon.Text = remaining > 0
                ? $"B-Ware: Alert ({remaining}s remaining)"
                : "B-Ware: Alert";
        }
        else
        {
            _notifyIcon.Icon = _greenIcon;
            _notifyIcon.Text = "B-Ware: Normal";
        }
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (s, e) =>
        {
            Services.Logger.Info("TrayIcon: Settings menu clicked");
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) =>
        {
            Services.Logger.Info("TrayIcon: Exit menu clicked");
            ExitRequested?.Invoke(this, EventArgs.Empty);
        };

        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        Services.Logger.Debug("TrayIcon: Context menu created with 2 items");
        return menu;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        Services.Logger.Debug($"TrayIcon: Mouse down - Button: {e.Button}");

        if (e.Button == MouseButtons.Left)
        {
            Services.Logger.Info("TrayIcon: Left click detected, triggering alert");
            LeftClicked?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Button == MouseButtons.Right)
        {
            Services.Logger.Debug("TrayIcon: Right click detected, showing context menu");
            var menu = CreateContextMenu();
            menu.Show(Cursor.Position);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _greenIcon.Dispose();
        _redIcon.Dispose();
        _grayIcon.Dispose();
    }
}
