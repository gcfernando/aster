// Developer ::> Gehan Fernando
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Modes;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Views;

public sealed class ProgrammerView : ModeView
{
    private readonly ProgrammerCalculator calculator = new();
    private readonly ResultDisplay display = new();
    private readonly Label preview = Ui.Label("", 8);
    private readonly Label bitHint = Ui.Label("Select a bit to toggle it. Groups contain four bits.", 8);
    private readonly SegmentedControl widths;
    private readonly SegmentedControl signs;
    private readonly SegmentedControl workspace = new("Programmer workspace", ["Keypad", "Bit editor"]);
    private readonly SegmentedControl bitRange = new("Bit range", ["Bits 31–0", "Bits 63–32"]);
    private readonly Panel workspaceHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly TableLayoutPanel bitGrid = Ui.Table(1, -1, -1);
    private readonly List<(int Base, string Abbreviation, CalcButton Choice, TextBox Value, CalcButton Copy)> representations = [];
    private readonly List<(CalcButton Button, int Index)> bitButtons = [];
    private readonly List<(CalcButton Button, int Digit)> digitButtons = [];
    private readonly TableLayoutPanel keys;
    private readonly TableLayoutPanel bitPanel = Ui.Table(1, 24, -100, 18);
    private string entry = "0";
    private bool replace = true;
    private bool updating;
    private bool incomplete;
    private bool hasRightOperand;
    private bool explicitLeadingSignEdit;
    private IntegerOperation? pending;
    private BigInteger left;
    private string expression = "Input DEC";
    private string? error;
    private string? notice;
    private int numberBase = 10;
    private int NumberBase => numberBase;

