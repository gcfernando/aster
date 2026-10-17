using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ModernCalculator.Core.Conversions;
using ModernCalculator.Core.Expressions;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Forms;
using ModernCalculator.WinForms.Themes;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

public static class CommercialUiChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Commercial UI: Scientific rejects oversized edits atomically and preserves undo", ScientificLength);
        check("Commercial UI: date validation identifies fields and recovers accessible help", DateValidation);
        check("Commercial UI: malformed Unicode searches and supplementary letters remain safe", UnicodeSearch);
        check("Commercial UI: compact unit selectors expose selection and preserve queries", CompactSelection);
        check("Commercial UI: bulk clear confirms and cancels on collection or panel changes", DrawerConfirmation);
        check("Commercial UI: drawer retains cards and updates current action targets", DrawerRetention);
        check("Commercial UI: support summary is local, accurate and privacy allowlisted", SupportSummary);
        check("Commercial UI: support Help pages fit compact and normal layouts", SupportPages);
    }

    private static void ScientificLength()
    {
        var context = new WorkspaceContext(); var messages = new List<string>(); context.StatusChanged += messages.Add;
        using var view = new ScientificView(context); using var host = Host(view, new(650, 620));
        var editor = Named<TextBox>(view, "Scientific expression");
        foreach (var name in new[] { "Square root", "x²", "Factorial", "Root with chosen degree", "Enter scientific exponent", "+/−" })
        {
            editor.Text = string.Concat(Enumerable.Repeat("1+", 1023)) + "11"; editor.SelectAll(); editor.ClearUndo();
            var original = editor.Text; var start = editor.SelectionStart; var length = editor.SelectionLength;
            Click(Named<CalcButton>(view, name));
            Require(editor.Text == original && editor.SelectionStart == start && editor.SelectionLength == length && !editor.CanUndo,
                "Rejected " + name + " changed input, selection or undo.");
            Require(messages[^1].Contains("no input was changed", StringComparison.Ordinal), "Length rejection was hidden by an unrelated hint.");
        }
        editor.Text = new string('1', ExpressionEvaluator.MaximumLength - 1); editor.Select(editor.TextLength, 0); editor.ClearUndo();
        Click(Named<CalcButton>(view, "7")); Require(editor.TextLength == ExpressionEvaluator.MaximumLength && editor.CanUndo, "Boundary insertion failed.");
        var full = editor.Text; Click(Named<CalcButton>(view, "7"));
        Require(editor.Text == full && editor.CanUndo, "Rejected insertion discarded a valid undo operation.");
        editor.Undo(); Require(editor.TextLength == ExpressionEvaluator.MaximumLength - 1, "Undo after rejection did not restore the accepted edit.");
        editor.Text = new string('1', ExpressionEvaluator.MaximumLength - 6); editor.SelectAll(); Click(Named<CalcButton>(view, "Square root"));
        Require(editor.TextLength == ExpressionEvaluator.MaximumLength && editor.Text.EndsWith(')'), "Exact-limit wrapping was truncated.");
        editor.Text = new string('1', ExpressionEvaluator.MaximumLength); editor.Select(editor.TextLength, 0);
        Click(Named<CalcButton>(view, "Enter scientific exponent"));
        Require(editor.TextLength == ExpressionEvaluator.MaximumLength && editor.SelectionStart == editor.TextLength && editor.SelectionLength == 0,
            "Rejected implicit EXP operand changed the original caret.");
    }

    private static void DateValidation()
    {
        foreach (var name in new[] { "Start date", "End date", "Starting date" })
        {
            using var input = new DateInput(name, new(2026, 10, 17));
            input.DateText = "2026-02-31";
            try { input.ReadDate(); throw new InvalidOperationException("Invalid date accepted."); }
            catch (FormatException ex) { Require(ex.Message.StartsWith(name, StringComparison.Ordinal), "Date error omitted its field name."); }
            Require(input.Editor.AccessibleDescription?.StartsWith(name, StringComparison.Ordinal) == true, "Invalid editor retained ordinary help.");
            foreach (var date in new[] { DateOnly.MinValue, DateOnly.MaxValue, new DateOnly(2026, 2, 28) })
            {
                input.SetDate(date); Require(input.ReadDate() == date, "ISO boundary changed.");
                Require(input.Editor.AccessibleDescription?.StartsWith("Type a Gregorian", StringComparison.Ordinal) == true, "Corrected date retained its error.");
            }
        }
    }

    private static void UnicodeSearch()
    {
        Require(UnitSearch.Normalize("\ud800metres\udc00") == "metres", "Lone surrogate repair changed searchable words.");
        Require(UnitSearch.Normalize("\U00010428") == "\U00010428", "A valid supplementary letter was discarded.");
        Require(UnitSearch.Normalize("MÉTRES ²") == "metres 2", "Accent/superscript normalization changed.");
        using var browser = new ConverterChoiceBrowser("Search probe", "Search"); using var host = Host(browser, new(320, 180));
        var chosen = "";
        browser.SetEntries([new("length", "Metre", "metre metres m", () => chosen = "metre", "m"), new("mass", "Gram", "gram grams g", () => chosen = "gram", "g")]);
        var search = Descendants(browser).OfType<TextBox>().Single(); search.Text = "\ud800metre";
        InvokeControl(search, "OnKeyDown", new KeyEventArgs(Keys.Enter)); Require(chosen == "metre", "Malformed pasted query broke keyboard activation.");
        browser.SetEntries([new("mass", "Gram", "gram grams g", () => chosen = "new gram", "g")], true);
        search.Text = "g"; InvokeControl(search, "OnKeyDown", new KeyEventArgs(Keys.Enter)); Require(chosen == "new gram", "Search cache retained obsolete choices/actions.");
    }

    private static void CompactSelection()
    {
        using var selector = new SearchableUnitSelector("Probe unit"); using var host = Host(selector, new(280, 140));
        foreach (var category in UnitConverter.Categories)
        {
            var index = Math.Min(1, category.Units.Count - 1); selector.AdaptCompact(true); selector.SetUnits(category.Units, index);
            var browser = Descendants(selector).OfType<ConverterChoiceBrowser>().Single();
            Require(Field<CalcButton[]>(browser, "buttons")[0].Selected, "Compact startup did not reveal selected unit in " + category.Name);
            selector.AdaptCompact(false); selector.AdaptCompact(true);
            Require(Field<CalcButton[]>(browser, "buttons")[0].Selected, "Density changes lost selected-unit visibility.");
            var search = Descendants(browser).OfType<TextBox>().Single(); search.Text = "no matches";
            selector.AdaptCompact(false); selector.AdaptCompact(true); Require(search.Text == "no matches" && selector.SelectedIndex == index, "Density change cleared query or changed units.");
        }
    }

    private static void DrawerConfirmation() => WithMain((form, context) =>
    {
        context.Record("Standard", "1+2", "3"); context.Memory.Store(5m); context.NotifyMemory(); Invoke(form, "SetDrawer", true);
        var clear = Field<CalcButton>(form, "clearJournal"); var tabs = Field<SegmentedControl>(form, "drawerTabs");
        Click(clear); Require(context.History.Items.Count == 1 && clear.AccessibleName == "Confirm clear history", "First bulk activation deleted history.");
        context.Record("Standard", "2+3", "5"); Require(clear.AccessibleName == "Clear history", "New data did not cancel confirmation.");
        Click(clear); tabs.SelectedIndex = 1; Require(clear.AccessibleName == "Clear memory" && context.History.Items.Count == 2, "Switching collections retained confirmation.");
        Click(clear); Invoke(form, "SetDrawer", false); Invoke(form, "SetDrawer", true); Require(clear.AccessibleName == "Clear memory", "Drawer close retained confirmation.");
        Click(clear); Require(context.Memory.Items.Count == 1, "First memory clear activation deleted data.");
        Click(clear); Require(context.Memory.Items.Count == 0 && context.History.Items.Count == 2, "Confirmed clear targeted the wrong collection.");
        tabs.SelectedIndex = 0; Click(clear); Click(clear); Require(context.History.Items.Count == 0, "Confirmed history clear did not complete.");
    });

    private static void DrawerRetention() => WithMain((form, context) =>
    {
        context.Record("Standard", "1+2", "3"); Invoke(form, "SetDrawer", true);
        var list = Field<Panel>(form, "journalList"); var card = list.Controls[0]; var display = Descendants(card).OfType<ResultDisplay>().Single();
        Invoke(form, "RefreshDrawer"); Require(ReferenceEquals(card, list.Controls[0]) && !card.IsDisposed, "Unchanged card was recreated.");
        context.Record("Standard", "2+3", "5");
        Require(ReferenceEquals(card, list.Controls[0]) && display.FullValue == "5", "New result rebuilt rather than updated the card.");
        Click(Named<CalcButton>(card, "Delete this item"));
        Require(context.History.Items.Count == 1 && context.History.Items[0].Result == "3" && display.FullValue == "3", "Retained action deleted an obsolete item.");
    });

    private static void SupportSummary()
    {
        var settings = new AppSettings { Mode = "Scientific", Theme = ThemePreference.Dark };
        var text = SupportInfo.Capture(settings, Palette.Resolve(settings.Theme), 144);
        Require(text.Contains("Version: " + SupportInfo.Version, StringComparison.Ordinal) && text.Contains("Build: " + SupportInfo.Build, StringComparison.Ordinal) &&
            text.Contains("DPI: 144", StringComparison.Ordinal) && text.Contains("Mode: Scientific", StringComparison.Ordinal), "Support metadata is inaccurate.");
        var keys = text.Split(Environment.NewLine).Where(line => line.Contains(':')).Select(line => line[..line.IndexOf(':')]).ToArray();
        Require(keys.SequenceEqual(new[] { "Product", "Version", "Build", "Runtime", "OS", "Architecture", "Culture", "Mode", "Theme", "Theme preference", "DPI", "Display digits", "Density" }), "Support data expanded beyond its privacy allowlist.");
        var copied = ""; SupportInfo.Copy(text, value => copied = value); Require(copied == text, "Copy altered support data.");
        Require(SupportInfo.Copy(text, _ => throw new ExternalException()).Contains("busy", StringComparison.Ordinal), "Busy clipboard was not handled.");
        var opened = ""; SupportInfo.OpenIssueTracker(value => opened = value); Require(opened == SupportInfo.IssueTracker, "Support link used another destination.");
    }

    private static void SupportPages()
    {
        foreach (var compact in new[] { false, true })
        foreach (var theme in new[] { ThemePreference.Light, ThemePreference.Dark })
        {
            using var help = new HelpForm(new AppSettings(), Palette.Resolve(theme));
            _ = help.Handle; help.ClientSize = compact ? new(620 * help.DeviceDpi / 96, 340 * help.DeviceDpi / 96) : new(620 * help.DeviceDpi / 96, 520 * help.DeviceDpi / 96);
            Named<SegmentedControl>(help, "Help topic").SelectedIndex = 4; Layout(help); Handles(help); Layout(help);
            for (var page = 0; page < 8; page++)
            {
                foreach (var label in Descendants(Field<TableLayoutPanel>(help, "body")).OfType<Label>())
                {
                    var measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.ClientSize.Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
                    Require(measured.Height <= label.Height, "Support help text is clipped: " + label.Text);
                }
                var next = Named<CalcButton>(help, "Next help page"); if (!next.Enabled) break; Click(next); Layout(help);
            }
            Require(Named<CalcButton>(help, "Copy private support information").Enabled && !help.Visible, "Support controls missing or test showed a window.");
        }
    }

    private static void WithMain(Action<MainForm, WorkspaceContext> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterCommercialUi-" + Guid.NewGuid().ToString("N"));
        try { using var form = new MainForm(new StateStore(directory)); _ = form.Handle; action(form, Field<WorkspaceContext>(form, "context")); Require(!form.Visible, "Hidden check showed a window."); }
        finally
        {
            var resolved = Path.GetFullPath(directory); var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            Require(Path.GetDirectoryName(resolved) == temporary && Path.GetFileName(resolved).StartsWith("AsterCommercialUi-", StringComparison.Ordinal), "Unexpected temporary test path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
    private static Form Host(Control content, Size size) { var host = new Form { ClientSize = size, AutoScaleMode = AutoScaleMode.None }; host.Controls.Add(content); _ = host.Handle; Layout(host); Handles(host); Layout(host); return host; }
    private static void Layout(Control control) { control.PerformLayout(); foreach (Control child in control.Controls) Layout(child); }
    private static void Handles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) Handles(child); }
    private static void Click(CalcButton button) { Require(button.Enabled, "Disabled test control."); InvokeControl(button, "OnClick", EventArgs.Empty); }
    private static object? Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static void InvokeControl(Control control, string name, object argument) => typeof(Control).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [argument]);
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static T Named<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().Single(control => control.AccessibleName == name);
    private static IEnumerable<Control> Descendants(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
