using System.Globalization;
using System.Reflection;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

/// <summary>Hidden component checks for regional input and keyboard/validation recovery.</summary>
public static class UsabilityAuditChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Usability audit: each compact calendar page retains a keyboard day", CalendarPageKeyboardEntry);
        check("Usability audit: corrected number input clears stale accessibility errors", NumericValidationRecovery);
        check("Usability audit: Scientific decimal key matches the regional separator", ScientificDecimalKey);
    }

    private static void CalendarPageKeyboardEntry()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var locale in new[] { "en-US", "sv-SE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(locale);
                using var calendar = new DateCalendarPanel();
                using var host = Host(calendar, new(590, 246));
                var chosen = 0;
                calendar.DateSelected += _ => chosen++;
                var previous = Named<CalcButton>(calendar, "Previous calendar weeks");
                var next = Named<CalcButton>(calendar, "Next calendar weeks");
                for (var month = 1; month <= 12; month++)
                {
                    var selected = new DateOnly(2026, month, DateTime.DaysInMonth(2026, month));
                    calendar.ShowDate("date", selected);
                    Require(previous.Enabled, "Month-end cursor must start on the second compact week page.");
                    Click(previous);
                    CheckKeyboardDay(calendar);
                    Require(next.Enabled, "Browsing back did not expose the next week page.");
                    // FocusInput must retain the page just browsed, rather than return to the selection.
                    calendar.FocusSelectedDay();
                    Require(next.Enabled && !previous.Enabled, "Restoring keyboard input returned to the old week page.");
                    Click(next);
                    CheckKeyboardDay(calendar);
                    Require(previous.Enabled && !next.Enabled, "Browsing forward did not retain the next week page.");
                    var selectedDay = Descendants(calendar).OfType<CalcButton>().Single(button => button.Selected);
                    Require(selectedDay.Text == selected.Day.ToString(CultureInfo.CurrentCulture), "Week paging changed the selected date.");
                    Require(chosen == 0, "Browsing or focusing calendar weeks chose a date before activation.");
                }
            }
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private static void CheckKeyboardDay(DateCalendarPanel calendar)
    {
        var currentDays = Descendants(calendar).OfType<CalcButton>().Where(button =>
            int.TryParse(button.Text, out _) && button.Parent is TableLayoutPanel grid &&
            grid.RowStyles[grid.GetRow(button)].SizeType == SizeType.Percent).ToArray();
        Require(currentDays.Count(button => button.TabStop && button.Enabled) == 1,
            "A compact calendar page must expose exactly one valid day to Tab navigation.");
    }

    private static void NumericValidationRecovery()
    {
        using var input = new FlatNumberInput { AccessibleName = "Display precision", Minimum = 3, Maximum = 15, Value = 7 };
        using var host = Host(input, new(300, 40));
        void FailCommit()
        {
            input.Editor.Text = "invalid";
            Require(!input.TryCommit(out var error) && input.Editor.AccessibleDescription == error,
                "Invalid commit did not describe its validation error.");
        }
        void CheckRecovered()
        {
            var description = input.Editor.AccessibleDescription ?? "";
            Require(description.Contains("Up/Down", StringComparison.Ordinal) &&
                description.Contains("3 to 15", StringComparison.Ordinal),
                "A corrected number still announces its previous commit error.");
        }
        FailCommit(); input.Editor.Text = "8"; CheckRecovered();
        Require(input.TryCommit(out var error) && error.Length == 0 && input.Value == 8, "Corrected input did not commit.");
        FailCommit(); input.Value = 9; CheckRecovered();
        FailCommit(); input.Maximum = 15; CheckRecovered();
    }

    private static void ScientificDecimalKey()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var locale in new[] { "en-US", "sv-SE", "fr-FR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(locale);
                using var view = new ScientificView(new WorkspaceContext());
                using var host = Host(view, new(650, 620));
                var button = Named<CalcButton>(view, "Decimal separator");
                var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                Require(button.Text == separator && button.AccessibleDescription?.Contains(separator, StringComparison.Ordinal) == true,
                    "Scientific decimal key label/help disagrees with the current region.");
                var editor = Named<TextBox>(view, "Scientific expression");
                editor.Text = "1"; editor.SelectionStart = editor.TextLength;
                Click(button); editor.SelectedText = "5";
                Require(editor.Text == "1" + separator + "5", "Decimal key inserted a different separator from its label.");
                view.Command(Keys.Enter);
                Require(view.CopyText == (1.5d).ToString("G17", CultureInfo.CurrentCulture), "Localized decimal keypad input evaluated incorrectly.");
            }
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private static Form Host(Control content, Size size)
    {
        var host = new Form { ClientSize = size, AutoScaleMode = AutoScaleMode.None };
        host.Controls.Add(content); _ = host.Handle; Layout(host); Handles(host); Layout(host);
        Require(!host.Visible, "Usability checks must keep native windows hidden.");
        return host;
    }
    private static void Handles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) Handles(child); }
    private static void Layout(Control control) { control.PerformLayout(); foreach (Control child in control.Controls) Layout(child); }
    private static void Click(CalcButton button)
    {
        Require(button.Enabled, "Cannot activate disabled control " + button.AccessibleName);
        typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);
    }
    private static T Named<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().Single(control => control.AccessibleName == name);
    private static IEnumerable<Control> Descendants(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
