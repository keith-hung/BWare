namespace BWare.Models;

/// <summary>
/// Tracks pending offline triggers (not persisted, in-memory only).
/// </summary>
public class LocalAlertState
{
    /// <summary>
    /// Gets or sets whether there is a pending trigger waiting to be sent.
    /// </summary>
    public bool PendingTrigger { get; private set; }

    /// <summary>
    /// Gets or sets the local expiration time for the queued alert.
    /// </summary>
    public DateTime? LocalExpiresAt { get; private set; }

    /// <summary>
    /// Gets or sets when the trigger was queued.
    /// </summary>
    public DateTime? QueuedAt { get; private set; }

    /// <summary>
    /// Resets the local alert state (clears pending trigger).
    /// </summary>
    public void Reset()
    {
        PendingTrigger = false;
        LocalExpiresAt = null;
        QueuedAt = null;
    }

    /// <summary>
    /// Queues a trigger to be sent when connection is restored.
    /// </summary>
    public void QueueTrigger()
    {
        // Don't queue if already pending
        if (PendingTrigger)
            return;

        PendingTrigger = true;
        QueuedAt = DateTime.UtcNow;
        LocalExpiresAt = DateTime.UtcNow.AddSeconds(60);
    }

    /// <summary>
    /// Gets whether the queued trigger has expired locally.
    /// </summary>
    public bool IsExpired => PendingTrigger && LocalExpiresAt.HasValue && DateTime.UtcNow > LocalExpiresAt.Value;

    /// <summary>
    /// Gets the remaining seconds for the queued trigger (0 if expired or not pending).
    /// </summary>
    public int RemainingSeconds
    {
        get
        {
            if (!PendingTrigger || !LocalExpiresAt.HasValue)
                return 0;

            var remaining = (LocalExpiresAt.Value - DateTime.UtcNow).TotalSeconds;
            return Math.Max(0, (int)remaining);
        }
    }
}
