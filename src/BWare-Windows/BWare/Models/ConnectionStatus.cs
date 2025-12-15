namespace BWare.Models;

/// <summary>
/// Represents the connection state to Firebase.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Currently attempting to establish connection.
    /// </summary>
    Connecting,

    /// <summary>
    /// Successfully connected to Firebase.
    /// </summary>
    Connected,

    /// <summary>
    /// Connection lost or failed.
    /// </summary>
    Disconnected
}
