// Developer ::> Gehan Fernando
using System.Globalization;
using ModernCalculator.Core.Modes;
using ModernCalculator.WinForms.Controls;

namespace ModernCalculator.WinForms.Views;

/// <summary>Independent date-difference and date-adjustment workflows with inline date picking.</summary>
public sealed class DateView : ModeView
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private readonly SegmentedControl workflow = new("Date calculation", ["Days between dates", "Adjust a date"]);
    private readonly SegmentedControl dayCounting = new("Count dates", ["Elapsed days", "Include both dates"]);
    private readonly SegmentedControl operation = new("Date adjustment operation", ["Add", "Subtract"]);
    private readonly DateInput first = new("Start date", Today);
    private readonly DateInput second = new("End date", Today.AddDays(30));
    private readonly DateInput starting = new("Starting date", Today);
    private readonly FlatNumberInput years = Interval("Years");
    private readonly FlatNumberInput months = Interval("Months");
    private readonly FlatNumberInput weeks = Interval("Weeks");
    private readonly FlatNumberInput days = Interval("Days");
    private readonly ResultDisplay differenceDisplay = new();
    private readonly ResultDisplay adjustmentDisplay = new();
    private readonly Label differenceDetail = Ui.Label("", 9);
    private readonly Label adjustmentDetail = Ui.Label("", 9);
    private readonly CalcButton saveDifference;
    private readonly CalcButton saveAdjustment;
    private readonly Panel body = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly TableLayoutPanel differenceLayout;
    private readonly TableLayoutPanel adjustmentLayout;
    private readonly DateCalendarPanel calendar = new() { Visible = false };
    private DateInput? calendarTarget;
    private bool updating;
    private bool fittingDetail;
    private string differenceCopy = "";
    private string adjustmentCopy = "";
    public DateView(WorkspaceContext context) : base(context)
    {
        AccessibleName = "Date calculator"; AutoScroll = false;
        var root = Ui.Table(1, 40, -100); root.Controls.Add(workflow, 0, 0); root.Controls.Add(body, 0, 1); Controls.Add(root);
        differenceLayout = Ui.Table(1, 22, -26, 34, 34, -45, -29);
        differenceLayout.Controls.Add(Ui.Label("Compare two calendar dates", 11, true), 0, 0);
        var dates = Ui.Table(2, -100); dates.Controls.Add(first, 0, 0); dates.Controls.Add(second, 1, 0); differenceLayout.Controls.Add(dates, 0, 1);
        var compareActions = Ui.Table(3, -100);
        compareActions.Controls.Add(Ui.Button("Swap dates", Swap, ButtonTone.Normal, name: "Swap the start and end dates"), 0, 0);
        compareActions.Controls.Add(Ui.Button("Reset", ResetDifference, ButtonTone.Normal, name: "Reset date difference to today and thirty days later"), 1, 0);
        saveDifference = Ui.Button("Save difference", () => Difference(true), ButtonTone.Operator, name: "Save this date difference to history");
        compareActions.Controls.Add(saveDifference, 2, 0);
        differenceLayout.Controls.Add(compareActions, 0, 2); differenceLayout.Controls.Add(dayCounting, 0, 3);
        differenceLayout.Controls.Add(differenceDisplay, 0, 4); differenceLayout.Controls.Add(differenceDetail, 0, 5);
        adjustmentLayout = Ui.Table(1, 22, -25, 34, 20, -20, 34, -35, -20);
        adjustmentLayout.Controls.Add(Ui.Label("Move a date forwards or backwards", 11, true), 0, 0);
        adjustmentLayout.Controls.Add(starting, 0, 1); adjustmentLayout.Controls.Add(operation, 0, 2);
        var names = Ui.Table(4, -100); var intervals = Ui.Table(4, -100);
        var fields = new[] { years, months, weeks, days }; var labels = new[] { "Years", "Months", "Weeks", "Days" };
        for (var index = 0; index < fields.Length; index++) { names.Controls.Add(Ui.Label(labels[index], 9, true), index, 0); intervals.Controls.Add(fields[index], index, 0); }
        adjustmentLayout.Controls.Add(names, 0, 3); adjustmentLayout.Controls.Add(intervals, 0, 4);
        var adjustActions = Ui.Table(2, -100);
        adjustActions.Controls.Add(Ui.Button("Reset", ResetAdjustment, ButtonTone.Normal, name: "Reset adjustment to today with a zero interval"), 0, 0);
        saveAdjustment = Ui.Button("Calculate & save", () => Adjust(true), ButtonTone.Operator, name: "Calculate adjusted date and save it to history");
        adjustActions.Controls.Add(saveAdjustment, 1, 0);
        adjustmentLayout.Controls.Add(adjustActions, 0, 5); adjustmentLayout.Controls.Add(adjustmentDisplay, 0, 6); adjustmentLayout.Controls.Add(adjustmentDetail, 0, 7);
        body.Controls.Add(differenceLayout); body.Controls.Add(adjustmentLayout); body.Controls.Add(calendar);
        foreach (var field in new[] { first, second, starting }) { field.CalendarRequested += (_, _) => OpenCalendar(field); field.DateChanged += (_, _) => { if (!updating) EvaluateCurrent(false); }; }
        foreach (var field in fields) field.InputChanged += (_, _) => { if (!updating) Adjust(false); };
        dayCounting.SelectedIndexChanged += (_, _) => { if (!updating) Difference(false); }; operation.SelectedIndexChanged += (_, _) => { if (!updating) Adjust(false); };
        workflow.SelectedIndexChanged += (_, _) => { CloseCalendar(false); ShowWorkflow(); EvaluateCurrent(false); FocusInput(); };
        calendar.DateSelected += date => { if (calendarTarget is { } target) target.SetDate(date); CloseCalendar(); };
        calendar.CancelRequested += () => CloseCalendar();
        foreach (var detail in new[] { differenceDetail, adjustmentDetail })
        { detail.SizeChanged += (_, _) => FitDetail(detail); detail.TextChanged += (_, _) => FitDetail(detail); }
        SizeChanged += (_, _) => AdaptLayout(root);
        ShowWorkflow(); Difference(false); Adjust(false); FinishLayout(); AdaptLayout(root);
    }
    private static FlatNumberInput Interval(string name) => new() { AccessibleName = name, Minimum = -9999, Maximum = 9999, Value = 0 };
    private void ShowWorkflow() { differenceLayout.Visible = workflow.SelectedIndex == 0; adjustmentLayout.Visible = workflow.SelectedIndex == 1; (workflow.SelectedIndex == 0 ? differenceLayout : adjustmentLayout).BringToFront(); }
    private void AdaptLayout(TableLayoutPanel root)
    {
        var scale = DeviceDpi / 96f; var compact = ClientSize.Height / scale < 370;
        root.RowStyles[0].Height = (compact ? 30 : 40) * scale;
        differenceLayout.RowStyles[0].Height = adjustmentLayout.RowStyles[0].Height = (compact ? 0 : 22) * scale;
        differenceLayout.GetControlFromPosition(0, 0)!.Visible = adjustmentLayout.GetControlFromPosition(0, 0)!.Visible = !compact;
        SetRow(differenceLayout, 1, compact, 54, 26, scale);
        SetRow(differenceLayout, 5, compact, 42, 29, scale);
        differenceLayout.RowStyles[4].Height = compact ? 100 : 45;
        differenceLayout.RowStyles[2].Height = (compact ? 28 : 34) * scale;
        differenceLayout.RowStyles[3].Height = (compact ? 30 : 34) * scale;
        SetRow(adjustmentLayout, 1, compact, 48, 25, scale);
        SetRow(adjustmentLayout, 4, compact, 34, 20, scale);
        SetRow(adjustmentLayout, 7, compact, 28, 20, scale);
        adjustmentLayout.RowStyles[6].Height = compact ? 100 : 35;
        adjustmentLayout.RowStyles[2].Height = (compact ? 30 : 34) * scale;
        adjustmentLayout.RowStyles[5].Height = (compact ? 28 : 34) * scale;
        adjustmentLayout.RowStyles[3].Height = (compact ? 17 : 20) * scale;
        foreach (var input in new[] { first, second, starting }) input.AdaptCompact(compact);
        Ui.SetFont(differenceDetail, compact ? 8 : 9); Ui.SetFont(adjustmentDetail, compact ? 8 : 9);
        FitDetail(differenceDetail); FitDetail(adjustmentDetail);
    }
    private static void SetRow(TableLayoutPanel layout, int row, bool compact, float compactHeight, float normalWeight, float scale)
    { layout.RowStyles[row].SizeType = compact ? SizeType.Absolute : SizeType.Percent; layout.RowStyles[row].Height = compact ? compactHeight * scale : normalWeight; }
    private void FitDetail(Label detail)
    {
        if (fittingDetail || detail.Width <= 8 || detail.Height <= 0) return;
        fittingDetail = true;
        try
        {
            var size = 9f;
            while (size > 8)
            {
                using var candidate = new Font("Segoe UI", size);
                var measured = TextRenderer.MeasureText(detail.Text, candidate, new Size(Math.Max(1, detail.ClientSize.Width - 8), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                if (measured.Height <= detail.ClientSize.Height) break;
                size -= .5f;
            }
            Ui.SetFont(detail, size);
        }
        finally { fittingDetail = false; }
    }
    private void OpenCalendar(DateInput target)
    {
        calendarTarget = target; calendar.ShowDate(target.AccessibleName ?? "date", target.TryGetDate(out var date) ? date : Today);
        differenceLayout.Visible = adjustmentLayout.Visible = false; calendar.Visible = true; calendar.BringToFront(); Ui.Theme(calendar, Palette); calendar.FocusSelectedDay();
        Context.Status("Choose a calendar day, or press Escape to return to direct date entry.");
    }
    private void CloseCalendar(bool restoreFocus = true)
    {
        var target = calendarTarget; calendarTarget = null; calendar.Visible = false; ShowWorkflow();
        if (restoreFocus)
        {
            EvaluateCurrent(false);
            if (CopyText.Length > 0) Context.Status(workflow.SelectedIndex == 0 ? "Days between dates update as you edit. Save difference records the result." : "Adjusted dates update as you edit. Calculate & save records the result.");
            target?.FocusInput();
        }
    }
    private void Swap()
    {
        updating = true; try { (first.DateText, second.DateText) = (second.DateText, first.DateText); } finally { updating = false; }
        Difference(false); Context.Status("Start and end dates swapped. The difference is an absolute day count.");
    }
    private void ResetDifference()
    {
        updating = true; try { first.SetDate(Today); second.SetDate(Today.AddDays(30)); dayCounting.SelectedIndex = 0; } finally { updating = false; }
        Difference(false); first.FocusInput(); Context.Status("Date difference reset to today and thirty days later.");
    }
    private void ResetAdjustment()
    {
        updating = true; try { starting.SetDate(Today); operation.SelectedIndex = 0; foreach (var field in new[] { years, months, weeks, days }) field.Value = 0; } finally { updating = false; }
        Adjust(false); starting.FocusInput(); Context.Status("Date adjustment reset to today with a zero interval.");
    }
    // Keep live previews out of history unless the caller requests a saved result.
    private void EvaluateCurrent(bool record) { if (workflow.SelectedIndex == 0) Difference(record); else Adjust(record); }
    private void Difference(bool record)
    {
        try
        {
            var start = first.ReadDate(); var end = second.ReadDate(); var span = DateCalculator.Difference(start, end);
            var inclusive = dayCounting.SelectedIndex == 1; var count = span.TotalDays + (inclusive ? 1 : 0);
            differenceCopy = count.ToString(CultureInfo.CurrentCulture);
            var label = $"{Iso(start)} → {Iso(end)} · {(inclusive ? "both dates counted" : "elapsed days")}";
            differenceDisplay.Set(label, $"{count:N0} {(count == 1 ? "day" : "days")}");
            var endpoints = inclusive ? "Both dates counted; same date: 1 day." : "Start not counted; end counted. Same date: 0 days.";
            differenceDetail.Text = $"{count / 7:N0} weeks + {count % 7} days · {span.Years} years, {span.Months} months, {span.Days} days elapsed\r\n{endpoints}" + (start > end ? "\r\nDates reversed; the absolute difference is shown." : "");
            differenceDetail.AccessibleDescription = differenceDetail.Text;
            saveDifference.Enabled = true;
            if (record) { Context.Record("Dates", label, $"{count} days"); Context.Status("Date difference saved to history."); }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or OverflowException)
        { differenceCopy = ""; saveDifference.Enabled = false; ShowError(differenceDisplay, differenceDetail, ex); }
    }
    private void Adjust(bool record)
    {
        try
        {
            var start = starting.ReadDate(); var fields = new[] { years, months, weeks, days }; var values = new int[4];
            for (var index = 0; index < fields.Length; index++) if (!fields[index].TryGetValue(out values[index])) throw new FormatException($"{fields[index].AccessibleName}: enter a whole number from −9999 to 9999.");
            var sign = operation.SelectedIndex == 0 ? 1 : -1;
            var result = DateCalculator.Add(start, sign * values[0], sign * values[1], sign * values[2], sign * values[3]);
            adjustmentCopy = result.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var label = $"{Iso(start)} {(sign == 1 ? "+" : "−")} {values[0]}y {values[1]}m {values[2]}w {values[3]}d";
            adjustmentDisplay.Set("Resulting date · " + label, adjustmentCopy);
            adjustmentDetail.Text = result.ToString("dddd, d MMMM yyyy", DateCalendarPanel.GregorianFormattingCulture())
                + "\r\nYears → months → weeks → days; month ends use the last day.";
            adjustmentDetail.AccessibleDescription = adjustmentDetail.Text;
            saveAdjustment.Enabled = true;
            if (record) { Context.Record("Dates", label, adjustmentCopy); Context.Status("Adjusted date saved to history."); }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or OverflowException)
        { adjustmentCopy = ""; saveAdjustment.Enabled = false; ShowError(adjustmentDisplay, adjustmentDetail, ex); }
    }
    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private void ShowError(ResultDisplay display, Label detail, Exception exception)
    {
        var message = exception is FormatException ? exception.Message : "The resulting date must be within years 0001–9999.";
        display.Set("Check date input", message, true); detail.Text = "Use YYYY-MM-DD and whole-number intervals. Correct the input to continue."; detail.AccessibleDescription = message;
        Context.Status(message);
    }
    public override string CopyText => workflow.SelectedIndex == 0 ? differenceCopy : adjustmentCopy;
    public override void Reuse(string value)
    {
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { Context.Status("Choose a date result in YYYY-MM-DD format; a day count cannot be used as a date."); return; }
        var target = workflow.SelectedIndex == 0 ? first : starting; target.SetDate(date); target.FocusInput();
    }
    public override void FocusInput() { if (calendarTarget is not null) calendar.FocusSelectedDay(); else (workflow.SelectedIndex == 0 ? first : starting).FocusInput(); }
    public override void RefreshFormatting() => EvaluateCurrent(false);
    public override bool Command(Keys key)
    {
        if (calendarTarget is not null) { if (key == Keys.Escape) { CloseCalendar(); return true; } return key == Keys.Enter && calendar.ChooseFocusedDate(); }
        if (key != Keys.Enter || HasFocusedAction(this)) return false; EvaluateCurrent(true); return true;
    }
    private static bool HasFocusedAction(Control control) => control is ButtonBase { Focused: true } || control.Controls.Cast<Control>().Any(HasFocusedAction);
}
