using System.Globalization;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Controls;

/// <summary>Inline Gregorian calendar; it never creates a popup or dropdown window.</summary>
public sealed class DateCalendarPanel : UserControl, IThemedControl
{
    private readonly Label heading = Ui.Label("", 12, true);
    private readonly Label context = Ui.Label("", 9);
    private readonly CalcButton previous;
    private readonly CalcButton next;
    private readonly CalcButton previousWeeks;
    private readonly CalcButton nextWeeks;
    private readonly Label weekPageLabel = Ui.Label("", 8);
    private readonly TableLayoutPanel grid;
    private readonly List<(CalcButton Button, DateOnly? Date)> days = [];
    private DateOnly month;
    private DateOnly selected;
    private DateOnly cursor;
    private int weeksPerPage = 6;
    private int weekPage;
    public event Action<DateOnly>? DateSelected;
    public event Action? CancelRequested;
    public DateCalendarPanel()
    {
        SuspendLayout(); Dock = DockStyle.Fill; Margin = new(3); AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        AccessibleName = "Inline date calendar"; AccessibleRole = AccessibleRole.Grouping;
        var layout = Ui.Table(1, 24, 36, 22, -100, 36, 22);
        layout.Controls.Add(context, 0, 0);
        var navigation = Ui.Table(3, -100); navigation.ColumnStyles.Clear();
        navigation.ColumnStyles.Add(new(SizeType.Absolute, 40)); navigation.ColumnStyles.Add(new(SizeType.Percent, 100)); navigation.ColumnStyles.Add(new(SizeType.Absolute, 40));
        previous = Ui.Button("‹", () => ChangeMonth(-1), ButtonTone.Normal, name: "Previous calendar month");
        next = Ui.Button("›", () => ChangeMonth(1), ButtonTone.Normal, name: "Next calendar month");
        heading.TextAlign = ContentAlignment.MiddleCenter; navigation.Controls.Add(previous, 0, 0); navigation.Controls.Add(heading, 1, 0); navigation.Controls.Add(next, 2, 0);
        layout.Controls.Add(navigation, 0, 1);
        var week = Ui.Table(7, -100); var first = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        for (var i = 0; i < 7; i++) { var label = Ui.Label(CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(first + i) % 7], 8); label.TextAlign = ContentAlignment.MiddleCenter; week.Controls.Add(label, i, 0); }
        layout.Controls.Add(week, 0, 2);
        grid = Ui.Table(7, -1, -1, -1, -1, -1, -1);
        for (var index = 0; index < 42; index++)
        {
            var slot = index; var button = Ui.Button("", () => { if (days[slot].Date is { } date) DateSelected?.Invoke(date); }, ButtonTone.Normal);
            button.Margin = new(2); Ui.SetFont(button, 10);
            button.PreviewKeyDown += (_, e) => { if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown) e.IsInputKey = true; };
            button.KeyDown += (_, e) =>
            {
                if (days[slot].Date is not { } date) return;
                try
                {
                    var target = e.KeyCode switch { Keys.Left => date.AddDays(-1), Keys.Right => date.AddDays(1), Keys.Up => date.AddDays(-7), Keys.Down => date.AddDays(7),
                        Keys.Home => new DateOnly(date.Year, date.Month, 1), Keys.End => new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
                        Keys.PageUp => date.AddMonths(-1), Keys.PageDown => date.AddMonths(1), _ => (DateOnly?)null };
                    if (target is null) return;
                    cursor = target.Value; month = new(cursor.Year, cursor.Month, 1); RefreshMonth(); FocusSelectedDay(); e.Handled = e.SuppressKeyPress = true;
                }
                catch (ArgumentOutOfRangeException) { e.Handled = e.SuppressKeyPress = true; }
            };
            days.Add((button, null)); grid.Controls.Add(button, index % 7, index / 7);
        }
        layout.Controls.Add(grid, 0, 3);
        var actions = Ui.Table(5, -100); actions.ColumnStyles.Clear();
        actions.ColumnStyles.Add(new(SizeType.Absolute, 32)); actions.ColumnStyles.Add(new(SizeType.Percent, 35));
        actions.ColumnStyles.Add(new(SizeType.Percent, 30)); actions.ColumnStyles.Add(new(SizeType.Percent, 35)); actions.ColumnStyles.Add(new(SizeType.Absolute, 32));
        previousWeeks = Ui.Button("‹", () => ChangeWeekPage(-1), ButtonTone.Normal, name: "Previous calendar weeks");
        nextWeeks = Ui.Button("›", () => ChangeWeekPage(1), ButtonTone.Normal, name: "Next calendar weeks");
        previousWeeks.Margin = nextWeeks.Margin = new(1); weekPageLabel.TextAlign = ContentAlignment.MiddleCenter;
        actions.Controls.Add(previousWeeks, 0, 0);
        actions.Controls.Add(Ui.Button("Today", () => DateSelected?.Invoke(DateOnly.FromDateTime(DateTime.Today)), ButtonTone.Normal), 1, 0);
        actions.Controls.Add(weekPageLabel, 2, 0);
        actions.Controls.Add(Ui.Button("Cancel", () => CancelRequested?.Invoke(), ButtonTone.Quiet), 3, 0);
        actions.Controls.Add(nextWeeks, 4, 0);
        var help = Ui.Label("Arrows: move day · Page Up/Down: month · Enter: choose · Esc: cancel", 8);
        layout.Controls.Add(actions, 0, 4); layout.Controls.Add(help, 0, 5);
        SizeChanged += (_, _) =>
        {
            var scale = DeviceDpi / 96f; var compact = ClientSize.Height / scale < 290;
            layout.RowStyles[0].Height = (compact ? 20 : 24) * scale; layout.RowStyles[1].Height = (compact ? 30 : 36) * scale;
            layout.RowStyles[2].Height = (compact ? 18 : 22) * scale; layout.RowStyles[4].Height = (compact ? 30 : 36) * scale;
            layout.RowStyles[5].Height = (compact ? 0 : 22) * scale; help.Visible = !compact;
            var newPageSize = compact ? 3 : 6;
            if (weeksPerPage != newPageSize) { weeksPerPage = newPageSize; RefreshMonth(); }
        };
        AccessibleDescription = help.Text; Controls.Add(layout); ShowDate("date", DateOnly.FromDateTime(DateTime.Today)); ResumeLayout(true);
    }
    public void ShowDate(string inputName, DateOnly date) { context.Text = "Choose " + inputName; selected = cursor = date; month = new(date.Year, date.Month, 1); RefreshMonth(); }
    public void FocusSelectedDay() { RefreshMonth(); var button = days.FirstOrDefault(item => item.Date == cursor).Button; button?.Focus(); }
    public bool ChooseFocusedDate()
    {
        foreach (var item in days) if (item.Button.Focused && item.Date is { } date) { DateSelected?.Invoke(date); return true; }
        return false;
    }
    private void ChangeMonth(int amount)
    {
        try { month = month.AddMonths(amount); cursor = new(month.Year, month.Month, Math.Min(cursor.Day, DateTime.DaysInMonth(month.Year, month.Month))); RefreshMonth(); }
        catch (ArgumentOutOfRangeException) { }
    }
    private void ChangeWeekPage(int direction) { weekPage = Math.Clamp(weekPage + direction, 0, (6 / weeksPerPage) - 1); RefreshMonth(false); }
    private void RefreshMonth(bool exposeCursor = true)
    {
        var formatting = GregorianFormattingCulture();
        heading.Text = month.ToString("MMMM yyyy", formatting);
        previous.Enabled = month.Year != 1 || month.Month != 1; next.Enabled = month.Year != 9999 || month.Month != 12;
        var offset = ((int)month.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek + 7) % 7;
        var count = DateTime.DaysInMonth(month.Year, month.Month);
        if (exposeCursor && cursor.Year == month.Year && cursor.Month == month.Month) weekPage = (offset + cursor.Day - 1) / (weeksPerPage * 7);
        weekPage = Math.Clamp(weekPage, 0, (6 / weeksPerPage) - 1);
        for (var row = 0; row < 6; row++)
        {
            var visible = row >= weekPage * weeksPerPage && row < (weekPage + 1) * weeksPerPage;
            grid.RowStyles[row].SizeType = visible ? SizeType.Percent : SizeType.Absolute;
            grid.RowStyles[row].Height = visible ? 100f / weeksPerPage : 0;
        }
        previousWeeks.Visible = nextWeeks.Visible = weekPageLabel.Visible = weeksPerPage < 6;
        previousWeeks.Enabled = weekPage > 0; nextWeeks.Enabled = weekPage + 1 < 6 / weeksPerPage;
        weekPageLabel.Text = $"Weeks {weekPage * weeksPerPage + 1}–{(weekPage + 1) * weeksPerPage} / 6";
        weekPageLabel.AccessibleName = weekPageLabel.Text;
        for (var index = 0; index < days.Count; index++)
        {
            var day = index - offset + 1; var button = days[index].Button;
            DateOnly? date = day >= 1 && day <= count ? new(month.Year, month.Month, day) : null;
            days[index] = (button, date); button.Visible = index / 7 >= weekPage * weeksPerPage && index / 7 < (weekPage + 1) * weeksPerPage;
            button.Enabled = date.HasValue; button.TabStop = date == cursor;
            button.Text = date.HasValue ? day.ToString(CultureInfo.CurrentCulture) : "";
            button.AccessibleName = date?.ToString("dddd, d MMMM yyyy", formatting) ?? "Outside this month";
            button.AccessibleDescription = date == selected ? "Selected date. Press Enter or Space to choose." : "Press Enter or Space to choose this date.";
            button.Selected = date == selected; button.Invalidate();
        }
    }
    // The grid and ISO editors are Gregorian even when the region defaults to another calendar.
    internal static CultureInfo GregorianFormattingCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        if (culture.DateTimeFormat.Calendar is GregorianCalendar) return culture;
        var calendar = culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault();
        if (calendar is null) return CultureInfo.InvariantCulture;
        var formatting = (CultureInfo)culture.Clone();
        formatting.DateTimeFormat.Calendar = calendar;
        return formatting;
    }
    public void ApplyTheme(Palette palette) { BackColor = palette.Surface; context.ForeColor = palette.Muted; }
}
