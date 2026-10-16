using ModernCalculator.WinForms.Themes;
using System.Drawing.Drawing2D;

namespace ModernCalculator.WinForms.Controls;

/// <summary>Retains native checkbox keyboard/accessibility semantics with the shared flat appearance.</summary>
public sealed class FlatCheckBox : CheckBox, IThemedControl
{
    private Palette palette = Palette.Resolve(Infrastructure.Settings.ThemePreference.Light);
    private bool hovered;
    public FlatCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        Dock = DockStyle.Fill; AutoSize = false; Margin = new(4, 2, 4, 2); Cursor = Cursors.Hand;
        Ui.SetFont(this, 9); _ = new FittedText(this); AccessibleRole = AccessibleRole.CheckButton;
    }
    public void ApplyTheme(Palette theme) { palette = theme; BackColor = theme.Surface; ForeColor = theme.Text; Invalidate(); }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(hovered ? palette.Key : palette.Surface); g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f; var size = Math.Min(18 * scale, Height - 4);
        if (size < 2) return;
        var box = new RectangleF(3 * scale, (Height - size) / 2, size, size);
        using var shape = VectorIcon.Rounded(box, 4 * scale);
        using var fill = new SolidBrush(Checked ? palette.Accent : palette.Surface); g.FillPath(fill, shape);
        using var outline = new Pen(Checked ? palette.Accent : palette.Muted, 1.3f * scale); g.DrawPath(outline, shape);
        if (Checked) VectorIcon.Draw(g, IconKind.Check, RectangleF.Inflate(box, -2 * scale, -2 * scale), palette.AccentText);
        var text = new Rectangle((int)(30 * scale), 0, Math.Max(1, Width - (int)(32 * scale)), Height);
        TextRenderer.DrawText(g, Text, Font, text, Enabled ? palette.Text : palette.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) { using var focus = new Pen(palette.Accent, 1.4f * scale); g.DrawRectangle(focus, 1, 1, Math.Max(0, Width - 3), Math.Max(0, Height - 3)); }
    }
}
