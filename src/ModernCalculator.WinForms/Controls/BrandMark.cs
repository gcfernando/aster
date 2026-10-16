// Developer ::> Gehan Fernando
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Controls;

public sealed class BrandMark : Control, IThemedControl
{
    private Palette palette = Palette.Resolve(Infrastructure.Settings.ThemePreference.Dark);
    public BrandMark() { Dock = DockStyle.Fill; DoubleBuffered = true; AccessibleName = "Aster Calculator"; }
    public void ApplyTheme(Palette theme) { palette = theme; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var scale = DeviceDpi / 96f;
        e.Graphics.Clear(palette.Window);
        VectorIcon.Draw(e.Graphics, IconKind.Aster, new(4 * scale, (Height - 22 * scale) / 2, 22 * scale, 22 * scale), palette.Accent);
        using var title = new Font("Segoe UI", 13, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, "Aster", title, new Rectangle((int)(37 * scale), 0, Width - (int)(37 * scale), Height), palette.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }
}
