namespace BWare.Services;

/// <summary>
/// Service for countdown timer functionality during active alerts.
/// </summary>
public class TimerService : IDisposable
{
    private System.Timers.Timer? _timer;
    private long _expiresAt;
    private bool _disposed;

    /// <summary>
    /// Raised every second with the remaining seconds count.
    /// </summary>
    public event EventHandler<int>? Tick;

    /// <summary>
    /// Raised when the countdown reaches zero.
    /// </summary>
    public event EventHandler? Expired;

    /// <summary>
    /// Starts the countdown timer.
    /// </summary>
    /// <param name="expiresAt">Unix timestamp when the alert expires.</param>
    public void Start(long expiresAt)
    {
        Stop();

        _expiresAt = expiresAt;
        _timer = new System.Timers.Timer(1000); // 1 second interval
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();

        // Fire initial tick
        OnTimerElapsed(null, null!);
    }

    /// <summary>
    /// Stops the countdown timer.
    /// </summary>
    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Elapsed -= OnTimerElapsed;
            _timer.Dispose();
            _timer = null;
        }
    }

    /// <summary>
    /// Gets the remaining seconds until expiration.
    /// </summary>
    public int RemainingSeconds
    {
        get
        {
            if (_expiresAt == 0) return 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Math.Max(0, (int)(_expiresAt - now));
        }
    }

    /// <summary>
    /// Returns true if the timer has expired.
    /// </summary>
    public bool IsExpired => _expiresAt > 0 && RemainingSeconds <= 0;

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs? e)
    {
        var remaining = RemainingSeconds;

        if (remaining <= 0)
        {
            Stop();
            Expired?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Tick?.Invoke(this, remaining);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
