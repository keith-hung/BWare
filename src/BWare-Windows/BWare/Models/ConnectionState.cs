namespace BWare.Models;

/// <summary>
/// Encapsulates connection status with optional disconnect reason.
/// </summary>
public record ConnectionState
{
    /// <summary>
    /// Current connection status.
    /// </summary>
    public ConnectionStatus Status { get; init; }

    /// <summary>
    /// Reason for disconnection (when status is Disconnected).
    /// </summary>
    public DisconnectReason? Reason { get; init; }

    /// <summary>
    /// Returns true if currently connected to Firebase.
    /// </summary>
    public bool IsConnected => Status == ConnectionStatus.Connected;

    /// <summary>
    /// Gets display text for the current connection state.
    /// </summary>
    public string DisplayText => Status switch
    {
        ConnectionStatus.Connecting => "Connecting...",
        ConnectionStatus.Connected => "Connected",
        ConnectionStatus.Disconnected => Reason switch
        {
            DisconnectReason.NetworkUnavailable => "Disconnected: Network unavailable",
            DisconnectReason.AuthenticationFailed => "Disconnected: Authentication failed",
            DisconnectReason.ServerError => "Disconnected: Server error",
            _ => "Disconnected"
        },
        _ => "Unknown"
    };

    /// <summary>
    /// Creates a connecting state.
    /// </summary>
    public static ConnectionState Connecting => new() { Status = ConnectionStatus.Connecting };

    /// <summary>
    /// Creates a connected state.
    /// </summary>
    public static ConnectionState Connected => new() { Status = ConnectionStatus.Connected };

    /// <summary>
    /// Creates a disconnected state with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for disconnection.</param>
    /// <returns>A new ConnectionState in disconnected status.</returns>
    public static ConnectionState Disconnected(DisconnectReason reason = DisconnectReason.Unknown) =>
        new() { Status = ConnectionStatus.Disconnected, Reason = reason };
}
