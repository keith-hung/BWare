using System.Runtime.InteropServices;

namespace BWare.Services;

/// <summary>
/// Manages global hotkey registration using Windows API.
/// </summary>
public class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier flags
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // Hotkey IDs
    public const int HOTKEY_TRIGGER_ALERT = 1;
    public const int HOTKEY_CANCEL_ALERT = 2;

    // WM_HOTKEY message
    public const int WM_HOTKEY = 0x0312;

    private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys = new();
    private HotkeyMessageWindow? _messageWindow;
    private bool _disposed;

    /// <summary>
    /// Fired when a registered hotkey is pressed.
    /// </summary>
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>
    /// Initializes the hotkey service by creating a message-only window.
    /// </summary>
    public void Initialize()
    {
        if (_messageWindow != null) return;

        _messageWindow = new HotkeyMessageWindow(this);
        Logger.Info("HotkeyService initialized");
    }

    /// <summary>
    /// Registers a global hotkey.
    /// </summary>
    /// <param name="id">Unique hotkey ID.</param>
    /// <param name="modifiers">Modifier keys (Ctrl, Alt, Shift, Win).</param>
    /// <param name="key">The key code.</param>
    /// <returns>True if registration succeeded, false if hotkey is already in use.</returns>
    public bool RegisterHotkey(int id, uint modifiers, uint key)
    {
        if (_messageWindow == null)
        {
            Logger.Warning("HotkeyService not initialized");
            return false;
        }

        // Unregister existing hotkey with same ID first
        UnregisterHotkey(id);

        // Add MOD_NOREPEAT to prevent repeated firing when key is held
        var result = RegisterHotKey(_messageWindow.Handle, id, modifiers | MOD_NOREPEAT, key);

        if (result)
        {
            _registeredHotkeys[id] = new HotkeyBinding(id, modifiers, key);
            Logger.Info($"Hotkey registered: ID={id}, Modifiers={modifiers}, Key={key}");
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            Logger.Warning($"Failed to register hotkey ID={id}: Win32 error {error}");
        }

        return result;
    }

    /// <summary>
    /// Unregisters a previously registered hotkey.
    /// </summary>
    /// <param name="id">The hotkey ID to unregister.</param>
    public void UnregisterHotkey(int id)
    {
        if (_messageWindow == null || !_registeredHotkeys.ContainsKey(id)) return;

        UnregisterHotKey(_messageWindow.Handle, id);
        _registeredHotkeys.Remove(id);
        Logger.Info($"Hotkey unregistered: ID={id}");
    }

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        if (_messageWindow == null) return;

        foreach (var id in _registeredHotkeys.Keys.ToList())
        {
            UnregisterHotKey(_messageWindow.Handle, id);
        }
        _registeredHotkeys.Clear();
        Logger.Info("All hotkeys unregistered");
    }

    /// <summary>
    /// Tests if a hotkey combination is available (not registered by another application).
    /// </summary>
    /// <param name="modifiers">Modifier keys.</param>
    /// <param name="key">The key code.</param>
    /// <returns>True if the hotkey is available, false if it's in use.</returns>
    public bool TestHotkeyAvailability(uint modifiers, uint key)
    {
        if (_messageWindow == null) return false;

        const int testId = 9999;
        var result = RegisterHotKey(_messageWindow.Handle, testId, modifiers | MOD_NOREPEAT, key);

        if (result)
        {
            // Hotkey is available, unregister the test registration
            UnregisterHotKey(_messageWindow.Handle, testId);
        }

        return result;
    }

    /// <summary>
    /// Called by the message window when a hotkey is pressed.
    /// </summary>
    internal void OnHotkeyPressed(int id)
    {
        Logger.Info($"Hotkey pressed: ID={id}");
        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(id));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        _messageWindow?.DestroyHandle();
        _messageWindow = null;
    }

    /// <summary>
    /// Converts modifier flags and key code to a display string.
    /// </summary>
    public static string GetHotkeyDisplayString(uint modifiers, uint key)
    {
        var parts = new List<string>();

        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");

        var keyName = ((Keys)key).ToString();
        parts.Add(keyName);

        return string.Join("+", parts);
    }

    /// <summary>
    /// Parses WPF key and modifiers to Windows API format.
    /// </summary>
    public static (uint modifiers, uint key) ConvertFromWpf(System.Windows.Input.Key wpfKey, System.Windows.Input.ModifierKeys wpfModifiers)
    {
        uint modifiers = 0;
        if ((wpfModifiers & System.Windows.Input.ModifierKeys.Control) != 0) modifiers |= MOD_CONTROL;
        if ((wpfModifiers & System.Windows.Input.ModifierKeys.Alt) != 0) modifiers |= MOD_ALT;
        if ((wpfModifiers & System.Windows.Input.ModifierKeys.Shift) != 0) modifiers |= MOD_SHIFT;
        if ((wpfModifiers & System.Windows.Input.ModifierKeys.Windows) != 0) modifiers |= MOD_WIN;

        // Convert WPF Key to WinForms Keys (virtual key code)
        var formsKey = (Keys)System.Windows.Input.KeyInterop.VirtualKeyFromKey(wpfKey);
        return (modifiers, (uint)formsKey);
    }
}

/// <summary>
/// Event arguments for hotkey pressed events.
/// </summary>
public class HotkeyPressedEventArgs : EventArgs
{
    public int HotkeyId { get; }

    public HotkeyPressedEventArgs(int hotkeyId)
    {
        HotkeyId = hotkeyId;
    }
}

/// <summary>
/// Represents a registered hotkey binding.
/// </summary>
public record HotkeyBinding(int Id, uint Modifiers, uint Key);

/// <summary>
/// Hidden message-only window to receive hotkey messages.
/// </summary>
internal class HotkeyMessageWindow : NativeWindow
{
    private readonly HotkeyService _service;

    public HotkeyMessageWindow(HotkeyService service)
    {
        _service = service;
        CreateHandle(new CreateParams
        {
            Caption = "BWareHotkeyWindow",
            // HWND_MESSAGE (-3) creates a message-only window
            Parent = new IntPtr(-3)
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == HotkeyService.WM_HOTKEY)
        {
            var id = m.WParam.ToInt32();
            _service.OnHotkeyPressed(id);
        }

        base.WndProc(ref m);
    }
}
