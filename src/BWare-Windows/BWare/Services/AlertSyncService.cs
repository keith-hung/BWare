using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BWare.Models;

namespace BWare.Services;

/// <summary>
/// Service for synchronizing alert state with Firebase Realtime Database.
/// Uses Server-Sent Events (SSE) for real-time updates.
/// </summary>
public class AlertSyncService : IDisposable
{
    private ParsedFirebaseUrl _firebaseUrl;
    private readonly string _clientId;
    private readonly HttpClient _httpClient;
    private readonly LocalAlertState _localAlertState;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private DateTime _lastClickTime = DateTime.MinValue;
    private AlertState _currentState = AlertState.Normal;

    // Health check timer for detecting stale connections
    private System.Threading.Timer? _healthCheckTimer;
    private DateTime? _lastDataReceivedTime;

    private const int ReconnectDelayMs = 5000;
    private const int ClickDebounceMs = 500;
    private const double ConnectionTimeoutSeconds = 90; // Firebase sends keep-alive every ~30s
    private const double HealthCheckIntervalSeconds = 30;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ConnectionState CurrentConnectionState { get; private set; } = ConnectionState.Connecting;

    /// <summary>
    /// Raised when the alert state changes.
    /// </summary>
    public event EventHandler<AlertState>? AlertStateChanged;

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public AlertSyncService(ParsedFirebaseUrl firebaseUrl, string clientId)
    {
        _firebaseUrl = firebaseUrl;
        _clientId = clientId;
        _localAlertState = new LocalAlertState();
        _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <summary>
    /// Starts the SSE connection to Firebase.
    /// </summary>
    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        await ConnectWithReconnectAsync(_cts.Token);
    }

