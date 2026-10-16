using System.Reflection;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Forms;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

/// <summary>Managed routing and focus-snapshot checks; no dialogs or desktop focus are exercised.</summary>
public static class IndependentSharedChecks
{
    public static void Run(Action<string, Action> check)
    {
        foreach (var mode in new[] { "Standard", "Scientific", "Programmer", "Dates" })
        {
            var currentMode = mode;
            check("Independent shared: Enter defers focused actions in " + mode, () => FocusedActions(currentMode));
            check("Independent shared: input Enter evaluates once in " + mode, () => InputEnter(currentMode));
        }
        check("Independent shared: capture nested date field and preserve selection", DateSelectionSnapshot);
        check("Independent shared: selection restoration clamps changed text and tolerates disposal", ChangedSelectionSnapshot);
        check("Independent shared: removed focus target falls back to mode input", RemovedFocusFallback);
        check("Independent shared: Tab makes mode memory action own Enter", TabbedMemoryAction);
        check("Independent shared: typed calculator input restores Enter evaluation", TypedInputAfterTab);
        check("Independent shared: mouse keypad input restores Enter evaluation", MouseInputAfterTab);
    }

    private static void FocusedActions(string mode)
    {
        WithForm(mode, (form, context, view) =>
        {
            PrepareInput(view);
            var buttons = new[] { Named<CalcButton>(form, "Preferences"), Named<CalcButton>(form, "Show or hide history and memory") };
            foreach (var button in buttons)
            {
                form.ActiveControl = button;
                Require(ReferenceEquals(ActiveLeaf(form), button), "Hidden control activation did not establish the routing target.");
                Require(!Command(form, Keys.Enter), "Calculator consumed Enter for a focused shared action: " + button.AccessibleName);
                Require(context.History.Items.Count == 0, "Focused action Enter recorded a calculation.");
            }
            // These retain native ButtonBase processing rather than invoking a calculator command.
            using var checkbox = new CheckBox { Text = "Test native checkbox" };
            using var radio = new RadioButton { Text = "Test native radio" };
            form.Controls.Add(checkbox); form.Controls.Add(radio);
            foreach (var control in new ButtonBase[] { checkbox, radio })
            {
                form.ActiveControl = control;
                Require(ReferenceEquals(ActiveLeaf(form), control), "Hidden control activation did not establish the native choice target.");
                Require(!Command(form, Keys.Enter), "Calculator consumed Enter for native choice processing.");
                Require(context.History.Items.Count == 0, "Native choice Enter recorded a calculation.");
            }
        });
    }

    private static void InputEnter(string mode)
    {
        WithForm(mode, (form, context, view) =>
        {
            PrepareInput(view);
            if (view is ScientificView) view.ActiveControl = Named<TextBox>(view, "Scientific expression");
            else if (view is DateView)
            {
                var end = Descendants(view).OfType<DateInput>().Single(input => input.AccessibleName == "End date");
                end.ActiveControl = end.Editor; view.ActiveControl = end;
            }
            else if (view is ProgrammerView) view.ActiveControl = Descendants(view).OfType<TextBox>().Single(editor => !editor.ReadOnly);
            else view.ActiveControl = Named<CalcButton>(view, "Open memory list");
            form.ActiveControl = view;
            Require(Command(form, Keys.Enter), "Input Enter did not reach the calculator.");
            Require(context.History.Items.Count == 1, "A single input Enter must record exactly one calculation.");
            if (view is StandardView or ScientificView or ProgrammerView) Require(view.CopyText == "5", "Input Enter evaluated the wrong value.");
        });
    }

    private static void PrepareInput(ModeView view)
    {
        if (view is StandardView or ProgrammerView) foreach (var character in "2+3") view.Character(character);
        else if (view is ScientificView) view.Reuse("5");
    }

    private static void TabbedMemoryAction()
    {
        WithForm("Standard", (form, context, view) =>
        {
            PrepareInput(view);
            _ = Command(form, Keys.Tab);
            view.ActiveControl = Named<CalcButton>(view, "Open memory list"); form.ActiveControl = view;
            Require(!Command(form, Keys.Enter), "Tab-focused memory action lost native Enter processing.");
            Require(context.History.Items.Count == 0, "Tab-focused MA Enter evaluated the pending calculation.");
        });
    }

    private static void TypedInputAfterTab()
    {
        WithForm("Standard", (form, context, view) =>
        {
            view.Character('2'); view.Character('+'); _ = Command(form, Keys.Shift | Keys.Tab);
            view.ActiveControl = Named<CalcButton>(view, "Open memory list"); form.ActiveControl = view;
            var key = new KeyPressEventArgs('3'); Invoke(form, "OnKeyPress", key);
            Require(key.Handled, "The calculator did not consume the typed digit.");
            Require(Command(form, Keys.Enter) && context.History.Items.Count == 1 && view.CopyText == "5",
                "Typing after Tab did not restore the normal calculator Enter convention.");
        });
    }

