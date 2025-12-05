using System.Text.Json.Serialization;

namespace BWare.Models;

/// <summary>
/// Shared state synchronized across all connected devices via Firebase Realtime Database.
/// </summary>
public record AlertState
{
    /// <summary>
    /// Current alert status (normal or alert).
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(AlertStatusJsonConverter))]
    public AlertStatus Status { get; init; }

    /// <summary>
    /// Unix timestamp (seconds) when alert expires. 0 when status is normal.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public long ExpiresAt { get; init; }

    /// <summary>
    /// UUID of the client that triggered the alert.
    /// </summary>
    [JsonPropertyName("triggeredBy")]
    public string? TriggeredBy { get; init; }

    /// <summary>
    /// Unix timestamp (seconds) when alert was triggered.
    /// </summary>
    [JsonPropertyName("triggeredAt")]
    public long? TriggeredAt { get; init; }

    /// <summary>
    /// Gets the remaining seconds until the alert expires.
    /// Returns 0 if already expired or status is normal.
    /// </summary>
    [JsonIgnore]
    public int RemainingSeconds
    {
        get
        {
            if (Status != AlertStatus.Alert || ExpiresAt == 0)
                return 0;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Math.Max(0, (int)(ExpiresAt - now));
        }
    }

    /// <summary>
    /// Returns true if the alert has expired (status is alert but expiresAt has passed).
    /// </summary>
    [JsonIgnore]
    public bool IsExpired
    {
        get
        {
            if (Status != AlertStatus.Alert)
                return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return ExpiresAt <= now;
        }
    }

    /// <summary>
    /// Creates a default normal state.
    /// </summary>
    public static AlertState Normal => new()
    {
        Status = AlertStatus.Normal,
        ExpiresAt = 0
    };

    /// <summary>
    /// Creates an alert state with 60-second duration.
    /// </summary>
    /// <param name="clientId">The client ID that triggers the alert.</param>
    /// <returns>A new AlertState in alert status.</returns>
    public static AlertState CreateAlert(string clientId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new AlertState
        {
            Status = AlertStatus.Alert,
            ExpiresAt = now + 60,
            TriggeredBy = clientId,
            TriggeredAt = now
        };
    }
}

/// <summary>
/// JSON converter for AlertStatus enum to/from Firebase string values.
/// </summary>
public class AlertStatusJsonConverter : JsonConverter<AlertStatus>
{
    public override AlertStatus Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "alert" => AlertStatus.Alert,
            _ => AlertStatus.Normal
        };
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, AlertStatus value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            AlertStatus.Alert => "alert",
            _ => "normal"
        });
    }
}
