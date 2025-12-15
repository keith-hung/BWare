namespace BWare.Models;

/// <summary>
/// Provides detail when connection to Firebase is lost.
/// </summary>
public enum DisconnectReason
{
    /// <summary>
    /// Network is unavailable.
    /// </summary>
    NetworkUnavailable,

    /// <summary>
    /// Firebase authentication/permission failed.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// Firebase server returned an error.
    /// </summary>
    ServerError,

    /// <summary>
    /// Unknown or unspecified reason.
    /// </summary>
    Unknown
}
