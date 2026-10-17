// Developer ::> Gehan Fernando
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;
using System.ComponentModel;

namespace ModernCalculator.WinForms.Forms;

public sealed class SettingsForm : BorderlessForm
{
    protected override bool ResizableFrame => false;
    private readonly SegmentedControl theme;
    private readonly FlatNumberInput precision;
    private readonly FlatCheckBox grouping;
    private readonly FlatCheckBox history;
    private readonly FlatCheckBox top;
    private readonly FlatCheckBox reducedMotion;
    private readonly FlatCheckBox highContrast;
    private readonly SegmentedControl density;
    private readonly SegmentedControl shortcuts;
    private readonly Panel content = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly List<Control> pages = [];
    private readonly CalcButton clearHistory;
    private readonly Label clearNote = Ui.Label("Clearing removes expressions, results, modes and timestamps. Saved unit pairs are kept.", 9);
    private bool confirmingClear;
    public AppSettings Result { get; private set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool ClearHistoryRequested { get; private set; }

    public SettingsForm(AppSettings settings, Palette palette)
    {
        SuspendLayout(); Result = settings;
        Text = "Aster · Preferences"; Ui.SetFont(this, 10);
        AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new(570, 500); MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; KeyPreview = true;
        var layout = Ui.Table(1, 42, 40, -100, 38); layout.Padding = new(14, 6, 14, 10);
        var heading = Ui.Table(3, -100); heading.ColumnStyles[0].Width = 80; heading.ColumnStyles[1].Width = 10; heading.ColumnStyles[2].Width = 10;
        var title = Ui.Label("Preferences", 14, true); RegisterDragSurface(title); RegisterDragSurface(heading);
        heading.Controls.Add(title, 0, 0);
        heading.Controls.Add(Ui.Button("?", ShowHelp, ButtonTone.Quiet, name: "Help and keyboard shortcuts (F1)"), 1, 0);
        heading.Controls.Add(Ui.Button("", () => { DialogResult = DialogResult.Cancel; Close(); }, ButtonTone.Quiet, IconKind.Close, "Close preferences"), 2, 0);
        layout.Controls.Add(heading, 0, 0);
        var sections = new SegmentedControl("Preferences section", ["Appearance", "Numbers", "History", "Window"]);
        layout.Controls.Add(sections, 0, 1); layout.Controls.Add(content, 0, 2);
        theme = new SegmentedControl("Theme preference", ["System", "Dark", "Light"]) { SelectedIndex = (int)settings.Theme };
        density = new SegmentedControl("Layout density", ["Automatic", "Compact"]) { SelectedIndex = (int)settings.Density };
        shortcuts = new SegmentedControl("Mode shortcut preference", ["Ctrl + 1–5", "Ctrl + Alt + 1–5"]) { SelectedIndex = (int)settings.ModeShortcuts };
        grouping = Check("Group thousands", settings.GroupDigits);
        history = Check("Save history between sessions", settings.PersistHistory);
        top = Check("Keep Aster above other windows", settings.AlwaysOnTop);
        reducedMotion = Check("Reduce hover motion", settings.ReducedMotion);
        highContrast = Check("Use high-contrast system colors", settings.HighContrast);
        precision = new FlatNumberInput { AccessibleName = "Scientific significant display digits", Minimum = 3, Maximum = 15, Value = settings.Precision };
        var example = Ui.Label("", 9); example.AccessibleName = "Display precision feedback";
        void UpdateExample()
        {
            var valid = precision.TryGetValue(out var digits);
            example.Text = valid ? "Example: 1 ÷ 3 → " + (1d / 3).ToString("G" + digits, System.Globalization.CultureInfo.CurrentCulture)
                : "Enter a whole number from 3 to 15.";
            example.AccessibleDescription = example.Text;
            example.ForeColor = valid ? palette.Muted : palette.Error;
        }
        precision.InputChanged += (_, _) => UpdateExample(); UpdateExample();
        var appearance = Page(6);
        appearance.Controls.Add(Ui.Label("Appearance follows Windows when System is selected.", 9), 0, 0);
        appearance.Controls.Add(theme, 0, 1); appearance.Controls.Add(density, 0, 2);
        appearance.Controls.Add(reducedMotion, 0, 3); appearance.Controls.Add(highContrast, 0, 4);
        appearance.Controls.Add(Ui.Label("Automatic density adapts to space. Compact prefers a smaller normal window.", 9), 0, 5);
        pages.Add(appearance);
        var numbers = Page(6);
        numbers.Controls.Add(Ui.Label("Displayed significant digits — Scientific & Converter", 10, true), 0, 0);
        numbers.Controls.Add(precision, 0, 1); numbers.Controls.Add(Ui.Label("Range: 3–15 significant digits; rounded for display only.", 9), 0, 2);
        numbers.Controls.Add(example, 0, 3); numbers.Controls.Add(grouping, 0, 4);
        numbers.Controls.Add(Ui.Label("Standard keeps its full decimal value. Scientific and Converter use approximate binary64 arithmetic; accuracy depends on the calculation.", 9), 0, 5);
        pages.Add(numbers);
        var privacy = Page(5); privacy.Controls.Add(history, 0, 0);
        privacy.Controls.Add(Ui.Label("History stores up to 200 expressions, results, modes and timestamps locally. Turning persistence off removes saved history on Save.", 9), 0, 1);
        clearHistory = Ui.Button("Clear saved history", StageClearHistory, ButtonTone.Normal, IconKind.Trash, "Clear saved history");
        privacy.Controls.Add(clearHistory, 0, 2); privacy.Controls.Add(clearNote, 0, 3);
        privacy.Controls.Add(Ui.Label("Session memory is never saved. Unit pairs and preferences stay on this device.", 9), 0, 4); pages.Add(privacy);
        var window = Page(5); window.Controls.Add(top, 0, 0); window.Controls.Add(Ui.Label("Mode-switch shortcuts", 10, true), 0, 1);
        window.Controls.Add(shortcuts, 0, 2); window.Controls.Add(Ui.Button("Help & keyboard shortcuts", ShowHelp, name: "Open shortcut guide"), 0, 3);
        window.Controls.Add(Ui.Label("Editing keys keep their usual behavior. F1 always opens help. Alt+1–4 selects Programmer bases.", 9), 0, 4); pages.Add(window);
        foreach (var page in pages) { page.Visible = false; content.Controls.Add(page); }
        void SelectPage() { foreach (var page in pages) page.Visible = false; pages[sections.SelectedIndex].Visible = true; pages[sections.SelectedIndex].BringToFront(); }
        sections.SelectedIndexChanged += (_, _) => SelectPage(); SelectPage();
        var actions = Ui.Table(3, -100);
        actions.Controls.Add(Ui.Button("Restore defaults", () =>
        {
            var defaults = new AppSettings(); theme.SelectedIndex = (int)defaults.Theme; density.SelectedIndex = 0; shortcuts.SelectedIndex = 0;
            precision.Value = defaults.Precision; grouping.Checked = defaults.GroupDigits; history.Checked = defaults.PersistHistory;
            top.Checked = reducedMotion.Checked = highContrast.Checked = false; ClearHistoryRequested = confirmingClear = false;
            clearHistory.Text = "Clear saved history"; clearNote.Text = "Defaults are staged. Save applies them; Cancel keeps current preferences.";
        }, ButtonTone.Quiet), 0, 0);
        var cancel = Ui.Button("Cancel", () => { DialogResult = DialogResult.Cancel; Close(); }, ButtonTone.Quiet);
        var save = Ui.Button("Save preferences", () =>
        {
            if (!precision.TryCommit(out var error))
            {
                sections.SelectedIndex = 1; UpdateExample(); precision.FocusInput();
                Ui.Announce(precision.Editor, error, true); return;
            }
            Result = settings with { Theme = (ThemePreference)theme.SelectedIndex, Density = (DensityPreference)density.SelectedIndex,
                ModeShortcuts = (ModeShortcutPreference)shortcuts.SelectedIndex, Precision = precision.Value, GroupDigits = grouping.Checked,
                PersistHistory = history.Checked, AlwaysOnTop = top.Checked, ReducedMotion = reducedMotion.Checked, HighContrast = highContrast.Checked };
            DialogResult = DialogResult.OK; Close();
        }, ButtonTone.Accent);
        actions.Controls.Add(cancel, 1, 0); actions.Controls.Add(save, 2, 0); layout.Controls.Add(actions, 0, 3);
        CancelButton = cancel; AcceptButton = save; Controls.Add(layout); Ui.Theme(this, palette);
        foreach (var label in pages.SelectMany(Descendants).OfType<Label>()) label.ForeColor = palette.Muted;
        UpdateExample();
        ResumeLayout(true); Ui.AttachTooltips(this);
    }
    private static TableLayoutPanel Page(int rows) => Ui.Table(1, Enumerable.Repeat(-1f, rows).ToArray());
    private static IEnumerable<Control> Descendants(Control control) { foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    // Stage history clearing here; the main form applies it only after preferences are saved.
    private void StageClearHistory()
    {
        if (ClearHistoryRequested) { ClearHistoryRequested = confirmingClear = false; clearHistory.Text = "Clear saved history"; clearNote.Text = "History will be kept."; }
        else if (!confirmingClear) { confirmingClear = true; clearHistory.Text = "Confirm clear history"; clearNote.Text = "Confirm, then Save preferences to remove history. Cancel discards this request."; }
        else { ClearHistoryRequested = true; confirmingClear = false; clearHistory.Text = "Keep history"; clearNote.Text = "History clearing is staged. Save clears it; Cancel keeps all history."; }
    }
    private void ShowHelp() { using var help = new HelpForm(Result, Palette.Resolve(Result.Theme, Result.HighContrast)); help.ShowDialog(this); }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1) { ShowHelp(); return true; }
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e); var area = Screen.FromControl(Owner ?? this).WorkingArea; var inset = 16 * DeviceDpi / 96;
        Size = new(Math.Min(Width, area.Width - inset * 2), Math.Min(Height, area.Height - inset * 2));
        Location = new(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }
    private static FlatCheckBox Check(string text, bool value) => new() { Text = text, AccessibleName = text, Checked = value };
}
