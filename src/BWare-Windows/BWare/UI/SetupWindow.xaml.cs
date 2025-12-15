using System.Windows;
using BWare.Models;
using BWare.Services;

namespace BWare.UI;

/// <summary>
/// First-time setup wizard for new users.
/// </summary>
public partial class SetupWindow : Window
{
    private readonly string _clientId;

    /// <summary>
    /// Gets the created configuration after successful setup.
    /// </summary>
    public ClientConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Gets whether the setup was completed successfully.
    /// </summary>
    public bool SetupCompleted { get; private set; }

    public SetupWindow()
    {
        InitializeComponent();

        // Generate a new client ID
        _clientId = ClientConfiguration.GenerateClientId();
        ClientIdTextBox.Text = _clientId;
    }

    private void UrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ValidateUrl();
    }

    private void ValidateUrl()
    {
        var url = UrlTextBox.Text?.Trim();
        var error = FirebaseConfig.GetValidationError(url);

        if (error != null)
        {
            ErrorLabel.Text = error;
            ErrorLabel.Visibility = Visibility.Visible;
            GetStartedButton.IsEnabled = false;
        }
        else
        {
            ErrorLabel.Visibility = Visibility.Collapsed;
            GetStartedButton.IsEnabled = true;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_clientId);

        // Visual feedback
        var originalContent = CopyButton.Content;
        CopyButton.Content = "Copied!";

        // Reset after 2 seconds
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

    private void GetStartedButton_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(url) || !FirebaseConfig.IsValidUrl(url))
        {
            ValidateUrl();
            return;
        }

        // Create and save configuration
        Configuration = ClientConfiguration.Create(url);
        Configuration.Save();

        SetupCompleted = true;
        DialogResult = true;
        Close();
    }
}
