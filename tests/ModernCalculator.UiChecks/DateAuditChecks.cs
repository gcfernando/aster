using System.Globalization;
using System.Reflection;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

/// <summary>Managed date UI checks; never shows a form or sends OS input.</summary>
public static class DateAuditChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Dates audit: populated initial result and no implicit history", InitialState);
        check("Dates audit: separate workflows and reusable adjusted dates", SeparateWorkflows);
        check("Dates audit: elapsed/inclusive, identical/reversed and leap dates", DifferenceSemantics);
        check("Dates audit: signed intervals, month ends and invalid input", AdjustmentSemantics);
        check("Dates audit: inline calendar day/month keys, Today and Cancel", CalendarInteractions);
        check("Dates audit: flat integer keyboard entry and validation", FlatIntegerInput);
        check("Dates review: invalid results disable saving and valid edits recover", InvalidSaveState);
        foreach (var culture in new[] { "th-TH", "ar-SA" })
            check("Dates review: Gregorian captions and calendar under " + culture, () => GregorianConsistency(culture));
        foreach (var theme in new[] { ThemePreference.Light, ThemePreference.Dark })
        {
            check("Dates audit: compact inline calendar targets and details " + theme, () => CompactLayout(theme));
            check("Dates audit: 246-DIP workflows and three-week calendar pages " + theme, () => ShortWorkspace(theme));
        }
    }
    private static void InitialState()
    {
        var context = new WorkspaceContext(); using var view = new DateView(context); using var host = Host(view);
        Require(view.CopyText == "30", "Populated start/end dates must initially show thirty elapsed days.");
        Require(Field<ResultDisplay>(view, "differenceDisplay").FullValue == "30 days", "Initial result contradicts populated dates.");
        Require(context.History.Items.Count == 0, "Constructing or formatting the date view must not record history.");
        view.RefreshFormatting(); Require(context.History.Items.Count == 0, "Formatting recorded a history entry.");
    }
    private static void SeparateWorkflows()
    {
        var context = new WorkspaceContext(); using var view = new DateView(context); using var host = Host(view);
        var workflow = Field<SegmentedControl>(view, "workflow");
        Field<DateInput>(view, "first").SetDate(new(2026, 10, 16)); Field<DateInput>(view, "second").SetDate(new(2026, 11, 15));
        workflow.SelectedIndex = 1; view.Reuse("2026-01-31"); Field<FlatNumberInput>(view, "months").Value = 1;
        Require(view.CopyText == "2026-02-28", "Date adjustment did not use its dedicated starting date.");
        Require(Field<DateInput>(view, "first").DateText == "2026-10-16", "Reusing an adjustment date changed the difference input.");
        workflow.SelectedIndex = 0; Require(view.CopyText == "30", "Switching workflows lost the difference result.");
        workflow.SelectedIndex = 1; Require(view.CopyText == "2026-02-28", "Switching workflows lost the adjustment result.");
        Require(context.History.Items.Count == 0, "Live date changes recorded history.");
        Click(Named<CalcButton>(view, "Calculate adjusted date and save it to history"));
        Require(context.History.Items.Count == 1 && context.History.Items[0].Result == "2026-02-28", "Explicit adjustment save failed.");
        Click(Named<CalcButton>(view, "Reset adjustment to today with a zero interval"));
        Require(view.CopyText == DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Adjustment Reset did not restore today.");
    }
    private static void DifferenceSemantics()
    {
        var context = new WorkspaceContext(); using var view = new DateView(context); using var host = Host(view);
        var first = Field<DateInput>(view, "first"); var second = Field<DateInput>(view, "second"); var counting = Field<SegmentedControl>(view, "dayCounting");
        first.SetDate(new(2024, 2, 28)); second.SetDate(new(2024, 3, 1)); Require(view.CopyText == "2", "Leap-day difference is incorrect.");
        Click(Named<CalcButton>(view, "Swap the start and end dates")); Require(view.CopyText == "2", "Reversing dates changed an absolute difference.");
        first.SetDate(new(2024, 2, 29)); second.SetDate(new(2024, 2, 29)); Require(view.CopyText == "0", "Identical dates must have zero elapsed days.");
        counting.SelectedIndex = 1; Require(view.CopyText == "1", "Inclusive identical dates must count once.");
        second.SetDate(new(2024, 3, 1)); Require(view.CopyText == "2", "Inclusive counting must include both endpoints.");
        first.DateText = "2024-02-30"; Require(view.CopyText == "", "Malformed dates left a copyable stale result.");
        Require(Field<ResultDisplay>(view, "differenceDisplay").FullValue.Contains("valid date", StringComparison.OrdinalIgnoreCase), "Malformed date lacks an explanatory result.");
        Click(Named<CalcButton>(view, "Reset date difference to today and thirty days later"));
        Require(view.CopyText == "30" && counting.SelectedIndex == 0, "Difference Reset did not restore elapsed mode and valid dates.");
        Require(context.History.Items.Count == 0, "Live, Swap or Reset actions recorded history.");
        Click(Named<CalcButton>(view, "Save this date difference to history")); Require(context.History.Items.Count == 1, "Explicit difference save failed.");
    }
    private static void AdjustmentSemantics()
    {
        var context = new WorkspaceContext(); using var view = new DateView(context); using var host = Host(view);
        Field<SegmentedControl>(view, "workflow").SelectedIndex = 1;
        var start = Field<DateInput>(view, "starting"); var months = Field<FlatNumberInput>(view, "months"); var days = Field<FlatNumberInput>(view, "days"); var years = Field<FlatNumberInput>(view, "years");
        start.SetDate(new(2024, 1, 31)); months.Value = 1; Require(view.CopyText == "2024-02-29", "January 31 did not clamp to leap-year February 29.");
        months.Value = 0; start.SetDate(new(2024, 2, 29)); years.Value = 1; Require(view.CopyText == "2025-02-28", "Adding a year from leap day did not clamp.");
        years.Value = 0; start.SetDate(new(2026, 3, 15)); days.Value = -1; Require(view.CopyText == "2026-03-14", "Signed negative interval entry was lost.");
        Field<SegmentedControl>(view, "operation").SelectedIndex = 1; Require(view.CopyText == "2026-03-16", "Subtract must reverse a negative entered interval.");
        days.Editor.Text = "invalid"; Require(view.CopyText == "", "Invalid amount recomputed using an old backing Value.");
        Require(Field<ResultDisplay>(view, "adjustmentDisplay").FullValue.Contains("whole number", StringComparison.OrdinalIgnoreCase), "Invalid amount lacks a validation message.");
        days.Value = 0; start.SetDate(new(1, 1, 1)); days.Value = 1; Require(view.CopyText == "", "Date underflow must invalidate the result.");
        Require(context.History.Items.Count == 0, "Live adjustment or its errors recorded history.");
    }
    private static void CalendarInteractions()
    {
        var context = new WorkspaceContext(); using var view = new DateView(context); using var host = Host(view);
        var input = Field<DateInput>(view, "first"); input.SetDate(new(2026, 10, 16));
        Click(Named<CalcButton>(input, "Choose Start date in inline calendar"));
        var calendar = Field<DateCalendarPanel>(view, "calendar"); Require(Field<DateInput?>(view, "calendarTarget") == input, "Calendar did not attach to the selected date input.");
        var current = Descendants(calendar).OfType<CalcButton>().Single(button => button.Text == "16");
        Key(current, Keys.Right); Require(Field<DateOnly>(calendar, "cursor") == new DateOnly(2026, 10, 17), "Right arrow did not move one calendar day.");
        current = Descendants(calendar).OfType<CalcButton>().Single(button => button.Text == "17"); Key(current, Keys.PageDown);
        Require(Field<DateOnly>(calendar, "cursor") == new DateOnly(2026, 11, 17), "Page Down did not move one calendar month.");
        Click(Descendants(calendar).OfType<CalcButton>().Single(button => button.Text == "18"));
        Require(input.DateText == "2026-11-18" && Field<DateInput?>(view, "calendarTarget") is null, "Choosing a day did not close the inline calendar and update its input.");
        Click(Named<CalcButton>(input, "Choose Start date in inline calendar")); Click(Named<CalcButton>(calendar, "Cancel"));
        Require(input.DateText == "2026-11-18", "Cancel changed the selected date.");
        Click(Named<CalcButton>(input, "Choose Start date in inline calendar")); Require(view.Command(Keys.Escape), "Escape did not close the calendar.");
        Click(Named<CalcButton>(input, "Set Start date to today")); Require(input.ReadDate() == DateOnly.FromDateTime(DateTime.Today), "Today shortcut failed.");
        Click(Named<CalcButton>(input, "Choose Start date in inline calendar")); Click(Named<CalcButton>(calendar, "Today"));
        Require(Field<DateInput?>(view, "calendarTarget") is null && input.ReadDate() == DateOnly.FromDateTime(DateTime.Today), "Calendar Today did not choose today and return.");
        Require(context.History.Items.Count == 0, "Calendar navigation or selection recorded history.");
    }
    private static void FlatIntegerInput()
    {
        using var input = new FlatNumberInput { AccessibleName = "Test interval", Minimum = -9999, Maximum = 9999, Value = 0 }; using var host = Host(input);
        Key(input.Editor, Keys.Up); Require(input.Value == 1, "Up did not increment integer entry.");
        Key(input.Editor, Keys.PageDown); Require(input.Value == -9, "Page Down did not decrement by ten.");
        Key(input.Editor, Keys.Control | Keys.End); Require(input.Value == 9999, "Ctrl+End did not select the maximum.");
        input.Editor.Text = "10000"; Require(!input.TryCommit(out var error) && error.Length > 0, "Out-of-range typed amount was accepted.");
        input.Editor.Text = "-3"; Require(input.TryCommit(out _) && input.Value == -3, "Signed typed amount failed.");
    }
    private static void InvalidSaveState()
    {
        var context = new WorkspaceContext(); using var view = new DateView(context); using var host = Host(view);
        var saveDifference = Named<CalcButton>(view, "Save this date difference to history");
        var saveAdjustment = Named<CalcButton>(view, "Calculate adjusted date and save it to history");
        Field<DateInput>(view, "first").DateText = "";
        Require(!saveDifference.Enabled && view.CopyText.Length == 0, "Empty date leaves saving enabled.");
        Field<DateInput>(view, "first").SetDate(new(2026, 10, 16));
        Require(saveDifference.Enabled, "Valid date did not restore saving.");
        Field<SegmentedControl>(view, "workflow").SelectedIndex = 1;
        Field<FlatNumberInput>(view, "days").Editor.Text = "invalid";
        Require(!saveAdjustment.Enabled && view.CopyText.Length == 0, "Invalid interval leaves saving enabled.");
        Field<FlatNumberInput>(view, "days").Value = 1;
        Field<DateInput>(view, "starting").SetDate(DateOnly.MaxValue);
        Require(!saveAdjustment.Enabled, "Out-of-range adjusted date leaves saving enabled.");
        Field<DateInput>(view, "starting").SetDate(new(2026, 10, 16));
        Require(saveAdjustment.Enabled && view.CopyText == "2026-10-17", "Correcting overflow did not restore saving.");
        Require(context.History.Items.Count == 0, "Validation transitions recorded history.");
    }
    private static void GregorianConsistency(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            var context = new WorkspaceContext(); using var view = new DateView(context); using var host = Host(view);
            var first = Field<DateInput>(view, "first"); first.SetDate(new(2026, 10, 16));
            Field<DateInput>(view, "second").SetDate(new(2026, 10, 17));
            Click(Named<CalcButton>(view, "Save this date difference to history"));
            Require(context.History.Items[0].Expression.StartsWith("2026-10-16 → 2026-10-17", StringComparison.Ordinal), "Difference history changed the Gregorian ISO years/months.");
            Click(Named<CalcButton>(first, "Choose Start date in inline calendar"));
            var calendar = Field<DateCalendarPanel>(view, "calendar");
            var formatting = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            formatting.DateTimeFormat.Calendar = formatting.OptionalCalendars.OfType<GregorianCalendar>().First();
            Require(Field<Label>(calendar, "heading").Text == new DateOnly(2026, 10, 1).ToString("MMMM yyyy", formatting), "Calendar heading disagrees with its Gregorian grid.");
            var sixteenth = Descendants(calendar).OfType<CalcButton>().Single(button => button.Text == "16");
            Require(sixteenth.AccessibleName == new DateOnly(2026, 10, 16).ToString("dddd, d MMMM yyyy", formatting), "Calendar cell accessible date disagrees with its visible Gregorian day.");
            view.Command(Keys.Escape);
            Field<SegmentedControl>(view, "workflow").SelectedIndex = 1;
            Field<DateInput>(view, "starting").SetDate(new(2026, 10, 16));
            Field<FlatNumberInput>(view, "days").Value = 1;
            Click(Named<CalcButton>(view, "Calculate adjusted date and save it to history"));
            Require(context.History.Items[0].Expression.StartsWith("2026-10-16 +", StringComparison.Ordinal) && context.History.Items[0].Result == "2026-10-17", "Adjustment caption and reusable date disagree.");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    private static void CompactLayout(ThemePreference theme)
    {
        using var view = new DateView(new()); using var host = Host(view, new(480, 350)); Ui.Theme(host, Palette.Resolve(theme)); Layout(host);
        Click(Named<CalcButton>(Field<DateInput>(view, "first"), "Choose Start date in inline calendar")); Layout(host);
        var calendar = Field<DateCalendarPanel>(view, "calendar");
        foreach (var day in CurrentCalendarDays(calendar))
            Require(day.Width * 96f / day.DeviceDpi >= 24 && day.Height * 96f / day.DeviceDpi >= 24, "Compact calendar day target fell below 24 logical pixels: " + day.Size);
        view.Command(Keys.Escape); Layout(host); CheckDetail(Field<Label>(view, "differenceDetail"));
        Field<SegmentedControl>(view, "workflow").SelectedIndex = 1; Layout(host); CheckDetail(Field<Label>(view, "adjustmentDetail"));
        Require(!Descendants(view).OfType<ScrollableControl>().Any(control => control.AutoScroll), "Date workflow enables scrolling.");
    }
    private static void ShortWorkspace(ThemePreference theme)
    {
        using var view = new DateView(new()); using var host = Host(view, new(590, 246)); Ui.Theme(host, Palette.Resolve(theme)); Layout(host);
        Field<DateInput>(view, "first").SetDate(new(2026, 10, 16));
        CheckDetail(Field<Label>(view, "differenceDetail"));
        foreach (var name in new[] { "first", "second" }) CheckEditor(Field<DateInput>(view, name).Editor);
        Click(Named<CalcButton>(Field<DateInput>(view, "first"), "Choose Start date in inline calendar")); Layout(host);
        var calendar = Field<DateCalendarPanel>(view, "calendar");
        Require(Field<int>(calendar, "weeksPerPage") == 3, "Short calendar must page three weeks at a time.");
        var visibleDates = new HashSet<int>();
        while (Named<CalcButton>(calendar, "Previous calendar weeks").Enabled) { Click(Named<CalcButton>(calendar, "Previous calendar weeks")); Layout(host); }
        while (true)
        {
            foreach (var day in CurrentCalendarDays(calendar))
            {
                visibleDates.Add(int.Parse(day.Text, CultureInfo.CurrentCulture));
                Require(day.Width * 96f / day.DeviceDpi >= 24 && day.Height * 96f / day.DeviceDpi >= 24,
                    "Short calendar day target fell below 24 logical pixels: " + day.Size);
            }
            var next = Named<CalcButton>(calendar, "Next calendar weeks"); if (!next.Enabled) break; Click(next); Layout(host);
        }
        Require(visibleDates.SetEquals(Enumerable.Range(1, 31)), "Calendar week pages do not expose every October date.");
        view.Command(Keys.Escape);
        Click(Named<CalcButton>(Field<DateInput>(view, "first"), "Choose Start date in inline calendar"));
        var eighteenth = Descendants(calendar).OfType<CalcButton>().Single(button => button.Text == "18"); Key(eighteenth, Keys.Right); Layout(host);
        Require(Field<int>(calendar, "weekPage") == 1 && CurrentCalendarDays(calendar).Any(button => button.Text == "19"), "Day-key navigation did not reveal the destination week page.");
        view.Command(Keys.Escape); Field<SegmentedControl>(view, "workflow").SelectedIndex = 1; Layout(host);
        CheckEditor(Field<DateInput>(view, "starting").Editor);
        foreach (var name in new[] { "years", "months", "weeks", "days" })
        {
            var interval = Field<FlatNumberInput>(view, name); CheckEditor(interval.Editor);
            foreach (var action in Descendants(interval).OfType<CalcButton>()) Require(action.Height * 96f / action.DeviceDpi >= 24, "Compact interval step target fell below 24 logical pixels.");
        }
        CheckDetail(Field<Label>(view, "adjustmentDetail"));
        Require(!Descendants(view).OfType<ScrollableControl>().Any(control => control.AutoScroll), "Short date workspace enables scrolling.");
    }
    private static IEnumerable<CalcButton> CurrentCalendarDays(DateCalendarPanel calendar) => Descendants(calendar).OfType<CalcButton>()
        .Where(button => int.TryParse(button.Text, out _) && button.Parent is TableLayoutPanel grid && grid.RowStyles[grid.GetRow(button)].SizeType == SizeType.Percent);
    private static void CheckEditor(TextBox editor)
    {
        Require(editor.ClientSize.Height >= editor.Font.Height, $"Compact input text is vertically clipped: {editor.AccessibleName}, {editor.ClientSize.Height} < {editor.Font.Height}.");
        Require(editor.Width > TextRenderer.MeasureText(editor.Text, editor.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width,
            "Compact date/interval text does not fit its editor: " + editor.AccessibleName);
    }
    private static void CheckDetail(Label detail)
    {
        var measured = TextRenderer.MeasureText(detail.Text, detail.Font, new Size(Math.Max(1, detail.Width - 8), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        Require(measured.Height <= detail.Height, $"Date detail text is clipped: needs {measured.Height}, has {detail.Height}.");
    }
    private static Form Host(Control child, Size? size = null)
    {
        var logical = size ?? new Size(590, 480);
        var host = new Form { ClientSize = logical, AutoScaleMode = AutoScaleMode.None }; child.Dock = DockStyle.Fill; host.Controls.Add(child);
        _ = host.Handle; host.ClientSize = new(logical.Width * host.DeviceDpi / 96, logical.Height * host.DeviceDpi / 96);
        EnsureHandles(host); Ui.Theme(host, Palette.Resolve(ThemePreference.Light)); Layout(host); return host;
    }
    private static void EnsureHandles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) EnsureHandles(child); }
    private static void Layout(Control control) { control.PerformLayout(); foreach (Control child in control.Controls) Layout(child); control.PerformLayout(); }
    private static IEnumerable<Control> Descendants(Control control) { foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static T Named<T>(Control control, string name) where T : Control => Descendants(control).OfType<T>().Single(item => item.AccessibleName == name);
    private static T Field<T>(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException("Missing date field " + name);
        return (T)field.GetValue(instance)!;
    }
    private static void Click(Control control) => typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [EventArgs.Empty]);
    private static void Key(Control control, Keys key) => control.GetType().GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [new KeyEventArgs(key)]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
