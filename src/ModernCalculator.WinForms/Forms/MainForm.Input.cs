// Developer ::> Gehan Fernando
using System.Globalization;
using System.Runtime.InteropServices;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.History;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.WinForms.Forms;

public sealed partial class MainForm
{
    private bool keyboardActionNavigation;
    private static Control? ActiveDescendant(Control? control)
    {
        while (control is ContainerControl container && container.ActiveControl is not null) control = container.ActiveControl;
        return control;
    }
    private static bool IsTextEditor(Control? control) => ActiveDescendant(control) is TextBoxBase { ReadOnly: false } or NumericUpDown;
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var editing = IsTextEditor(ActiveControl);
        if (keyData is Keys.Tab or (Keys.Shift | Keys.Tab)) keyboardActionNavigation = true;
        if (keyData == Keys.F1) { ShowHelp(); return true; }
        // Explicit keyboard navigation makes Enter activate the focused action.
        // Typed/mouse calculator input keeps Enter as equals even when a key retains focus.
        if (keyData == Keys.Enter && ActiveDescendant(ActiveControl) is ButtonBase action &&
            (!Current.Contains(action) || keyboardActionNavigation))
            return base.ProcessCmdKey(ref msg, keyData);
        if (keyData is (Keys.Control | Keys.Z) or (Keys.Control | Keys.Y) && Current is StandardView && !historySearch.ContainsFocus && Current.Command(keyData)) return true;
        if ((keyData & Keys.Modifiers) == Keys.Alt && Current.Command(keyData)) return true;
        if (keyData == (Keys.Control | Keys.C) && !HasTextSelection(ActiveControl)) { CopyResult(); return true; }
        if (keyData == (Keys.Control | Keys.V) && !editing) { PasteResult(); return true; }
        if (keyData == (Keys.Control | Keys.H)) { SetDrawer(!drawerVisible); return true; }
        var modeModifier = context.Settings.ModeShortcuts == Infrastructure.Settings.ModeShortcutPreference.ControlAltNumbers ? Keys.Control | Keys.Alt : Keys.Control;
        if ((keyData & Keys.Modifiers) == modeModifier && (keyData & Keys.KeyCode) is >= Keys.D1 and <= Keys.D5)
        { SwitchMode(new[] { "Standard", "Scientific", "Programmer", "Converter", "Dates" }[(int)(keyData & Keys.KeyCode) - (int)Keys.D1]); return true; }
        var searchFocused = historySearch.ContainsFocus;
        if (!searchFocused && (keyData is Keys.Enter or Keys.Escape || !editing && keyData is Keys.Back or Keys.Delete or Keys.F9) && Current.Command(keyData))
        { if (keyData == Keys.Enter) keyboardActionNavigation = false; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!IsTextEditor(ActiveControl) && !e.Handled && Current.Character(e.KeyChar))
        { e.Handled = true; keyboardActionNavigation = false; }
        base.OnKeyPress(e);
    }
    private void ResetActionNavigation(object? sender, MouseEventArgs e) => keyboardActionNavigation = false;
    private void CopyResult()
    {
        if (Current.CopyText.Length == 0) { context.Status("Complete a calculation before copying a result."); return; }
        CopyText(Current.CopyText);
    }
    private static bool HasTextSelection(Control? control)
    {
        return ActiveDescendant(control) is TextBoxBase box && box.SelectionLength > 0;
    }
    private InputFocusSnapshot CaptureInputFocus() => new(ActiveDescendant(ActiveControl));
    // Restore focus only while the original control still belongs to the form and can accept it.
    private void RestoreInputFocus(InputFocusSnapshot snapshot)
    {
        if (snapshot.Control is { IsDisposed: false, Disposing: false } control && Contains(control) &&
            control.Visible && control.Enabled && control.CanSelect && control.Focus())
        {
            snapshot.RestoreSelection();
            return;
        }
        Current.FocusInput();
    }
    private sealed class InputFocusSnapshot(Control? control)
    {
        public Control? Control { get; } = control;
        private readonly int selectionStart = control is TextBoxBase editor ? editor.SelectionStart : 0;
        private readonly int selectionLength = control is TextBoxBase editor ? editor.SelectionLength : 0;
        public void RestoreSelection()
        {
            if (Control is not TextBoxBase { IsDisposed: false, Disposing: false } editor) return;
            var start = Math.Min(selectionStart, editor.TextLength);
            editor.Select(start, Math.Min(selectionLength, editor.TextLength - start));
        }
    }
    private void CopyText(string text)
    {
        try { Clipboard.SetText(text); context.Status("Copied to clipboard."); }
        catch (ExternalException) { context.Status("The clipboard is busy. Try copying again."); }
    }
    private void PasteResult()
    {
        try
        {
            if (!Clipboard.ContainsText()) { context.Status("The clipboard does not contain text."); return; }
            var text = Clipboard.GetText().Trim();
            var maximum = Current is StandardView ? NumberText.MaximumExactInputLength : NumberText.MaximumInputLength;
            if (text.Length > maximum) { context.Status($"Pasted input is limited to {maximum} characters in this mode."); return; }
            Current.Reuse(text);
        }
        catch (ExternalException) { context.Status("The clipboard is busy. Try pasting again."); }
    }
    private string LocalResult(HistoryItem item)
    {
        return item.LocalizedResult(CultureInfo.CurrentCulture);
    }
    // Reuse stored numeric text before display localization can change its interpretation.
    private void ReuseHistory(HistoryItem item)
    {
        if (Current is StandardView standard) standard.ReuseStoredResult(item.Result);
        else if (Current is ProgrammerView programmer) programmer.ReuseDecimalResult(item.Result);
        else if (Current is ScientificView scientific) scientific.ReuseStoredResult(item.Result);
        else Current.Reuse(LocalResult(item));
        Current.FocusInput();
    }
    private void ReuseMemory(BigDecimal value)
    {
        if (Current is StandardView standard) standard.ReuseStoredResult(value.ToString());
        else if (Current is ProgrammerView programmer) programmer.ReuseDecimalResult(value.ToString());
        else if (Current is ScientificView scientific) scientific.ReuseExact(value);
        else Current.Reuse(value.ToString(CultureInfo.CurrentCulture));
    }
}
