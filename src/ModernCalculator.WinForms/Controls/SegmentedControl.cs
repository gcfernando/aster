using System.ComponentModel;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Controls;

/// <summary>Native radio-button semantics with visible flat choices and arrow-key selection.</summary>
public sealed class SegmentedControl : UserControl, IThemedControl
{
    private readonly List<Segment> segments = [];
    private int selectedIndex;
    private bool updating;
    [DefaultValue(0)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < 0 || value >= segments.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (selectedIndex == value && segments[value].Checked) return;
            selectedIndex = value; updating = true;
            for (var i = 0; i < segments.Count; i++) { segments[i].Checked = i == value; segments[i].TabStop = i == value; segments[i].Invalidate(); }
            updating = false; SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? SelectedIndexChanged;
    public SegmentedControl(string accessibleName, IEnumerable<string> labels, IReadOnlyList<IconKind>? icons = null)
    {
        SuspendLayout();
        AccessibleName = accessibleName; AccessibleRole = AccessibleRole.Grouping;
        Dock = DockStyle.Fill; Margin = new(3); Padding = Padding.Empty;
        AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        var items = labels.ToArray();
        if (items.Length == 0) throw new ArgumentException("At least one choice is required.", nameof(labels));
        var layout = Ui.Table(items.Length, -100);
        for (var index = 0; index < items.Length; index++)
        {
            var optionIndex = index;
            var segment = new Segment { Text = items[index], AccessibleName = accessibleName + ": " + items[index], Checked = index == 0, TabStop = index == 0, Icon = icons is not null && index < icons.Count ? icons[index] : IconKind.None };
            segment.CheckedChanged += (_, _) => { if (segment.Checked && !updating) SelectedIndex = optionIndex; };
            segment.KeyDown += (_, e) =>
            {
                var next = e.KeyCode switch { Keys.Left or Keys.Up => (SelectedIndex + segments.Count - 1) % segments.Count,
                    Keys.Right or Keys.Down => (SelectedIndex + 1) % segments.Count, Keys.Home => 0, Keys.End => segments.Count - 1, _ => -1 };
                if (next < 0) return;
                e.Handled = e.SuppressKeyPress = true; SelectedIndex = next; segments[next].Focus();
            };
            segments.Add(segment); layout.Controls.Add(segment, index, 0);
        }
        Controls.Add(layout);
        ResumeLayout(true);
    }
    public void ApplyTheme(Palette palette) { BackColor = palette.Key; }
    private sealed class Segment : RadioButton, IThemedControl
    {
        private Palette palette = Palette.Resolve(Infrastructure.Settings.ThemePreference.Light);
        private bool hovered;
        private bool pressed;
        [DefaultValue(IconKind.None)] public IconKind Icon { get; init; }
        public Segment()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            Appearance = Appearance.Button; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Dock = DockStyle.Fill; Margin = Padding.Empty; Cursor = Cursors.Hand; Ui.SetFont(this, 10);
            _ = new FittedText(this, () => Icon != IconKind.None && Height >= 44 * DeviceDpi / 96
                ? Height - 25 * DeviceDpi / 96f : Height);
        }
        public void ApplyTheme(Palette theme) { palette = theme; BackColor = theme.Key; Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(palette.Key);
            var rectangle = Rectangle.Inflate(ClientRectangle, -1, -1);
            if (rectangle.Width <= 0 || rectangle.Height <= 0) return;
            using var shape = VectorIcon.Rounded(rectangle, DesignTokens.KeyRadius * DeviceDpi / 96f);
            var selectedFill = Color.FromArgb((palette.Surface.R * 8 + palette.Accent.R * 2) / 10, (palette.Surface.G * 8 + palette.Accent.G * 2) / 10, (palette.Surface.B * 8 + palette.Accent.B * 2) / 10);
            if (Checked || hovered || pressed) { using var fill = new SolidBrush(pressed ? palette.Border : Checked ? selectedFill : palette.NumberKey); e.Graphics.FillPath(fill, shape); }
            if (Focused && ShowFocusCues) { using var focus = new Pen(palette.Accent, 1.5f); e.Graphics.DrawPath(focus, shape); }
            var foreground = Enabled ? Checked ? palette.Text : palette.Muted : palette.Muted;
            var textRectangle = rectangle;
            if (Icon != IconKind.None && Height >= 44 * DeviceDpi / 96)
            {
                var size = 17 * DeviceDpi / 96f;
                VectorIcon.Draw(e.Graphics, Icon, new((Width - size) / 2, 5 * DeviceDpi / 96f, size, size), foreground);
                textRectangle = new(rectangle.Left, (int)(25 * DeviceDpi / 96f), rectangle.Width, rectangle.Height - (int)(25 * DeviceDpi / 96f));
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, textRectangle, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Checked) { using var line = new Pen(palette.Accent, 2 * DeviceDpi / 96f); e.Graphics.DrawLine(line, rectangle.Left + 10, rectangle.Bottom - 1, rectangle.Right - 10, rectangle.Bottom - 1); }
        }
    }
}
