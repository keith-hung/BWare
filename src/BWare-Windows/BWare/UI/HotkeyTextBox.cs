using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BWare.Services;
using HotkeyBindingModel = BWare.Models.HotkeyBinding;

namespace BWare.UI;

/// <summary>
/// Custom TextBox that captures hotkey combinations.
/// </summary>
public class HotkeyTextBox : System.Windows.Controls.TextBox
{
    private uint _modifiers;
    private uint _key;
    private bool _isRecording;
    private HotkeyService? _hotkeyService;

    /// <summary>
    /// Current modifier flags.
    /// </summary>
    public uint Modifiers
    {
        get => _modifiers;
        set
        {
            _modifiers = value;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Current key code.
    /// </summary>
    public uint Key
    {
        get => _key;
        set
        {
            _key = value;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Gets whether a valid hotkey is set.
    /// </summary>
    public bool HasValidHotkey => _key != 0 && _modifiers != 0;

    /// <summary>
    /// Event fired when the hotkey combination changes.
    /// </summary>
    public event EventHandler<HotkeyChangedEventArgs>? HotkeyChanged;

    /// <summary>
    /// Sets the HotkeyService for availability checking.
    /// </summary>
    public void SetHotkeyService(HotkeyService service)
    {
        _hotkeyService = service;
    }

    public HotkeyTextBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = System.Windows.Input.Cursors.Hand;
        Text = "Click to set hotkey...";
        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
    }

    /// <summary>
    /// Sets the hotkey from a HotkeyBinding.
    /// </summary>
    public void SetHotkey(HotkeyBindingModel binding)
    {
        _modifiers = binding.Modifiers;
        _key = binding.Key;
        UpdateDisplay();
    }

    /// <summary>
    /// Gets the current hotkey as a HotkeyBinding.
    /// </summary>
    public HotkeyBindingModel GetHotkey(bool enabled)
    {
        return new HotkeyBindingModel
        {
            Enabled = enabled,
            Modifiers = _modifiers,
            Key = _key
        };
    }

    /// <summary>
    /// Clears the hotkey.
    /// </summary>
    public void ClearHotkey()
    {
        _modifiers = 0;
        _key = 0;
        UpdateDisplay();
        HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs(0, 0, true));
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        StartRecording();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        StopRecording();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;

        // Handle escape to cancel/clear
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            StopRecording();
            return;
        }

        // Handle delete/backspace to clear
        if (e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back)
        {
            ClearHotkey();
            StopRecording();
            return;
        }

        // Ignore modifier-only key presses
        if (IsModifierKey(e.Key))
        {
            // Show current modifiers while holding them
            var currentModifiers = GetCurrentModifiers();
            if (currentModifiers != 0)
            {
                Text = HotkeyService.GetHotkeyDisplayString(currentModifiers, 0).TrimEnd('+');
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)); // Blue while recording
            }
            return;
        }

        // Get the actual key (handle system keys)
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

        // Convert to Windows API format
        var (modifiers, vk) = HotkeyService.ConvertFromWpf(key, Keyboard.Modifiers);

        // Require at least one modifier
        if (modifiers == 0)
        {
            Text = "Please use Ctrl, Alt, or Shift";
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47)); // Red
            return;
        }

        // Check availability
        bool isAvailable = true;
        if (_hotkeyService != null)
        {
            isAvailable = _hotkeyService.TestHotkeyAvailability(modifiers, vk);
        }

        if (!isAvailable)
        {
            Text = $"{HotkeyService.GetHotkeyDisplayString(modifiers, vk)} (in use!)";
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47)); // Red
            ToolTip = "This hotkey is already registered by another application.";
            return;
        }

        // Set the hotkey
        _modifiers = modifiers;
        _key = vk;
        UpdateDisplay();
        StopRecording();

        HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs(_modifiers, _key, true));
    }

    protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnPreviewKeyUp(e);
            return;
        }

        e.Handled = true;

        // Update display when modifiers are released
        if (IsModifierKey(e.Key))
        {
            var currentModifiers = GetCurrentModifiers();
            if (currentModifiers == 0)
            {
                Text = "Press a key combination...";
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
            }
            else
            {
                Text = HotkeyService.GetHotkeyDisplayString(currentModifiers, 0).TrimEnd('+');
            }
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_isRecording)
        {
            Focus();
        }
    }

    private void StartRecording()
    {
        _isRecording = true;
        Text = "Press a key combination...";
        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)); // Blue
        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(227, 242, 253)); // Light blue
        ToolTip = "Press Escape to cancel, Delete to clear";
    }

    private void StopRecording()
    {
        _isRecording = false;
        Background = System.Windows.Media.Brushes.White;
        ToolTip = null;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_key == 0 || _modifiers == 0)
        {
            Text = "Click to set hotkey...";
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)); // Gray
        }
        else
        {
            Text = HotkeyService.GetHotkeyDisplayString(_modifiers, _key);
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)); // Dark gray
        }
    }

    private static bool IsModifierKey(System.Windows.Input.Key key)
    {
        return key == System.Windows.Input.Key.LeftCtrl ||
               key == System.Windows.Input.Key.RightCtrl ||
               key == System.Windows.Input.Key.LeftAlt ||
               key == System.Windows.Input.Key.RightAlt ||
               key == System.Windows.Input.Key.LeftShift ||
               key == System.Windows.Input.Key.RightShift ||
               key == System.Windows.Input.Key.LWin ||
               key == System.Windows.Input.Key.RWin;
    }

    private uint GetCurrentModifiers()
    {
        uint modifiers = 0;
        if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl))
            modifiers |= HotkeyService.MOD_CONTROL;
        if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt))
            modifiers |= HotkeyService.MOD_ALT;
        if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
            modifiers |= HotkeyService.MOD_SHIFT;
        if (Keyboard.IsKeyDown(System.Windows.Input.Key.LWin) || Keyboard.IsKeyDown(System.Windows.Input.Key.RWin))
            modifiers |= HotkeyService.MOD_WIN;
        return modifiers;
    }
}

/// <summary>
/// Event arguments for hotkey changed events.
/// </summary>
public class HotkeyChangedEventArgs : EventArgs
{
    public uint Modifiers { get; }
    public uint Key { get; }
    public bool IsAvailable { get; }

    public HotkeyChangedEventArgs(uint modifiers, uint key, bool isAvailable)
    {
        Modifiers = modifiers;
        Key = key;
        IsAvailable = isAvailable;
    }
}
