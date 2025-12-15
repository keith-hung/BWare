using System.Text.Json.Serialization;

namespace BWare.Models;

/// <summary>
/// Configuration for a single hotkey binding.
/// </summary>
public record HotkeyBinding
{
    /// <summary>
    /// Whether this hotkey is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Modifier flags (Ctrl=2, Alt=1, Shift=4, Win=8).
    /// </summary>
    [JsonPropertyName("modifiers")]
    public uint Modifiers { get; init; }

    /// <summary>
    /// Virtual key code.
    /// </summary>
    [JsonPropertyName("key")]
    public uint Key { get; init; }

    /// <summary>
    /// Gets a display string for this hotkey (e.g., "Ctrl+Alt+B").
    /// </summary>
    [JsonIgnore]
    public string DisplayString => Services.HotkeyService.GetHotkeyDisplayString(Modifiers, Key);

    /// <summary>
    /// Creates default hotkey binding for Ctrl+Alt+B (trigger alert).
    /// </summary>
    public static HotkeyBinding DefaultTriggerAlert() => new()
    {
        Enabled = true,
        Modifiers = Services.HotkeyService.MOD_CONTROL | Services.HotkeyService.MOD_ALT,
        Key = (uint)System.Windows.Forms.Keys.B
    };

    /// <summary>
    /// Creates an empty/disabled hotkey binding.
    /// </summary>
    public static HotkeyBinding Empty() => new()
    {
        Enabled = false,
        Modifiers = 0,
        Key = 0
    };

    /// <summary>
    /// Checks if this binding has a valid key combination.
    /// </summary>
    [JsonIgnore]
    public bool HasValidKey => Key != 0 && Modifiers != 0;
}

/// <summary>
/// Collection of all hotkey configurations.
/// </summary>
public record HotkeyConfiguration
{
    /// <summary>
    /// Hotkey for triggering an alert.
    /// </summary>
    [JsonPropertyName("triggerAlert")]
    public HotkeyBinding TriggerAlert { get; init; } = HotkeyBinding.DefaultTriggerAlert();

    /// <summary>
    /// Creates default hotkey configuration.
    /// </summary>
    public static HotkeyConfiguration Default() => new()
    {
        TriggerAlert = HotkeyBinding.DefaultTriggerAlert()
    };
}