    public ProgrammerView(WorkspaceContext context) : base(context)
    {
        AccessibleName = "Programmer calculator";
        var layout = Ui.Table(1, 18, -15, 32, -30, 28, -55);
        preview.AutoEllipsis = true; preview.AccessibleName = "Programmer expression and integer model";
        layout.Controls.Add(preview, 0, 0); layout.Controls.Add(display, 0, 1);
        var options = Ui.Table(2, -100);
        widths = new SegmentedControl("Integer width", ["8 bit", "16 bit", "32 bit", "64 bit"]) { SelectedIndex = 3, Margin = Padding.Empty };
        signs = new SegmentedControl("Integer interpretation", ["Signed", "Unsigned"]) { Margin = Padding.Empty };
        widths.AccessibleDescription = "Integers are masked to the selected width. Overflow wraps modulo 2 to this width. Narrowing discards high bits; widening sign-extends signed negative values.";
        signs.AccessibleDescription = "Signed uses two's complement; the highest bit has negative weight. Unsigned treats all bits as non-negative. Right shift is arithmetic when signed and logical when unsigned.";
        options.Controls.Add(widths, 0, 0); options.Controls.Add(signs, 1, 0); layout.Controls.Add(options, 0, 2);
        var conversions = Ui.Table(1, -22, -22, -22, -34);
        var baseNames = new[] { (10, "DEC", "Decimal"), (16, "HEX", "Hexadecimal"), (8, "OCT", "Octal") , (2, "BIN", "Binary") };
        for (var index = 0; index < baseNames.Length; index++)
        {
            var (radix, abbreviation, description) = baseNames[index];
            var row = Ui.Table(3, -100); row.ColumnStyles.Clear();
            row.ColumnStyles.Add(new(SizeType.Absolute, 76)); row.ColumnStyles.Add(new(SizeType.Percent, 100)); row.ColumnStyles.Add(new(SizeType.Absolute, 30));
            var choice = Ui.Button(abbreviation, () => SelectBase(radix, true), ButtonTone.Quiet, name: "Use " + description + " input");
            choice.AccessibleDescription = $"Select {description}, base {radix}, as the editable input. Shortcut Alt+{index + 1}. Other rows synchronize automatically.";
            choice.Margin = new(1); Ui.SetFont(choice, 9, FontStyle.Bold);
            var value = new RepresentationInput { AccessibleName = description + " representation, calculated", ReadOnly = true, Multiline = true,
                WordWrap = true, ScrollBars = ScrollBars.None, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill,
                Margin = new(3, 0, 3, 0), TabStop = false, MaxLength = 128 };
            Ui.SetFont(value, 9, family: "Consolas");
            value.TextChanged += (_, _) => EditRepresentation(radix, value);
            value.Enter += (_, _) =>
            {
                if (value.ReadOnly) return;
                explicitLeadingSignEdit = false;
                if (replace) value.SelectAll();
                Context.Status($"Editing {description}, base {radix}. Enter applies a pending operation; other rows show the same integer.");
            };
            value.KeyDown += (_, eventArgs) => { if (eventArgs.KeyCode == Keys.Home) explicitLeadingSignEdit = true; };
            value.KeyPress += (_, eventArgs) =>
            {
                if (value.ReadOnly) return;
                var character = eventArgs.KeyChar;
                if ((character is '+' or '-') && value.SelectionStart == 0 && (!replace || explicitLeadingSignEdit || value.TextLength == 0 || value.SelectionLength == value.TextLength)) return;
                var operation = character switch { '+' => "+", '-' => "−", '*' => "×", '/' => "÷", '%' => "mod", '=' => "=", _ => null };
                if (operation is null) return;
                eventArgs.Handled = true; Execute(() => Press(operation)); if (replace) value.SelectAll();
            };
            var copy = Ui.Button("", () => CopyRepresentation(radix, description), ButtonTone.Quiet, IconKind.Copy, "Copy " + description + " value");
            copy.AccessibleDescription = $"Copy the current {description} value without display grouping."; copy.Margin = new(1);
            row.Controls.Add(choice, 0, 0); row.Controls.Add(value, 1, 0); row.Controls.Add(copy, 2, 0);
            representations.Add((radix, abbreviation, choice, value, copy)); conversions.Controls.Add(row, 0, index);
        }
        layout.Controls.Add(conversions, 0, 3);
        workspace.Margin = Padding.Empty;
        workspace.AccessibleDescription = "Choose the calculator keypad or the bit editor. Both edit the same exact integer; switching keeps its value and pending operation.";
        layout.Controls.Add(workspace, 0, 4); layout.Controls.Add(workspaceHost, 0, 5);
        bitRange.Margin = Padding.Empty;
        bitRange.AccessibleDescription = "All 64 bits are available in two directly selectable groups of 32. Smaller widths use the low group.";
        bitHint.AutoEllipsis = true; bitHint.AccessibleName = "Bit position and place value";
        bitPanel.Controls.Add(bitRange, 0, 0); bitPanel.Controls.Add(bitGrid, 0, 1); bitPanel.Controls.Add(bitHint, 0, 2);
        keys = Ui.Table(6, -1, -1, -1, -1, -1);
        string[][] rows = [
            ["AND", "OR", "XOR", "NOT", "<<", ">>"],
            ["A", "B", "7", "8", "9", "÷"],
            ["C", "D", "4", "5", "6", "×"],
            ["E", "F", "1", "2", "3", "−"],
            ["Clear", "⌫", "0", "mod", "=", "+"]
        ];
        for (var row = 0; row < rows.Length; row++) for (var column = 0; column < 6; column++)
        {
            var label = rows[row][column];
            var digit = label.Length == 1 ? "0123456789ABCDEF".IndexOf(label, StringComparison.Ordinal) : -1;
            var button = Ui.Button(label == "⌫" ? "" : label, () => Execute(() => Press(label)),
                label == "=" ? ButtonTone.Accent : digit >= 0 ? ButtonTone.Number : ButtonTone.Operator,
                label == "⌫" ? IconKind.Backspace : IconKind.None, label == "⌫" ? "Backspace" : label);
            button.Margin = new(1); button.AccessibleDescription = OperationHelp(label);
            Ui.SetFont(button, label.Length > 2 ? 10 : 15);
            if (digit >= 0) digitButtons.Add((button, digit)); keys.Controls.Add(button, column, row);
        }
        workspaceHost.Controls.Add(keys); Controls.Add(layout);
        workspace.SelectedIndexChanged += (_, _) =>
        {
            workspaceHost.Controls.Clear(); workspaceHost.Controls.Add(workspace.SelectedIndex == 0 ? keys : bitPanel);
            Ui.AttachTooltips(workspaceHost);
            Context.Status(workspace.SelectedIndex == 0 ? ModelDescription : "Bit editor: choose a 32-bit range, then click or focus a bit and press Space. Four-bit groups have visible gaps.");
        };
        bitRange.SelectedIndexChanged += (_, _) => { BuildBits(); UpdateDisplay(); };
        widths.SelectedIndexChanged += (_, _) => Execute(() =>
        {
            calculator.SetWidth(new[] { 8, 16, 32, 64 }[widths.SelectedIndex]);
            if (calculator.Width != 64) bitRange.SelectedIndex = 0;
            ResetInputState(); BuildBits(); notice = ModelDescription;
        });
        signs.SelectedIndexChanged += (_, _) => Execute(() => { calculator.Signed = signs.SelectedIndex == 0; ResetInputState(); notice = ModelDescription; });
        void Adapt()
        {
            var scale = DeviceDpi / 96f; var compact = ClientSize.Height / scale < 360;
            // Keep five keypad rows usable in a 242-DIP effective workspace.
            // The input rows retain enough height for their text and copy targets.
            layout.RowStyles[0].Height = (compact ? 16 : 18) * scale;
            layout.RowStyles[1].SizeType = compact ? SizeType.Absolute : SizeType.Percent;
            layout.RowStyles[1].Height = compact ? 0 : 15; display.Visible = !compact;
            layout.RowStyles[2].Height = (compact ? 24 : 32) * scale;
            layout.RowStyles[3].SizeType = compact ? SizeType.Absolute : SizeType.Percent;
            layout.RowStyles[3].Height = compact ? 86 * scale : 30;
            layout.RowStyles[4].Height = (compact ? 20 : 28) * scale;
            bitPanel.RowStyles[0].Height = (compact ? 22 : 28) * scale;
            bitPanel.RowStyles[2].Height = (compact ? 16 : 18) * scale;
            // At narrow compact sizes the grouped 64-bit representation needs two complete lines.
            foreach (var representation in representations)
                Ui.SetFont(representation.Value, compact ? representation.Base == 2 ? 8 : 9 : 11, family: "Consolas");
        }
        SizeChanged += (_, _) => Adapt(); Adapt(); BuildBits(); UpdateDisplay(); FinishLayout();
    }

