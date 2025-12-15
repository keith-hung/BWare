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

    public SettingsWindow(ClientConfiguration configuration)
    {
        InitializeComponent();

        _originalConfiguration = configuration;
        _startupService = new StartupService();

        // Populate fields
        UrlTextBox.Text = configuration.DatabaseUrl;
        ClientIdTextBox.Text = configuration.ClientId;
        LaunchAtLoginCheckBox.IsChecked = _startupService.IsStartupEnabled();

        // Display version
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = $"B-Ware v{version?.Major}.{version?.Minor}.{version?.Build}";
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
            LaunchAtLogin = launchAtLogin
        };

        // Save to file
        UpdatedConfiguration.Save();

        SettingsSaved = true;
        DialogResult = true;
        Close();
    }
}
