using System.Drawing;
using System.Drawing.Drawing2D;

namespace BWare.Resources;

/// <summary>
/// Generates system tray icons dynamically.
/// This allows us to create icons at runtime without pre-built .ico files.
/// </summary>
public static class IconGenerator
{
    private const int IconSize = 16;
    private const int LargeIconSize = 32;

    /// <summary>
    /// Creates a circular icon with the specified color.
    /// </summary>
    /// <param name="color">The fill color for the circle.</param>
    /// <param name="size">Icon size (default 16x16).</param>
    /// <returns>A new Icon instance.</returns>
    public static Icon CreateCircleIcon(Color color, int size = IconSize)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        // Draw filled circle with slight padding
        var padding = size / 8;
        var circleSize = size - (padding * 2);

        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, padding, padding, circleSize, circleSize);

        // Add subtle border for better visibility
        using var pen = new Pen(Color.FromArgb(80, 0, 0, 0), 1f);
        graphics.DrawEllipse(pen, padding, padding, circleSize, circleSize);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Creates a green icon for normal status.
    /// </summary>
    public static Icon CreateGreenIcon(int size = IconSize) =>
        CreateCircleIcon(Color.FromArgb(76, 175, 80), size); // Material Design Green 500

    /// <summary>
    /// Creates a red icon for alert status.
    /// </summary>
    public static Icon CreateRedIcon(int size = IconSize) =>
        CreateCircleIcon(Color.FromArgb(244, 67, 54), size); // Material Design Red 500

    /// <summary>
    /// Creates a gray icon for disconnected status.
    /// </summary>
    public static Icon CreateGrayIcon(int size = IconSize) =>
        CreateCircleIcon(Color.FromArgb(158, 158, 158), size); // Material Design Gray 500

    /// <summary>
    /// Creates an application icon (larger, for windows and file explorer).
    /// </summary>
    public static Icon CreateAppIcon()
    {
        using var bitmap = new Bitmap(LargeIconSize, LargeIconSize);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        // Draw a "B" letter in a circle
        var padding = 2;
        var circleSize = LargeIconSize - (padding * 2);

        // Background circle
        using var bgBrush = new SolidBrush(Color.FromArgb(33, 150, 243)); // Material Blue 500
        graphics.FillEllipse(bgBrush, padding, padding, circleSize, circleSize);

        // Letter "B"
        using var font = new Font("Arial", 18, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var textSize = graphics.MeasureString("B", font);
        var x = (LargeIconSize - textSize.Width) / 2;
        var y = (LargeIconSize - textSize.Height) / 2;
        graphics.DrawString("B", font, textBrush, x, y);

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