    /// <summary>
    /// Stops the connection.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
    }

    /// <summary>
    /// Reconnects to a new Firebase URL.
    /// </summary>
    /// <param name="newUrl">The new Firebase URL to connect to.</param>
    public void Reconnect(ParsedFirebaseUrl newUrl)
    {
        Stop();
        _firebaseUrl = newUrl;
        _ = StartAsync();
    }

    /// <summary>
    /// Triggers an alert (with debouncing and duplicate prevention).
    /// Queues the trigger if currently disconnected.
    /// </summary>
    public async Task TriggerAlertAsync()
    {
        // Debounce: ignore clicks within 500ms
        var now = DateTime.UtcNow;
        if ((now - _lastClickTime).TotalMilliseconds < ClickDebounceMs)
            return;
        _lastClickTime = now;

        // Prevent duplicate triggers
        if (_currentState.Status == AlertStatus.Alert)
            return;

        // Also check if there's already a pending local trigger
        if (_localAlertState.PendingTrigger)
            return;

        // Queue trigger if disconnected
        if (!CurrentConnectionState.IsConnected)
        {
            _localAlertState.QueueTrigger();
            return;
        }

        var alertState = AlertState.CreateAlert(_clientId);
        await WriteAlertAsync(alertState);
    }

    /// <summary>
    /// Resets the alert state to normal.
    /// </summary>
    public async Task ResetToNormalAsync()
    {
        await WriteAlertAsync(AlertState.Normal);
    }

    /// <summary>
    /// Processes any queued trigger from when offline.
    /// Called immediately after connection is established.
    /// </summary>
    private async Task ProcessQueuedTriggerAsync()
    {
        if (!_localAlertState.PendingTrigger)
            return;

        // Check if queued trigger has expired
        if (_localAlertState.IsExpired)
        {
            _localAlertState.Reset();
            return;
        }

        // Send the queued trigger
        var alertState = AlertState.CreateAlert(_clientId);
        await WriteAlertAsync(alertState);

        // Clear local state after successful send
        _localAlertState.Reset();
    }

    private async Task ConnectWithReconnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Logger.Info("Connecting to Firebase...");
                CurrentConnectionState = ConnectionState.Connecting;
                ConnectionStateChanged?.Invoke(this, CurrentConnectionState);
                await ConnectAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Connection cancelled");
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Connection failed: {ex.GetType().Name} - {ex.Message}");
                var reason = ex switch
                {
                    HttpRequestException => DisconnectReason.NetworkUnavailable,
                    _ => DisconnectReason.Unknown
                };
                CurrentConnectionState = ConnectionState.Disconnected(reason);
                ConnectionStateChanged?.Invoke(this, CurrentConnectionState);

                // Wait before reconnecting
                try
                {
                    Logger.Info($"Retrying in {ReconnectDelayMs}ms...");
                    await Task.Delay(ReconnectDelayMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        Logger.Debug($"Requesting SSE from: {_firebaseUrl.SseUrl}");
        var request = new HttpRequestMessage(HttpMethod.Get, _firebaseUrl.SseUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

        using var response = await _httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();
        Logger.Info("Connected! Listening for events...");
        CurrentConnectionState = ConnectionState.Connected;
        ConnectionStateChanged?.Invoke(this, CurrentConnectionState);

        // Start health check timer
        StartHealthCheck();

        // Process any queued triggers from when offline
        await ProcessQueuedTriggerAsync();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? currentEvent = null;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) continue;

            // Update last data received time for health monitoring
            _lastDataReceivedTime = DateTime.UtcNow;

            if (line.StartsWith("event:"))
            {
                currentEvent = line[6..].Trim();
            }
            else if (line.StartsWith("data:") && currentEvent != null)
            {
                var data = line[5..].Trim();
                ProcessSSEData(currentEvent, data);
                currentEvent = null;
            }
        }
    }

    private void ProcessSSEData(string eventType, string data)
    {
        if (eventType == "keep-alive" || string.IsNullOrEmpty(data))
            return;

        if (eventType == "cancel" || eventType == "auth_revoked")
        {
            CurrentConnectionState = ConnectionState.Disconnected(DisconnectReason.AuthenticationFailed);
            ConnectionStateChanged?.Invoke(this, CurrentConnectionState);
            return;
        }

        try
        {
            // Parse the SSE payload: {"path": "/", "data": {...}}
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind != JsonValueKind.Null)
            {
                var alertState = JsonSerializer.Deserialize<AlertState>(dataElement.GetRawText());
                if (alertState != null)
                {
                    _currentState = alertState;

                    // Check if alert has expired
                    if (alertState.IsExpired)
                    {
                        _currentState = AlertState.Normal;
                    }

                    AlertStateChanged?.Invoke(this, _currentState);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to parse SSE data: {ex.Message}");
        }
    }

    private async Task WriteAlertAsync(AlertState state)
    {
        var json = JsonSerializer.Serialize(state);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            await _httpClient.PutAsync(_firebaseUrl.RestUrl, content);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to write alert: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts the periodic health check timer.
    /// Firebase sends keep-alive messages every ~30 seconds, so we check every 30 seconds
    /// and reconnect if no data has been received for 90+ seconds.
    /// </summary>
    private void StartHealthCheck()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = new System.Threading.Timer(
            _ => CheckConnectionHealth(),
            null,
            TimeSpan.FromSeconds(HealthCheckIntervalSeconds),
            TimeSpan.FromSeconds(HealthCheckIntervalSeconds)
        );
        Logger.Info($"Health check timer started (interval: {HealthCheckIntervalSeconds}s, timeout: {ConnectionTimeoutSeconds}s)");
    }

    /// <summary>
    /// Checks if the connection is still healthy based on last data received time.
    /// If no data has been received for longer than the timeout threshold, reconnects.
    /// </summary>
    private void CheckConnectionHealth()
    {
        if (CurrentConnectionState.Status != ConnectionStatus.Connected)
            return;

        if (_lastDataReceivedTime == null)
        {
            // No data ever received but marked as connected - reconnect
            Logger.Info("Health check: No data ever received, reconnecting...");
            ReconnectSSE();
            return;
        }

        var timeSinceLastData = DateTime.UtcNow - _lastDataReceivedTime.Value;
        Logger.Debug($"Health check: {(int)timeSinceLastData.TotalSeconds}s since last data");

        if (timeSinceLastData.TotalSeconds > ConnectionTimeoutSeconds)
        {
            Logger.Info($"Health check: Connection stale ({(int)timeSinceLastData.TotalSeconds}s > {ConnectionTimeoutSeconds}s), reconnecting...");
            ReconnectSSE();
        }
    }

    /// <summary>
    /// Forces a reconnection by cancelling the current connection and restarting.
    /// </summary>
    private void ReconnectSSE()
    {
        // Cancel current connection (will trigger retry logic in ConnectWithReconnectAsync)
        _cts?.Cancel();

        // Stop health check timer
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;

        // Start new connection
        _ = StartAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _healthCheckTimer?.Dispose();
        _httpClient.Dispose();
    }
}