    private static void MouseInputAfterTab()
    {
        WithForm("Standard", (form, context, view) =>
        {
            view.Character('2'); view.Character('+'); _ = Command(form, Keys.Tab);
            var key = Named<CalcButton>(view, "3"); view.ActiveControl = key; form.ActiveControl = view;
            // Invoke the registered mouse-input path without sending physical OS input.
            typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(key,
                [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]);
            typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(key, [EventArgs.Empty]);
            Require(Command(form, Keys.Enter) && context.History.Items.Count == 1 && view.CopyText == "5",
                "Mouse keypad entry after Tab did not preserve Enter as equals.");
        });
    }

    private static void DateSelectionSnapshot()
    {
        WithForm("Dates", (form, _, view) =>
        {
            var end = Descendants(view).OfType<DateInput>().Single(input => input.AccessibleName == "End date");
            end.Editor.Select(5, 2); end.ActiveControl = end.Editor; view.ActiveControl = end; form.ActiveControl = view;
            Require(ReferenceEquals(ActiveLeaf(form), end.Editor), "Nested date activation did not identify End date.");
            var snapshot = Invoke(form, "CaptureInputFocus")!;
            Require(ReferenceEquals(snapshot.GetType().GetProperty("Control")!.GetValue(snapshot), end.Editor), "Focus snapshot replaced End date with the first date field.");
            end.Editor.SelectAll(); RestoreSelection(snapshot);
            Require(end.Editor.SelectionStart == 5 && end.Editor.SelectionLength == 2, "Focus snapshot lost the original date selection.");
        });
    }

    private static void ChangedSelectionSnapshot()
    {
        using var editor = new TextBox { Text = "1234567890" };
        editor.Select(7, 3);
        var snapshotType = typeof(MainForm).GetNestedType("InputFocusSnapshot", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Input focus snapshot is unavailable.");
        var snapshot = Activator.CreateInstance(snapshotType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [editor], null)!;
        editor.Text = "12"; RestoreSelection(snapshot);
        Require(editor.SelectionStart == 2 && editor.SelectionLength == 0, "Selection was not clamped after a field changed while a dialog was open.");
        editor.Dispose(); RestoreSelection(snapshot);
    }

    private static void RemovedFocusFallback()
    {
        WithForm("Dates", (form, _, view) =>
        {
            var end = Descendants(view).OfType<DateInput>().Single(input => input.AccessibleName == "End date");
            var start = Descendants(view).OfType<DateInput>().Single(input => input.AccessibleName == "Start date");
            end.ActiveControl = end.Editor; view.ActiveControl = end; form.ActiveControl = view;
            var snapshot = Invoke(form, "CaptureInputFocus")!;
            var parent = (TableLayoutPanel)end.Parent!; var position = parent.GetPositionFromControl(end);
            parent.Controls.Remove(end);
            try
            {
                start.Editor.Select(2, 0); Invoke(form, "RestoreInputFocus", snapshot);
                Require(start.Editor.SelectionStart == 0 && start.Editor.SelectionLength == start.Editor.TextLength,
                    "A removed invoking control did not use the mode-input fallback.");
            }
            finally { parent.Controls.Add(end, position.Column, position.Row); }
        });
    }

    private static void RestoreSelection(object snapshot) => snapshot.GetType().GetMethod("RestoreSelection")!.Invoke(snapshot, null);
    private static Control? ActiveLeaf(MainForm form) => (Control?)typeof(MainForm).GetMethod("ActiveDescendant", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [form.ActiveControl]);
    private static bool Command(MainForm form, Keys key)
    {
        object?[] args = [Message.Create(form.Handle, 0x0100, (nint)(int)(key & Keys.KeyCode), 0), key];
        return (bool)typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, args)!;
    }
    private static object? Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static void WithForm(string mode, Action<MainForm, WorkspaceContext, ModeView> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterIndependentShared-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var form = new MainForm(new StateStore(directory));
            _ = form.Handle; Invoke(form, "SwitchMode", mode); Handles(form); form.PerformLayout();
            var context = (WorkspaceContext)typeof(MainForm).GetField("context", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var view = (ModeView)typeof(MainForm).GetProperty("Current", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Require(!form.Visible, "Managed routing checks must not show native windows.");
            action(form, context, view);
        }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Require(string.Equals(Path.GetDirectoryName(resolved), temporaryRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("AsterIndependentShared-", StringComparison.Ordinal), "Unexpected temporary verification directory.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
    private static void Handles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) Handles(child); }
    private static T Named<T>(Control control, string name) where T : Control => Descendants(control).OfType<T>().Single(item => item.AccessibleName == name);
    private static IEnumerable<Control> Descendants(Control control) { foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
