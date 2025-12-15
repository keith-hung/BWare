using System.Reflection;
using System.Windows;
using BWare.Models;
using BWare.Services;

namespace BWare.UI;

/// <summary>
/// Settings window for modifying application configuration.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ClientConfiguration _originalConfiguration;
    private readonly StartupService _startupService;
    private readonly HotkeyService _hotkeyService;

    /// <summary>
    /// Gets the updated configuration if settings were saved.
    /// </summary>
    public ClientConfiguration? UpdatedConfiguration { get; private set; }

    /// <summary>
    /// Gets whether settings were saved (requiring reconnection).
    /// </summary>
    public bool SettingsSaved { get; private set; }

    /// <summary>
    /// Gets whether the Firebase URL was changed (requiring reconnection).
    /// </summary>
    public bool UrlChanged { get; private set; }

    /// <summary>
    /// Gets whether hotkey settings were changed.
    /// </summary>
    public bool HotkeysChanged { get; private set; }

    public SettingsWindow(ClientConfiguration configuration)
    {
        InitializeComponent();

        _originalConfiguration = configuration;
        _startupService = new StartupService();
        _hotkeyService = new HotkeyService();
        _hotkeyService.Initialize();

        // Populate fields
        UrlTextBox.Text = configuration.DatabaseUrl;
        ClientIdTextBox.Text = configuration.ClientId;
        LaunchAtLoginCheckBox.IsChecked = _startupService.IsStartupEnabled();

        // Initialize hotkey controls
        InitializeHotkeyControls(configuration.Hotkeys);

        // Display version
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = $"B-Ware v{version?.Major}.{version?.Minor}.{version?.Build}";
    }

    private void InitializeHotkeyControls(HotkeyConfiguration hotkeys)
    {
        // Set up hotkey service for availability checking
        TriggerAlertHotkeyBox.SetHotkeyService(_hotkeyService);

        // Load trigger alert hotkey
        TriggerAlertEnabledCheckBox.IsChecked = hotkeys.TriggerAlert.Enabled;
        if (hotkeys.TriggerAlert.HasValidKey)
        {
            TriggerAlertHotkeyBox.SetHotkey(hotkeys.TriggerAlert);
        }
        TriggerAlertHotkeyBox.IsEnabled = hotkeys.TriggerAlert.Enabled;
    }

    private void HotkeyEnabled_Changed(object sender, RoutedEventArgs e)
    {
        // Enable/disable hotkey text box based on checkbox state
        TriggerAlertHotkeyBox.IsEnabled = TriggerAlertEnabledCheckBox.IsChecked ?? false;
    }

    private void ResetTriggerHotkey_Click(object sender, RoutedEventArgs e)
    {
        TriggerAlertHotkeyBox.SetHotkey(Models.HotkeyBinding.DefaultTriggerAlert());
    }

    private void UrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ValidateUrl();
    }

    private bool ValidateUrl()
    {
        var url = UrlTextBox.Text?.Trim();
        var error = FirebaseConfig.GetValidationError(url);

        if (error != null)
        {
            ErrorLabel.Text = error;
            ErrorLabel.Visibility = Visibility.Visible;
            return false;
        }
        else
        {
            ErrorLabel.Visibility = Visibility.Collapsed;
            return true;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_originalConfiguration.ClientId);

        // Visual feedback
        var originalContent = CopyButton.Content;
        CopyButton.Content = "Copied!";

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (s, args) =>
        {
            CopyButton.Content = originalContent;
            timer.Stop();
        };
        timer.Start();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateUrl())
        {
            return;
        }

        var newUrl = UrlTextBox.Text?.Trim() ?? "";
        var launchAtLogin = LaunchAtLoginCheckBox.IsChecked ?? false;

        // Check if URL changed
        UrlChanged = !string.Equals(newUrl, _originalConfiguration.DatabaseUrl, StringComparison.Ordinal);

        // Build hotkey configuration
        var newHotkeys = new HotkeyConfiguration
        {
            TriggerAlert = TriggerAlertHotkeyBox.GetHotkey(TriggerAlertEnabledCheckBox.IsChecked ?? false)
        };

        // Check if hotkeys changed
        HotkeysChanged = !HotkeysEqual(_originalConfiguration.Hotkeys, newHotkeys);

        // Update startup registration
        if (launchAtLogin != _startupService.IsStartupEnabled())
        {
            if (launchAtLogin)
            {
                _startupService.EnableStartup();
            }
            else
            {
                _startupService.DisableStartup();
            }
        }

        // Create updated configuration
        UpdatedConfiguration = _originalConfiguration with
        {
            DatabaseUrl = newUrl,
            LaunchAtLogin = launchAtLogin,
            Hotkeys = newHotkeys
        };

        // Save to file
        UpdatedConfiguration.Save();

        SettingsSaved = true;
        DialogResult = true;

        // Dispose the temporary hotkey service
        _hotkeyService.Dispose();
        Close();
    }

    private static bool HotkeysEqual(HotkeyConfiguration a, HotkeyConfiguration b)
    {
        return a.TriggerAlert.Enabled == b.TriggerAlert.Enabled &&
               a.TriggerAlert.Modifiers == b.TriggerAlert.Modifiers &&
               a.TriggerAlert.Key == b.TriggerAlert.Key;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _hotkeyService.Dispose();
    }
}
