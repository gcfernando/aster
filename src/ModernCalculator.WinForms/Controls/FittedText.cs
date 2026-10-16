// Developer ::> Gehan Fernando
namespace ModernCalculator.WinForms.Controls;

/// <summary>Owns one fitted font per control; the requested point size remains the upper bound.</summary>
internal sealed class FittedText : IDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, FittedText> instances = new();
    private readonly Control control;
    private readonly Func<float>? availableTextHeight;
    private Font? owned;
    private float preferred;
    private string family;
    private FontStyle style;
    private bool changing;
    public FittedText(Control control, Func<float>? availableTextHeight = null)
    {
        this.control = control; this.availableTextHeight = availableTextHeight;
        preferred = control.Font.SizeInPoints; family = control.Font.Name; style = control.Font.Style;
        instances.Add(control, this);
        control.FontChanged += FontChanged; control.SizeChanged += Changed; control.TextChanged += Changed;
        control.Disposed += (_, _) => Dispose();
    }
    public static void Request(Control control, Font font)
    {
        if (!instances.TryGetValue(control, out var fitted) || fitted.changing) return;
        fitted.preferred = font.SizeInPoints; fitted.family = font.Name; fitted.style = font.Style; fitted.Fit();
    }
    private void FontChanged(object? sender, EventArgs e)
    {
        if (changing) return;
        preferred = control.Font.SizeInPoints; family = control.Font.Name; style = control.Font.Style; Fit();
    }
    private void Changed(object? sender, EventArgs e) => Fit();
    private void Fit()
    {
        if (changing || control.IsDisposed || control.Width <= 4 || control.Height <= 4) return;
        var scale = control.DeviceDpi / 96f;
        var height = Math.Max(1, (availableTextHeight?.Invoke() ?? control.Height) - 3 * scale);
        var width = Math.Max(1, control.Width - 6 * scale);
        if (control is CalcButton { Icon: not IconKind.None, Text.Length: > 0 }) width -= 40 * scale;
        var pointSize = preferred;
        while (pointSize > 8)
        {
            using var candidate = new Font(family, pointSize, style);
            var measured = TextRenderer.MeasureText(control.Text, candidate, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            var actualHeight = candidate.Height;
            var actualWidth = measured.Width;
            if (actualHeight <= height && actualWidth <= width) break;
            pointSize = Math.Max(8, pointSize - .5f);
        }
        if (control.Font.SizeInPoints == pointSize && control.Font.Name == family && control.Font.Style == style) return;
        // Guard font replacement because assigning Font also raises FontChanged.
        changing = true;
        try
        {
            var next = new Font(family, pointSize, style); var previous = owned;
            control.Font = next; owned = next; previous?.Dispose();
        }
        finally { changing = false; }
    }
    public void Dispose() { changing = true; owned?.Dispose(); owned = null; }
}
