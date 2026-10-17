// Developer ::> Gehan Fernando
using System.ComponentModel;
using System.Globalization;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Controls;

/// <summary>Direct ISO date entry with explicit Today and inline-calendar actions.</summary>
public sealed class DateInput : UserControl, IThemedControl
{
    private const string DateHelp = "Type a Gregorian date, for example 2026-10-16. Years 0001 through 9999 are supported.";
    private readonly Label label;
    private readonly TextBox editor;
    private readonly TableLayoutPanel layout;
    private Palette palette = Palette.Resolve(Infrastructure.Settings.ThemePreference.Light);
    public event EventHandler? DateChanged;
    public event EventHandler? CalendarRequested;
    [Browsable(false)] public TextBox Editor => editor;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string DateText { get => editor.Text; set => editor.Text = value; }
    public DateInput(string name, DateOnly initial)
    {
        SuspendLayout(); AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        AccessibleName = name; AccessibleRole = AccessibleRole.Grouping; Dock = DockStyle.Fill; Margin = new(3);
        layout = Ui.Table(1, 20, -100);
        label = Ui.Label(name + " · YYYY-MM-DD", 9, true); layout.Controls.Add(label, 0, 0);
        var fields = Ui.Table(3, -100); fields.ColumnStyles.Clear();
        fields.ColumnStyles.Add(new(SizeType.Percent, 100)); fields.ColumnStyles.Add(new(SizeType.Absolute, 30)); fields.ColumnStyles.Add(new(SizeType.Absolute, 48));
        editor = Ui.Input(name + " in YYYY-MM-DD format", initial.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        editor.BorderStyle = BorderStyle.None; editor.Margin = new(4, 4, 4, 2); editor.MaxLength = 10; editor.PlaceholderText = "YYYY-MM-DD"; Ui.SetFont(editor, 11, family: "Consolas");
        editor.AccessibleDescription = DateHelp;
        var calendar = Ui.Button("", () => CalendarRequested?.Invoke(this, EventArgs.Empty), ButtonTone.Normal, IconKind.Calendar, "Choose " + name + " in inline calendar");
        calendar.AccessibleDescription = "Opens an inline month calendar. Direct keyboard entry remains available.";
        var today = Ui.Button("Today", () => SetDate(DateOnly.FromDateTime(DateTime.Today)), ButtonTone.Normal, name: "Set " + name + " to today");
        Ui.SetFont(today, 9); calendar.Margin = today.Margin = new(1);
        fields.Controls.Add(editor, 0, 0); fields.Controls.Add(calendar, 1, 0); fields.Controls.Add(today, 2, 0);
        layout.Controls.Add(fields, 0, 1); Controls.Add(layout);
        editor.TextChanged += (_, _) => { UpdateValidation(); DateChanged?.Invoke(this, EventArgs.Empty); Invalidate(); };
        editor.Enter += (_, _) => Invalidate(); editor.Leave += (_, _) => Invalidate();
        editor.KeyDown += (_, e) => { if (e.Alt && e.KeyCode == Keys.Down) { CalendarRequested?.Invoke(this, EventArgs.Empty); e.Handled = e.SuppressKeyPress = true; } };
        ResumeLayout(true);
    }
    // Parse ISO dates independently of the region's default calendar.
    public bool TryGetDate(out DateOnly date) => DateOnly.TryParseExact(editor.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private string ValidationMessage => $"{AccessibleName}: enter a valid date in YYYY-MM-DD format (years 0001–9999).";
    private void UpdateValidation() => editor.AccessibleDescription = TryGetDate(out _) ? DateHelp : ValidationMessage;
    public DateOnly ReadDate() => TryGetDate(out var date) ? date : throw new FormatException(ValidationMessage);
    public void SetDate(DateOnly date) => editor.Text = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public void FocusInput() { editor.Focus(); editor.SelectAll(); }
    public void AdaptCompact(bool compact)
    {
        layout.RowStyles[0].Height = (compact ? 16 : 20) * DeviceDpi / 96f;
        Ui.SetFont(label, compact ? 8 : 9, FontStyle.Bold);
        Ui.SetFont(editor, compact ? 10 : 11, family: "Consolas");
        editor.Margin = new(4, (compact ? 2 : 4) * DeviceDpi / 96, 4, 2 * DeviceDpi / 96);
    }
    public void ApplyTheme(Palette theme) { palette = theme; BackColor = theme.Surface; label.ForeColor = theme.Muted; editor.BackColor = theme.Key; editor.ForeColor = theme.Text; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using var pen = new Pen(!TryGetDate(out _) ? palette.Error : editor.Focused ? palette.Accent : palette.Border);
        if (Width > 1 && Height > 1) e.Graphics.DrawLine(pen, 0, Height - 1, Width - 1, Height - 1);
    }
}
