namespace BWare.Models;

/// <summary>
/// Represents the current state of the shared alert system.
/// Maps to Firebase values: "normal" and "alert".
/// </summary>
public enum AlertStatus
{
    /// <summary>
    /// Default state - displays green icon.
    /// </summary>
    Normal,

    /// <summary>
    /// Active alert state - displays red icon.
    /// </summary>
    Alert
}
