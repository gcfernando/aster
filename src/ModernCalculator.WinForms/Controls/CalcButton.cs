// Developer ::> Gehan Fernando
using System.Drawing.Drawing2D;
using System.ComponentModel;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Controls;

public enum ButtonTone { Normal, Number, Operator, Accent, Quiet }

public sealed class CalcButton : Button, IThemedControl
{
    private Palette palette = Palette.Resolve(Infrastructure.Settings.ThemePreference.Dark);
    private bool hovered;
    private bool pressed;
    private readonly System.Windows.Forms.Timer transition = new() { Interval = 15 };
    private float hoverFade;
    [DefaultValue(IconKind.None)] public IconKind Icon { get; set; }
    [DefaultValue(ButtonTone.Normal)] public ButtonTone Tone { get; set; }
    [DefaultValue(false)] public bool Selected { get; set; }
    [DefaultValue(false)] public bool VerticalIcon { get; set; }

    public CalcButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand; Margin = new(DesignTokens.Gutter); Dock = DockStyle.Fill;
        Ui.SetFont(this, 12); AccessibleRole = AccessibleRole.PushButton;
        _ = new FittedText(this);
        transition.Tick += (_, _) =>
        {
            var target = hovered && Enabled ? 1f : 0f;
            if (PresentationSettings.ReducedMotion) { hoverFade = target; transition.Stop(); Invalidate(); return; }
            hoverFade += (target - hoverFade) * .35f;
            if (Math.Abs(target - hoverFade) < .02f) { hoverFade = target; transition.Stop(); }
            Invalidate();
        };
    }
    public void ApplyTheme(Palette theme) { palette = theme; BackColor = theme.Surface; Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; if (PresentationSettings.ReducedMotion) hoverFade = 1; else transition.Start(); Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; if (PresentationSettings.ReducedMotion) hoverFade = 0; else transition.Start(); Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        var accent = Tone == ButtonTone.Accent;
        var fill = accent ? palette.Accent : Selected ? palette.NumberKey : Tone switch
        {
            ButtonTone.Number => palette.NumberKey, ButtonTone.Quiet => palette.Surface, _ => palette.Key
        };
        if (Enabled && (hoverFade > 0 || pressed)) fill = Mix(fill, palette.Text, pressed ? .14f : .07f * hoverFade);
        var color = !Enabled ? Mix(palette.Muted, palette.Surface, .6f) : accent ? palette.AccentText :
            Tone == ButtonTone.Operator || Selected ? palette.Accent : palette.Text;
        if (Icon == IconKind.Close && hovered && Enabled) { fill = Color.FromArgb(196, 44, 64); color = Color.White; }
        var rect = new RectangleF(1, 1, Width - 3, Height - 3);
        if (rect.Width < 1 || rect.Height < 1) return;
        using var shape = VectorIcon.Rounded(rect, DesignTokens.KeyRadius * DeviceDpi / 96f);
        using var brush = new SolidBrush(fill); g.FillPath(brush, shape);
        if (!palette.Dark && Tone is ButtonTone.Number && !Selected)
        { using var edge = new Pen(palette.Border, .7f); g.DrawPath(edge, shape); }
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(accent ? palette.AccentText : palette.Accent, Focused ? 2 : 1); g.DrawPath(pen, shape);
        }
        if (Selected) { using var line = new Pen(palette.Accent, 2 * DeviceDpi / 96f); g.DrawLine(line, rect.Left + 12, rect.Bottom - 1, rect.Right - 12, rect.Bottom - 1); }
        if (Text is "+" or "−" or "×" or "÷" or "=") { DrawOperator(g, Text[0], color); return; }
        var textRect = ClientRectangle;
        if (Icon != IconKind.None)
        {
            var iconSize = (VerticalIcon ? 22 : 20) * DeviceDpi / 96f;
            var center = Text.Length == 0 ? Width / 2f : VerticalIcon ? Width / 2f : 22 * DeviceDpi / 96f;
            var top = VerticalIcon ? Height / 2f - iconSize + 1 : (Height - iconSize) / 2;
            VectorIcon.Draw(g, Icon, new(center - iconSize / 2, top, iconSize, iconSize), color);
            if (Text.Length != 0)
                textRect = VerticalIcon ? new(0, Height / 2 + 7 * DeviceDpi / 96, Width, Height / 2 - 7 * DeviceDpi / 96)
                    : new(40 * DeviceDpi / 96, 0, Width - 44 * DeviceDpi / 96, Height);
        }
        TextRenderer.DrawText(g, Text, Font, textRect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
    // Draw arithmetic symbols as vectors so their appearance does not depend on the font.
    private void DrawOperator(Graphics graphics, char symbol, Color color)
    {
        var scale = DeviceDpi / 96f; var x = Width / 2f; var y = Height / 2f;
        var half = Math.Min(9 * scale, Math.Min(Width, Height) * .18f);
        using var pen = new Pen(color, 1.9f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (symbol == '×')
        { var arm = half * .8f; graphics.DrawLine(pen, x - arm, y - arm, x + arm, y + arm); graphics.DrawLine(pen, x - arm, y + arm, x + arm, y - arm); }
        else if (symbol == '=')
        { graphics.DrawLine(pen, x - half, y - 3 * scale, x + half, y - 3 * scale); graphics.DrawLine(pen, x - half, y + 3 * scale, x + half, y + 3 * scale); }
        else
        {
            graphics.DrawLine(pen, x - half, y, x + half, y);
            if (symbol == '+') graphics.DrawLine(pen, x, y - half, x, y + half);
            if (symbol == '÷') { using var dot = new SolidBrush(color); var r = 1.5f * scale; graphics.FillEllipse(dot, x - r, y - 7 * scale - r, r * 2, r * 2); graphics.FillEllipse(dot, x - r, y + 7 * scale - r, r * 2, r * 2); }
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) transition.Dispose(); base.Dispose(disposing); }
    private static Color Mix(Color first, Color second, float amount) => Color.FromArgb(
        (int)(first.R * (1 - amount) + second.R * amount), (int)(first.G * (1 - amount) + second.G * amount), (int)(first.B * (1 - amount) + second.B * amount));
}
