using ModernCalculator.WinForms.Themes;
using System.Runtime.CompilerServices;

namespace ModernCalculator.WinForms.Controls;

public static class Ui
{
    private static readonly ConditionalWeakTable<Form, ToolTip> dialogTooltips = new();
    public static void AttachTooltips(Control scope)
    {
        if (scope.FindForm() is ITooltipHost host) { host.RefreshTooltips(scope); return; }
        if (scope.FindForm() is not { } form) return;
        var tooltip = dialogTooltips.GetValue(form, owner =>
        {
            var value = new ToolTip { OwnerDraw = true, InitialDelay = 500, AutoPopDelay = 10000 };
            value.Popup += (_, e) => e.ToolTipSize = TextRenderer.MeasureText(value.GetToolTip(e.AssociatedControl), owner.Font, new Size(500, 0), TextFormatFlags.WordBreak) + new Size(16, 10);
            value.Draw += (_, e) => { using var background = new SolidBrush(owner.BackColor); e.Graphics.FillRectangle(background, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, owner.Font, Rectangle.Inflate(e.Bounds, -7, -4), owner.ForeColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix); };
            owner.Disposed += (_, _) => value.Dispose(); return value;
        });
        void Visit(Control control)
        {
            if (control is ButtonBase or TextBoxBase && !string.IsNullOrEmpty(control.AccessibleName))
                tooltip.SetToolTip(control, control.AccessibleName + (string.IsNullOrEmpty(control.AccessibleDescription) ? "" : "\r\n" + control.AccessibleDescription));
            foreach (Control child in control.Controls) Visit(child);
        }
        Visit(scope);
    }
    public static void Announce(Control control, string text, bool error = false)
    {
        if (!control.IsHandleCreated || !control.Visible) return;
        _ = control.AccessibilityObject.RaiseAutomationNotification(error
            ? System.Windows.Forms.Automation.AutomationNotificationKind.ActionAborted : System.Windows.Forms.Automation.AutomationNotificationKind.ActionCompleted,
            System.Windows.Forms.Automation.AutomationNotificationProcessing.MostRecent, text);
    }
    private static readonly ConditionalWeakTable<Control, FontCache> fonts = new();
    public static Label Label(string text, float size = 10, bool bold = false)
    {
        var label = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false, UseMnemonic = false, Margin = new(4, 0, 4, 0) };
        SetFont(label, size, bold ? FontStyle.Bold : FontStyle.Regular); return label;
    }
    public static void SetFont(Control control, float size, FontStyle style = FontStyle.Regular, string family = "Segoe UI")
    {
        var font = fonts.GetValue(control, item => new FontCache(item)).Get(family, size, style);
        control.Font = font;
        FittedText.Request(control, font);
    }
    private sealed class FontCache
    {
        private readonly Dictionary<(string Family, float Size, FontStyle Style), Font> values = [];
        public FontCache(Control owner) { owner.Disposed += (_, _) => { foreach (var font in values.Values) font.Dispose(); values.Clear(); }; }
        public Font Get(string family, float size, FontStyle style)
        {
            var key = (family, size, style);
            if (!values.TryGetValue(key, out var font)) values.Add(key, font = new Font(family, size, style));
            return font;
        }
    }
    public static CalcButton Button(string text, Action action, ButtonTone tone = ButtonTone.Normal, IconKind icon = IconKind.None, string? name = null)
    {
        var button = new CalcButton { Text = text, Tone = tone, Icon = icon, AccessibleName = name ?? text };
        button.Click += (_, _) => action(); return button;
    }
    public static TableLayoutPanel Table(int columns, params float[] rows)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows.Length, Margin = Padding.Empty, Padding = Padding.Empty };
        for (var i = 0; i < columns; i++) panel.ColumnStyles.Add(new(SizeType.Percent, 100f / columns));
        foreach (var row in rows) panel.RowStyles.Add(new(row < 0 ? SizeType.Percent : SizeType.Absolute, Math.Abs(row)));
        return panel;
    }
    public static TextBox Input(string name, string text = "")
    {
        var input = new TextBox { AccessibleName = name, Text = text, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
            Margin = new(5, 8, 5, 5), MaxLength = 2048 };
        SetFont(input, 14); return input;
    }
    public static void Theme(Control control, Palette palette) => ThemePainter.Apply(control, palette);
}

public interface ITooltipHost { void RefreshTooltips(Control scope); }
