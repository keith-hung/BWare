using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BWare.Models;

/// <summary>
/// Local configuration stored in %APPDATA%\BWare\settings.json.
/// </summary>
public record ClientConfiguration
{
    /// <summary>
    /// Firebase Realtime Database URL with path.
    /// </summary>
    [JsonPropertyName("databaseUrl")]
    public required string DatabaseUrl { get; init; }

    /// <summary>
    /// Whether to start with Windows.
    /// </summary>
    [JsonPropertyName("launchAtLogin")]
    public bool LaunchAtLogin { get; init; }

    /// <summary>
    /// Unique client identifier (UUID, generated once on first run).
    /// </summary>
    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    /// <summary>
    /// Last successful connection timestamp.
    /// </summary>
    [JsonPropertyName("lastConnected")]
    public DateTime? LastConnected { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Gets the settings file path (%APPDATA%\BWare\settings.json).
    /// </summary>
    public static string SettingsPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "BWare", "settings.json");
        }
    }

    /// <summary>
    /// Loads configuration from the settings file.
    /// </summary>
    /// <returns>The loaded configuration, or null if file doesn't exist or is invalid.</returns>
    public static ClientConfiguration? Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClientConfiguration>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the configuration to the settings file.
    /// </summary>
    public void Save()
    {
        var path = SettingsPath;
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Generates a new unique client ID.
    /// </summary>
    /// <returns>A new UUID string.</returns>
    public static string GenerateClientId() => Guid.NewGuid().ToString();

    /// <summary>
    /// Creates a new configuration with the specified database URL.
    /// </summary>
    /// <param name="databaseUrl">The Firebase database URL.</param>
    /// <returns>A new configuration with a generated client ID.</returns>
    public static ClientConfiguration Create(string databaseUrl) => new()
    {
        DatabaseUrl = databaseUrl,
        ClientId = GenerateClientId(),
        LaunchAtLogin = false,
        LastConnected = null
    };

    /// <summary>
    /// Creates a copy with updated last connected timestamp.
    /// </summary>
    /// <returns>A new configuration with current timestamp as LastConnected.</returns>
    public ClientConfiguration WithLastConnected() => this with { LastConnected = DateTime.UtcNow };
}
