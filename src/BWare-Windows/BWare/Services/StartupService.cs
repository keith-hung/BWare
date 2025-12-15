using Microsoft.Win32;

namespace BWare.Services;

/// <summary>
/// Service for managing Windows startup registration.
/// Uses the HKCU Run registry key (no admin privileges required).
/// </summary>
public class StartupService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BWare";

    /// <summary>
    /// Enables the application to start with Windows.
    /// </summary>
    public void EnableStartup()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            key?.SetValue(AppName, $"\"{exePath}\"");
        }
        catch
        {
            // Silently fail - not critical functionality
        }
    }

    /// <summary>
    /// Disables the application from starting with Windows.
    /// </summary>
    public void DisableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch
        {
            // Silently fail - not critical functionality
        }
    }

    /// <summary>
    /// Checks if the application is configured to start with Windows.
    /// </summary>
    /// <returns>True if startup is enabled, false otherwise.</returns>
    public bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
