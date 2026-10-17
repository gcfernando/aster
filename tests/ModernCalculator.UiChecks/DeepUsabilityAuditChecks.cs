using System.Globalization;
using System.Reflection;
using ModernCalculator.Core.Calculations;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

public static class DeepUsabilityAuditChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Deep usability: selected Scientific powers and postfix functions retain operands", SelectedPostfixOperations);
        check("Deep usability: explicit Scientific selections override answer continuation", SelectedAnswerEditing);
        check("Deep usability: Scientific keypad edits participate in native undo", KeypadUndo);
        check("Deep usability: ISO date reuse closes and updates the active calendar field", CalendarReuse);
        check("Deep usability: Scientific EXP captures complete localized exponent literals", LocalizedExponentOperands);
        check("Deep usability: signed Scientific numeric reuse consistently replaces completed input", NumericReuse);
    }

    private static void SelectedPostfixOperations()
    {
        foreach (var completed in new[] { false, true })
        foreach (var operation in new[] { ("x²", "(3)^2", 11d), ("x³", "(3)^3", 29d), ("Factorial", "(3)!", 8d), ("Power", "(3)^2", 11d) })
        WithScientific((view, editor) =>
        {
            editor.Text = "2+3";
            if (completed) view.Command(Keys.Enter);
            editor.Select(2, 1);
            Click(Named<CalcButton>(view, operation.Item1));
            if (operation.Item1 == "Power") editor.SelectedText = "2";
            Require(editor.Text == "2+" + operation.Item2, "A selected operand was discarded by " + operation.Item1);
            Require(Field<double?>(view, "answer") == (completed ? (double?)5 : null), "Editing changed Ans before evaluation.");
            view.Command(Keys.Enter);
            Require(view.CopyText == operation.Item3.ToString("G17", CultureInfo.CurrentCulture), "Selected postfix expression evaluated incorrectly.");
        });
        WithScientific((view, editor) =>
        {
            editor.Text = "2+3"; editor.SelectAll(); Click(Named<CalcButton>(view, "x²")); view.Command(Keys.Enter);
            Require(view.CopyText == "25", "Selected compound operand lost its precedence.");
        });
    }

    private static void SelectedAnswerEditing()
    {
        foreach (var operation in new[] { ("7", "2+7", "9"), ("Square root", "2+sqrt(9)", "5"),
            ("Change sign", "2+-(9)", "-7"), ("Enter scientific exponent", "2+9E3", "9002"),
            ("Root with chosen degree", "2+root(9,3)", "") })
        WithScientific((view, editor) =>
        {
            editor.Text = "2+9"; view.Command(Keys.Enter); editor.Select(2, 1);
            // The sign key retains its familiar visible label as its accessible name.
            var name = operation.Item1 == "Change sign" ? "+/−" : operation.Item1;
            Click(Named<CalcButton>(view, name));
            Require(editor.Text == operation.Item2, "Explicit post-result selection was replaced by Ans for " + name);
            Require(Field<double?>(view, "answer") == 11, "Explicit selection editing overwrote Ans.");
            if (operation.Item3.Length > 0)
            {
                view.Command(Keys.Enter);
                Require(view.CopyText == operation.Item3, "Explicit post-result edit evaluated incorrectly for " + name);
            }
        });
        WithScientific((view, editor) =>
        {
            editor.Text = "2+3"; view.Command(Keys.Enter); editor.Select(editor.TextLength, 0);
            Click(Named<CalcButton>(view, "x²")); view.Command(Keys.Enter);
            Require(view.CopyText == "25", "No-selection answer continuation changed.");
        });
        WithScientific((view, editor) =>
        {
            editor.Text = "3"; editor.Select(editor.TextLength, 0); Click(Named<CalcButton>(view, "Enter scientific exponent"));
            Require(editor.SelectionLength > 0, "Exponent template did not select its editable argument.");
            view.Command(Keys.Enter);
            Require(editor.SelectionLength == 0, "Completed template retained an automatic argument selection.");
            Click(Named<CalcButton>(view, "7"));
            Require(editor.Text == "7", "A leftover template selection prevented new input after a result.");
        });
    }

    private static void KeypadUndo()
    {
        foreach (var name in new[] { "7", "Decimal separator", "Backspace", "C", "x²", "+/−", "Square root", "Enter scientific exponent", "Root with chosen degree" })
        WithScientific((view, editor) =>
        {
            editor.Text = "23"; view.Command(Keys.Enter); editor.Select(editor.TextLength, 0); editor.ClearUndo();
            Click(Named<CalcButton>(view, name));
            Require(editor.CanUndo, "Scientific keypad edit cleared native undo: " + name);
            editor.Undo();
            Require(editor.Text == "23", "Native undo did not restore the prior expression for " + name);
            Require(Field<double?>(view, "answer") == 23, "Undo changed the saved answer.");
        });
    }

    private static void CalendarReuse()
    {
        using var view = new DateView(new WorkspaceContext()); using var host = Host(view, new(590, 620));
        foreach (var name in new[] { "Start date", "End date" })
        {
            var field = Descendants(view).OfType<DateInput>().Single(control => control.AccessibleName == name);
            Click(Named<CalcButton>(field, "Choose " + name + " in inline calendar"));
            Require(Field<DateInput?>(view, "calendarTarget") == field, "Calendar did not open for " + name);
            view.Reuse("2026-12-25");
            Require(Field<DateInput?>(view, "calendarTarget") is null && field.ReadDate() == new DateOnly(2026, 12, 25),
                "Imported ISO date remained behind the old calendar for " + name);
            Require(view.CopyText.Length > 0, "Date reuse did not refresh the visible calculation.");
        }
        Named<SegmentedControl>(view, "Date calculation").SelectedIndex = 1;
        var starting = Descendants(view).OfType<DateInput>().Single(control => control.AccessibleName == "Starting date");
        Click(Named<CalcButton>(starting, "Choose Starting date in inline calendar")); view.Reuse("2027-01-01");
        Require(Field<DateInput?>(view, "calendarTarget") is null && starting.ReadDate() == new DateOnly(2027, 1, 1) && view.CopyText == "2027-01-01",
            "Adjusted-date reuse left its calendar or preview stale.");
    }

    private static void LocalizedExponentOperands()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var locale in new[] { "en-US", "sv-SE", "fa-IR", "fa-AF" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(locale);
                var format = CultureInfo.CurrentCulture.NumberFormat;
                foreach (var sign in new[] { "-", "−", format.NegativeSign }.Distinct())
                {
                    using var view = new ScientificView(new WorkspaceContext()); using var host = Host(view, new(650, 620));
                    var editor = Named<TextBox>(view, "Scientific expression");
                    var raw = "1" + format.NumberDecimalSeparator + "5E" + sign + "3";
                    editor.Text = raw; view.Command(Keys.Enter);
                    Require(view.CopyText == (0.0015d).ToString("G17", CultureInfo.CurrentCulture), "Direct localized exponent literal was rejected in " + locale);
                    editor.Text = raw + " "; editor.Text = raw; editor.Select(editor.TextLength, 0);
                    Click(Named<CalcButton>(view, "Enter scientific exponent"));
                    Require(editor.Text == "(" + raw + ")*10^3", "EXP captured only part of a localized exponent literal in " + locale);
                    view.Command(Keys.Enter);
                    Require(view.CopyText == (1.5d).ToString("G17", CultureInfo.CurrentCulture), "EXP changed the whole localized operand's value in " + locale);
                }
            }
            // A comma separating function arguments is not an English decimal mark.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            using var argumentView = new ScientificView(new WorkspaceContext()); using var argumentHost = Host(argumentView, new(650, 620));
            var argumentEditor = Named<TextBox>(argumentView, "Scientific expression");
            argumentEditor.Text = "root(8,3)"; argumentEditor.Select(argumentEditor.TextLength - 1, 0);
            Click(Named<CalcButton>(argumentView, "Enter scientific exponent"));
            Require(argumentEditor.Text == "root(8,3E3)", "EXP consumed a function argument separator as a decimal mark.");
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private static void NumericReuse()
    {
        foreach (var numeric in new[] { -3d, 3d })
        foreach (var source in new[] { "Paste", "History", "Memory" })
        foreach (var selected in new[] { false, true })
        WithScientific((view, editor) =>
        {
            editor.Text = "2+3"; view.Command(Keys.Enter); editor.ClearUndo();
            if (selected) editor.Select(2, 1);
            if (source == "Paste") view.Reuse(numeric.ToString("G17", CultureInfo.CurrentCulture));
            else if (source == "History") view.ReuseStoredResult(numeric.ToString("G17", CultureInfo.InvariantCulture));
            else view.ReuseExact(BigDecimal.Parse(numeric.ToString("G17", CultureInfo.InvariantCulture)));
            var literal = ScientificEditing.ExpressionLiteral(numeric, CultureInfo.CurrentCulture);
            Require(editor.Text == (selected ? "2+" + literal : literal), "Numeric " + source + " continued the prior answer or discarded explicit selection.");
            Require(Field<double?>(view, "answer") == 5, "Numeric reuse overwrote Ans before evaluation.");
            Require(editor.CanUndo, "Numeric reuse lost native undo.");
            editor.Undo(); Require(editor.Text == "2+3" && Field<double?>(view, "answer") == 5, "Numeric reuse undo failed to restore input/Ans.");
            // Repeat after undo as an ordinary operand insertion, then verify the value itself.
            editor.SelectAll();
            if (source == "Paste") view.Reuse(literal);
            else if (source == "History") view.ReuseStoredResult(numeric.ToString("G17", CultureInfo.InvariantCulture));
            else view.ReuseExact(BigDecimal.Parse(numeric.ToString("G17", CultureInfo.InvariantCulture)));
            view.Command(Keys.Enter);
            Require(view.CopyText == numeric.ToString("G17", CultureInfo.CurrentCulture), "Numeric reuse evaluated with the wrong sign.");
        });
    }

    private static void WithScientific(Action<ScientificView, TextBox> action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            using var view = new ScientificView(new WorkspaceContext()); using var host = Host(view, new(650, 620));
            action(view, Named<TextBox>(view, "Scientific expression"));
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }
    private static Form Host(Control content, Size size)
    {
        var host = new Form { ClientSize = size, AutoScaleMode = AutoScaleMode.None };
        host.Controls.Add(content); _ = host.Handle; Layout(host); Handles(host); Layout(host);
        Require(!host.Visible, "Deep usability checks must keep native windows hidden."); return host;
    }
    private static void Handles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) Handles(child); }
    private static void Layout(Control control) { control.PerformLayout(); foreach (Control child in control.Controls) Layout(child); }
    private static void Click(CalcButton button)
    {
        Require(button.Enabled, "Cannot activate disabled control " + button.AccessibleName);
        typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, [EventArgs.Empty]);
    }
    private static T Named<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().Single(control => control.AccessibleName == name);
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static IEnumerable<Control> Descendants(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
