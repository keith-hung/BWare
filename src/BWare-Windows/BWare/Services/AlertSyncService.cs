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

    private const int ReconnectDelayMs = 5000;
    private const int ClickDebounceMs = 500;

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
                CurrentConnectionState = ConnectionState.Connecting;
                ConnectionStateChanged?.Invoke(this, CurrentConnectionState);
                await ConnectAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
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
        var request = new HttpRequestMessage(HttpMethod.Get, _firebaseUrl.SseUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

        using var response = await _httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();
        CurrentConnectionState = ConnectionState.Connected;
        ConnectionStateChanged?.Invoke(this, CurrentConnectionState);

        // Process any queued triggers from when offline
        await ProcessQueuedTriggerAsync();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? currentEvent = null;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) continue;

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
        catch
        {
            // Ignore JSON parsing errors
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
        catch
        {
            // Handle write errors (will be enhanced in Phase 7 for offline queue)
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _httpClient.Dispose();
    }
}
