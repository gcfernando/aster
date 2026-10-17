// Developer ::> Gehan Fernando
using System.ComponentModel;
using System.Globalization;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Controls;

/// <summary>Integer editor with flat, keyboard-accessible decrement/increment targets.</summary>
public sealed class FlatNumberInput : UserControl, IThemedControl
{
    private readonly TextBox editor = new() { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center,
        Dock = DockStyle.Fill, Margin = new(3), MaxLength = 12 };
    private readonly CalcButton decrease;
    private readonly CalcButton increase;
    private Palette palette = Palette.Resolve(Infrastructure.Settings.ThemePreference.Light);
    private int minimum;
    private int maximum = 9999;
    private int value;
    private bool updating;
    public event EventHandler? ValueChanged;
    public event EventHandler? InputChanged;
    [Browsable(false)] public TextBox Editor => editor;
    [DefaultValue(0)]
    public int Minimum { get => minimum; set { if (value > maximum) throw new ArgumentOutOfRangeException(nameof(value)); minimum = value; Value = Math.Clamp(this.value, minimum, maximum); UpdateNames(); } }
    [DefaultValue(9999)]
    public int Maximum { get => maximum; set { if (value < minimum) throw new ArgumentOutOfRangeException(nameof(value)); maximum = value; Value = Math.Clamp(this.value, minimum, maximum); UpdateNames(); } }
    [DefaultValue(0)]
    public int Value
    {
        get => value;
        set
        {
            if (value < minimum || value > maximum) throw new ArgumentOutOfRangeException(nameof(value));
            var changed = this.value != value;
            this.value = value; updating = true;
            editor.Text = value.ToString(CultureInfo.InvariantCulture); updating = false; UpdateButtons();
            if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
            InputChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public FlatNumberInput()
    {
        SuspendLayout(); AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Dock = DockStyle.Fill; Margin = new(3); Padding = new(1); AccessibleRole = AccessibleRole.SpinButton;
        var layout = Ui.Table(3, -100); layout.ColumnStyles.Clear();
        layout.ColumnStyles.Add(new(SizeType.Absolute, 30)); layout.ColumnStyles.Add(new(SizeType.Percent, 100)); layout.ColumnStyles.Add(new(SizeType.Absolute, 30));
        decrease = Ui.Button("−", () => Step(-1), ButtonTone.Normal, name: "Decrease value");
        increase = Ui.Button("+", () => Step(1), ButtonTone.Normal, name: "Increase value");
        decrease.Margin = increase.Margin = Padding.Empty;
        Ui.SetFont(editor, 11); editor.Text = "0";
        var editorHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        editorHost.Controls.Add(editor);
        void PositionEditor() { editor.Dock = DockStyle.None; editor.Bounds = new(3, Math.Max(0, (editorHost.Height - editor.PreferredHeight) / 2), Math.Max(1, editorHost.Width - 6), editor.PreferredHeight); }
        editorHost.SizeChanged += (_, _) => PositionEditor(); editor.FontChanged += (_, _) => PositionEditor();
        layout.Controls.Add(decrease, 0, 0); layout.Controls.Add(editorHost, 1, 0); layout.Controls.Add(increase, 2, 0); Controls.Add(layout);
        editor.TextChanged += (_, _) =>
        {
            if (updating) return;
            // Keep incomplete editor text without replacing the last valid numeric value.
            if (TryGetValue(out var entered)) { var changed = value != entered; value = entered; if (changed) ValueChanged?.Invoke(this, EventArgs.Empty); }
            UpdateButtons(); InputChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        };
        editor.PreviewKeyDown += (_, e) => { if (e.KeyCode is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown) e.IsInputKey = true; };
        editor.KeyDown += (_, e) =>
        {
            var amount = e.KeyCode switch { Keys.Up => 1, Keys.Down => -1, Keys.PageUp => 10, Keys.PageDown => -10, _ => 0 };
            if (amount != 0) { Step(amount); e.Handled = e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode is Keys.Home or Keys.End) { Value = e.KeyCode == Keys.Home ? minimum : maximum; editor.SelectAll(); e.Handled = e.SuppressKeyPress = true; }
        };
        editor.Enter += (_, _) => Invalidate(); editor.Leave += (_, _) => Invalidate();
        UpdateNames(); UpdateButtons(); ResumeLayout(true);
    }
    protected override void OnCreateControl() { base.OnCreateControl(); UpdateNames(); }
    public bool TryGetValue(out int entered) => int.TryParse(editor.Text.Trim(), NumberStyles.AllowLeadingSign,
        CultureInfo.InvariantCulture, out entered) && entered >= minimum && entered <= maximum;
    public bool TryCommit(out string error)
    {
        if (!TryGetValue(out var entered)) { error = $"Enter a whole number from {minimum} to {maximum}."; editor.AccessibleDescription = error; Invalidate(); return false; }
        Value = entered; error = ""; return true;
    }
    public void FocusInput() { editor.Focus(); editor.SelectAll(); }
    private void Step(int amount)
    {
        if (!TryGetValue(out var entered)) { editor.Focus(); editor.SelectAll(); return; }
        Value = (int)Math.Clamp((long)entered + amount, minimum, maximum); editor.Focus(); editor.SelectAll();
    }
    private void UpdateButtons()
    {
        var valid = TryGetValue(out var entered);
        decrease.Enabled = valid && entered > minimum; increase.Enabled = valid && entered < maximum;
        // A corrected edit or assigned value must not keep a previous commit error.
        if (valid) UpdateNames();
    }
    private void UpdateNames()
    {
        if (decrease is null) return;
        var name = AccessibleName ?? "Value";
        editor.AccessibleName = name + " integer"; editor.AccessibleDescription = $"Whole number from {minimum} to {maximum}. Up/Down changes by one; Page Up/Down by ten.";
        decrease.AccessibleName = "Decrease " + name; increase.AccessibleName = "Increase " + name;
        AccessibleDescription = editor.AccessibleDescription;
    }
    public void ApplyTheme(Palette theme) { palette = theme; BackColor = editor.BackColor = theme.Key; editor.ForeColor = theme.Text; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using var pen = new Pen(!TryGetValue(out _) ? palette.Error : ContainsFocus ? palette.Accent : palette.Border);
        if (Width > 1 && Height > 1) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
