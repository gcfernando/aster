namespace ModernCalculator.WinForms.Forms;

public sealed record WindowMetrics(Size InitialSize, Size MinimumSize);

/// <summary>Physical window bounds derived from effective pixels and the current monitor's work area.</summary>
public static class WindowLayoutPolicy
{
    public static WindowMetrics Calculate(string mode, Size workingArea, int dpi, bool drawerVisible = false, bool compact = false)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0) throw new ArgumentOutOfRangeException(nameof(workingArea));
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        var scale = dpi / 96d;
        var preferred = mode switch
        {
            "Scientific" => new Size(650, 710), "Programmer" => new Size(670, 710),
            "Converter" => new Size(650, 650), "Dates" => new Size(590, 620), _ => new Size(480, 680)
        };
        var minimum = mode switch
        {
            "Scientific" => new Size(540, 480), "Programmer" => new Size(570, 500),
            "Converter" => new Size(550, 470), "Dates" => new Size(480, 450), _ => new Size(380, 460)
        };
        if (compact) preferred.Height = Math.Max(minimum.Height, (int)Math.Round(preferred.Height * .88));
        if (drawerVisible) { preferred.Width += 260; minimum.Width += 220; }
        // Leave desktop space on every edge even when the desired effective size cannot fit.
        var inset = Math.Min((int)Math.Round(16 * scale), Math.Min(workingArea.Width, workingArea.Height) / 20);
        var available = new Size(Math.Max(1, workingArea.Width - inset * 2), Math.Max(1, workingArea.Height - inset * 2));
        var initial = new Size(Math.Min((int)Math.Round(preferred.Width * scale), available.Width), Math.Min((int)Math.Round(preferred.Height * scale), available.Height));
        var smallest = new Size(Math.Min((int)Math.Round(minimum.Width * scale), initial.Width), Math.Min((int)Math.Round(minimum.Height * scale), initial.Height));
        return new(initial, smallest);
    }
}
