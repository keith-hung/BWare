using Serilog;
using Serilog.Events;
using System.IO;

namespace BWare.Services;

/// <summary>
/// Simple file logger for B-Ware Windows client.
/// Logs are written to %APPDATA%\BWare\logs\
/// </summary>
public static class Logger
{
    private static readonly string LogPath;
    private static ILogger? _logger;

    static Logger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appData, "BWare", "logs");
        Directory.CreateDirectory(logDir);

        LogPath = Path.Combine(logDir, "bware-{Date}.log");
    }

    /// <summary>
    /// Initializes the logger. Call this once at application startup.
    /// </summary>
    public static void Initialize()
    {
        _logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.RollingFile(
                LogPath,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Info("=== B-Ware Windows Client Started ===");
        Info($"Log file pattern: {LogPath}");
    }

    public static void Debug(string message)
    {
        _logger?.Debug(message);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
    }

    public static void Info(string message)
    {
        _logger?.Information(message);
        System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
    }

    public static void Warning(string message)
    {
        _logger?.Warning(message);
        System.Diagnostics.Debug.WriteLine($"[WARN] {message}");
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (ex != null)
            _logger?.Error(ex, message);
        else
            _logger?.Error(message);

        System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
        if (ex != null)
            System.Diagnostics.Debug.WriteLine($"  Exception: {ex}");
    }

    /// <summary>
    /// Flushes and closes the logger. Call this on application shutdown.
    /// </summary>
    public static void Close()
    {
        (_logger as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Gets the path to the current log file.
    /// </summary>
    public static string GetLogPath() => LogPath;
}