    private string ModelDescription => $"{calculator.Width}-bit {(calculator.Signed ? "signed two's complement" : "unsigned")} · overflow wraps modulo 2^{calculator.Width} · >> {(calculator.Signed ? "arithmetic (sign extends)" : "logical (zero fills)")}";

    private void SelectBase(int radix, bool focus = false)
    {
        explicitLeadingSignEdit = false;
        numberBase = radix; entry = calculator.Format(radix); replace = true; error = null; incomplete = false;
        if (pending is null) expression = "Input " + BaseName(radix);
        UpdateDisplay(); Context.Status($"{BaseName(radix)} is editable. Alt+1/2/3/4 selects DEC/HEX/OCT/BIN. {ModelDescription}");
        if (focus) representations.Single(row => row.Base == radix).Value.Focus();
    }
    private static string BaseName(int radix) => radix switch { 10 => "DEC", 16 => "HEX", 8 => "OCT", _ => "BIN" };
    private static string NormalizeEntry(string text) => text.Replace(" ", "", StringComparison.Ordinal).Replace('−', '-');
    private void EditRepresentation(int radix, TextBox field)
    {
        if (updating || radix != NumberBase) return;
        explicitLeadingSignEdit = false;
        entry = NormalizeEntry(field.Text); replace = false;
        if (entry is "" or "+" or "-") { incomplete = true; hasRightOperand = false; error = null; UpdateDisplay(); Context.Status("Complete the integer in the active input row."); return; }
        var selection = field.SelectionStart;
        Execute(() => { calculator.Parse(entry, NumberBase); incomplete = false; hasRightOperand = true; NoticeInputWrap(entry); });
        field.SelectionStart = Math.Min(selection, field.TextLength);
    }
    private void ValidateInput()
    {
        if (!replace) calculator.Parse(entry, NumberBase);
        incomplete = false;
    }
    private void ResetInputState() { pending = null; entry = calculator.Format(NumberBase); replace = true; error = null; incomplete = false; hasRightOperand = false; expression = "Input " + BaseName(NumberBase); }
    private void CopyRepresentation(int radix, string description)
    {
        if (error is not null || incomplete) { Context.Status("Complete a valid integer before copying its representations."); return; }
        try { Clipboard.SetText(calculator.Format(radix)); Context.Status("Copied " + description.ToLowerInvariant() + " value (base " + radix + ")."); }
        catch (ExternalException) { Context.Status("The clipboard is busy. Try again."); }
    }
    public override string CopyText => error is null && !incomplete ? calculator.Format(NumberBase) : "";
    public override void FocusInput() => representations.Single(row => row.Base == NumberBase).Value.Focus();
    public override void Reuse(string value) => Execute(() => { calculator.Parse(NormalizeEntry(value), NumberBase); ResetInputState(); });
    public void ReuseDecimalResult(string value) => Execute(() => { calculator.ParseDecimalResult(value); ResetInputState(); });
    private void Execute(Action action)
    {
        notice = null; explicitLeadingSignEdit = false;
        try { action(); error = null; Context.Status(notice ?? ModelDescription); }
        catch (CalculationException exception) { error = exception.Message; Context.Status(exception.Message); }
        UpdateDisplay();
    }
    private void NoticeInputWrap(string text)
    {
        var raw = BigInteger.Zero; var negative = text.StartsWith('-');
        foreach (var character in text.TrimStart('+', '-').ToUpperInvariant()) raw = raw * NumberBase + "0123456789ABCDEF".IndexOf(character);
        if (negative) raw = -raw;
        if (raw != calculator.Value) notice = $"Input interpreted as {calculator.Value} using {ModelDescription}.";
    }
    private void Press(string label)
    {
        if (label.Length == 1 && "0123456789ABCDEF".Contains(label, StringComparison.Ordinal))
        {
            var proposed = replace || entry == "0" ? label : entry + label;
            calculator.Parse(proposed, NumberBase); entry = proposed; replace = false; incomplete = false; hasRightOperand = true; NoticeInputWrap(entry); return;
        }
        if (label == "Clear") { calculator.SetValue(0); ResetInputState(); return; }
        if (label == "⌫")
        {
            if (!replace) { entry = entry.Length > 1 ? entry[..^1] : "0"; if (entry is "-" or "+" or "") entry = "0"; calculator.Parse(entry, NumberBase); incomplete = false; hasRightOperand = true; }
            return;
        }
        ValidateInput();
        if (label == "NOT") { expression = $"NOT ({calculator.Value}) [DEC operands]"; calculator.Not(); entry = calculator.Format(NumberBase); replace = true; hasRightOperand = true; return; }
        if (label == "=") { Evaluate(); return; }
        var operation = label switch
        {
            "+" => IntegerOperation.Add, "−" => IntegerOperation.Subtract, "×" => IntegerOperation.Multiply, "÷" => IntegerOperation.Divide,
            "mod" => IntegerOperation.Remainder, "AND" => IntegerOperation.And, "OR" => IntegerOperation.Or, "XOR" => IntegerOperation.Xor,
            "<<" => IntegerOperation.LeftShift, ">>" => IntegerOperation.RightShift, _ => throw new CalculationException("Unknown integer operation.")
        };
        if (pending.HasValue && hasRightOperand) Evaluate();
        left = calculator.Value; pending = operation; replace = true; hasRightOperand = false;
        expression = $"{left} {OperationSymbol(operation)} … [DEC operands]";
    }
    // Record a result only while a binary operation is pending.
    private void Evaluate()
    {
        if (pending is null) return;
        var operation = pending.Value; var right = calculator.Value;
        calculator.SetValue(left);
        try { calculator.Apply(operation, right); }
        catch { calculator.SetValue(right); throw; }
        var raw = operation switch
        {
            IntegerOperation.Add => left + right, IntegerOperation.Subtract => left - right, IntegerOperation.Multiply => left * right,
            IntegerOperation.Divide => left / right, IntegerOperation.LeftShift => left << (int)right, _ => calculator.Value
        };
        var wrapped = raw != calculator.Value;
        expression = $"{left} {OperationSymbol(operation)} {right} = {calculator.Value} [DEC]{(wrapped ? " · wrapped" : "")}";
        if (wrapped) notice = $"Overflow wrapped to {calculator.Value}. {ModelDescription}";
        Context.Record("Programmer", expression, calculator.Format(10));
        pending = null; replace = true; hasRightOperand = false; entry = calculator.Format(NumberBase);
    }
    private void BuildBits()
    {
        bitGrid.SuspendLayout();
        while (bitGrid.Controls.Count > 0) { var child = bitGrid.Controls[0]; bitGrid.Controls.RemoveAt(0); child.Dispose(); }
        bitButtons.Clear(); bitGrid.RowStyles.Clear();
        var count = Math.Min(32, calculator.Width); var rows = (count + 15) / 16;
        bitGrid.RowCount = rows;
        for (var row = 0; row < rows; row++)
        {
            bitGrid.RowStyles.Add(new(SizeType.Percent, 100f / rows));
            var cells = Math.Min(16, count - row * 16);
            var first = (calculator.Width == 64 ? bitRange.SelectedIndex * 32 : 0) + count - row * 16 - 1;
            var line = Ui.Table(cells + 1, -100); line.ColumnStyles.Clear(); line.ColumnStyles.Add(new(SizeType.Absolute, 42));
            for (var column = 0; column < cells; column++) line.ColumnStyles.Add(new(SizeType.Percent, 100f / cells));
            var marker = Ui.Label($"{first}–{first - cells + 1}", 8); marker.AccessibleName = "Bit positions " + marker.Text;
            line.Controls.Add(marker, 0, 0);
            for (var column = 0; column < cells; column++)
            {
                var index = first - column;
                var button = Ui.Button("0", () => Execute(() =>
                {
                    calculator.ToggleBit(index); ResetInputState(); expression = $"Toggled bit {index} · value {calculator.Value} [DEC]";
                }), ButtonTone.Quiet, name: $"Toggle bit {index}");
                button.Margin = new(column > 0 && column % 4 == 0 ? 4 : 1, 1, 1, 1); Ui.SetFont(button, 10, family: "Consolas");
                button.MouseEnter += (_, _) => ShowBitHint(index); button.Enter += (_, _) => ShowBitHint(index);
                bitButtons.Add((button, index)); line.Controls.Add(button, column + 1, 0);
            }
            bitGrid.Controls.Add(line, 0, row);
        }
        bitRange.Enabled = calculator.Width == 64; bitGrid.ResumeLayout(); Ui.Theme(bitPanel, Palette); Ui.AttachTooltips(bitPanel);
    }
    private void ShowBitHint(int index)
    {
        var raw = BigInteger.One << index;
        bitHint.Text = $"Bit {index} · 2^{index} = {raw}{(calculator.Signed && index == calculator.Width - 1 ? " · sign bit has negative weight" : "")} · Space toggles";
        bitHint.AccessibleDescription = bitHint.Text; Context.Status(bitHint.Text);
    }
    private void UpdateDisplay()
    {
        updating = true;
        try
        {
            var pendingText = pending is null ? expression : $"{left} {OperationSymbol(pending.Value)} {(hasRightOperand ? calculator.Value.ToString(CultureInfo.InvariantCulture) : "…")} [DEC operands]";
            var inputState = error ?? (incomplete ? "Complete the integer in the active input row" : pendingText);
            preview.Text = $"{calculator.Width}-bit {(calculator.Signed ? "signed" : "unsigned")} · wraps · >> {(calculator.Signed ? "arithmetic" : "logical")} | {inputState}";
            preview.AccessibleDescription = inputState + ". " + ModelDescription;
            preview.ForeColor = error is null ? Palette.Text : Palette.Error;
            display.Set(pendingText, error ?? calculator.Format(NumberBase), error is not null);
            foreach (var row in representations)
            {
                var active = row.Base == NumberBase;
                var text = active && !replace ? entry : calculator.Format(row.Base);
                if (row.Base == 2 && (replace || !active))
                {
                    var padded = text.PadLeft(calculator.Width, '0');
                    text = string.Join(" ", Enumerable.Range(0, (padded.Length + 3) / 4).Select(index => padded.Substring(index * 4, Math.Min(4, padded.Length - index * 4))));
                }
                row.Value.ReadOnly = !active; row.Value.TabStop = active; row.Value.Cursor = active ? Cursors.IBeam : Cursors.Arrow;
                if (row.Value.Text != text) row.Value.Text = text;
                row.Value.AccessibleDescription = active ? $"Editable {BaseName(row.Base)} input. {(error is null ? "" : error + " ")}Valid digits: {"0123456789ABCDEF"[..row.Base]}. Spaces separate binary groups. {ModelDescription}" : "Read-only synchronized value. Select the base name to make it editable. " + ModelDescription;
                row.Choice.Text = row.Abbreviation + (active ? " input" : ""); row.Choice.Selected = active; row.Choice.Invalidate();
                row.Value.BackColor = active ? Palette.Key : Palette.Surface; row.Value.ForeColor = active ? error is null ? Palette.Accent : Palette.Error : Palette.Text;
                row.Copy.Enabled = error is null && !incomplete;
            }
            foreach (var (button, index) in bitButtons)
            {
                button.Text = (calculator.Bits & (BigInteger.One << index)) != 0 ? "1" : "0";
                button.Selected = button.Text == "1";
                var place = BigInteger.One << index;
                button.AccessibleDescription = $"Bit {index} is {button.Text}. Place value 2^{index} = {place}. " +
                    (calculator.Signed && index == calculator.Width - 1 ? $"Sign bit: signed weight is {-place}. " : "") + "Click or press Space to toggle. Four-bit groups have a visible gap.";
                button.Invalidate();
            }
            foreach (var (button, digit) in digitButtons) button.Enabled = digit < NumberBase;
        }
        finally { updating = false; }
        Ui.AttachTooltips(this);
    }
    public override void ApplyTheme(Palette palette) { base.ApplyTheme(palette); UpdateDisplay(); Ui.Theme(bitPanel, palette); }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The inactive workspace is detached from the control tree but remains owned by this view.
            if (keys.Parent is null) keys.Dispose();
            if (bitPanel.Parent is null) bitPanel.Dispose();
        }
        base.Dispose(disposing);
    }
    private static string OperationSymbol(IntegerOperation operation) => operation switch
    {
        IntegerOperation.Add => "+", IntegerOperation.Subtract => "−", IntegerOperation.Multiply => "×", IntegerOperation.Divide => "÷",
        IntegerOperation.LeftShift => "<<", IntegerOperation.RightShift => ">>", IntegerOperation.Remainder => "mod",
        IntegerOperation.And => "AND", IntegerOperation.Or => "OR", IntegerOperation.Xor => "XOR", _ => operation.ToString()
    };
    private sealed class RepresentationInput : TextBox
    {
        private bool updatingViewport;
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RefreshViewport(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); RefreshViewport(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RefreshViewport(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); RefreshViewport(); }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); RefreshViewport(); }
        private void RefreshViewport()
        {
            if (updatingViewport || !IsHandleCreated || IsDisposed || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            updatingViewport = true;
            try
            {
                // Native multiline EDIT can retain its initial line rectangle after DPI/layout
                // changes. Keep the complete current viewport, without changing the selection.
                var rectangle = new EditRectangle { Right = ClientSize.Width, Bottom = ClientSize.Height };
                _ = SetEditRectangle(Handle, 0x00b3 /* EM_SETRECT */, 0, ref rectangle);
            }
            finally { updatingViewport = false; }
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct EditRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SetEditRectangle(nint window, uint message, nint parameter, ref EditRectangle rectangle);
    private string OperationHelp(string label) => label switch
    {
        ">>" => "Right shift by a count from 0 through width minus 1. Signed mode fills with the sign bit (arithmetic); unsigned fills with zero (logical).",
        "<<" => "Left shift by a count from 0 through width minus 1; high bits are discarded at the selected width.",
        "NOT" => "Invert every bit within the selected integer width.",
        "AND" or "OR" or "XOR" => "Combine the current raw bits with the next integer using " + label + ". The pending expression shows operand order.",
        "mod" => "Integer remainder: the first operand divided by the second. The divisor must be non-zero; the remainder has the dividend's sign.",
        "Clear" => "Clear the current integer, pending operation and error.",
        "⌫" => "Remove the last entered digit. The active input field also supports native selection and text editing.",
        _ => "Enter " + label + " using the active base. " + ModelDescription
    };
    public override bool Character(char character)
    {
        var digit = char.ToUpperInvariant(character);
        if ("0123456789ABCDEF".Contains(digit)) { Execute(() => Press(digit.ToString())); return true; }
        var label = character switch { '+' => "+", '-' => "−", '*' => "×", '/' => "÷", '%' => "mod", '=' => "=", _ => null };
        if (label is null) return false; Execute(() => Press(label)); return true;
    }
    public override bool Command(Keys key)
    {
        var radix = key switch { (Keys.Alt | Keys.D1) => 10, (Keys.Alt | Keys.D2) => 16, (Keys.Alt | Keys.D3) => 8, (Keys.Alt | Keys.D4) => 2, _ => 0 };
        if (radix != 0) { SelectBase(radix, true); return true; }
        var label = key switch { Keys.Enter => "=", Keys.Escape or Keys.Delete => "Clear", Keys.Back => "⌫", _ => null };
        if (label is null) return false; Execute(() => Press(label)); return true;
    }
}
