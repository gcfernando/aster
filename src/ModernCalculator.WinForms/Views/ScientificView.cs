using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Expressions;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Views;

public sealed class ScientificView : ModeView
{
    private readonly ExpressionEvaluator evaluator = new();
    private readonly TextBox expression = Ui.Input("Scientific expression");
    private readonly ResultDisplay display = new();
    private readonly SegmentedControl angles;
    private readonly SegmentedControl functionGroup;
    private readonly Label angleLabel = Ui.Label("Angle mode", 8, true);
    private readonly Label categoryLabel = Ui.Label("Function category", 8, true);
    private readonly TableLayoutPanel functionBank = Ui.Table(6, -1, -1);
    private readonly Label inputHint = Ui.Label("Enter an expression; Enter calculates. Ans recalls the last successful result.", 8);
    private double? answer;
    private double result;
    private bool hasResult;
    private bool afterEquals;
    private bool? compactLayout;
    private string? error;
    public ScientificView(WorkspaceContext context) : base(context)
    {
        AccessibleName = "Scientific calculator";
        var layout = Ui.Table(1, -20, 32, 18, 36, -22, -58, 34);
        var resultRow = Ui.Table(2, -100);
        resultRow.ColumnStyles.Clear(); resultRow.ColumnStyles.Add(new(SizeType.Absolute, 52)); resultRow.ColumnStyles.Add(new(SizeType.Percent, 100));
        var resultLabel = Ui.Label("Result", 9, true); resultLabel.AccessibleName = "Scientific result label";
        resultRow.Controls.Add(resultLabel, 0, 0); resultRow.Controls.Add(display, 1, 0); layout.Controls.Add(resultRow, 0, 0);
        var editorRow = Ui.Table(2, -100);
        editorRow.ColumnStyles.Clear(); editorRow.ColumnStyles.Add(new(SizeType.Absolute, 92)); editorRow.ColumnStyles.Add(new(SizeType.Percent, 100));
        var editorLabel = Ui.Label("Expression", 9, true); editorLabel.AccessibleName = "Scientific expression label";
        editorRow.Controls.Add(editorLabel, 0, 0);
        Ui.SetFont(expression, 12, family: "Consolas"); expression.BorderStyle = BorderStyle.FixedSingle; expression.Margin = new(3, 1, 3, 1); expression.MaxLength = ExpressionEvaluator.MaximumLength;
        expression.AccessibleDescription = "Editable scientific expression. Select text to replace it; Ctrl+Z undoes text edits. Enter evaluates. Ans recalls the last successful result.";
        expression.TextChanged += (_, _) => InvalidateResult();
        expression.KeyPress += (_, e) => { if (e.KeyChar == '=') { e.Handled = true; Calculate(); } };
        editorRow.Controls.Add(expression, 1, 0); layout.Controls.Add(editorRow, 0, 1);
        inputHint.AutoEllipsis = true; inputHint.AccessibleName = "Scientific syntax help"; layout.Controls.Add(inputHint, 0, 2);
        var bar = Ui.Table(2, -100);
        bar.ColumnStyles[0].Width = 34; bar.ColumnStyles[1].Width = 66;
        angles = new SegmentedControl("Angle unit", ["DEG", "RAD"]) { Margin = Padding.Empty };
        angles.AccessibleDescription = "Angle mode applies to trigonometric inputs and inverse-trigonometric results. Hyperbolic functions are dimensionless.";
        functionGroup = new SegmentedControl("Scientific function group", ["Powers & logs", "Trigonometry", "Hyperbolic"]) { Margin = Padding.Empty };
        bar.Controls.Add(LabeledSelector(angleLabel, 58, angles), 0, 0);
        bar.Controls.Add(LabeledSelector(categoryLabel, 56, functionGroup), 1, 0);
        layout.Controls.Add(bar, 0, 3);
        layout.Controls.Add(functionBank, 0, 4);
        var keys = Ui.Table(6, -1, -1, -1, -1, -1);
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ",";
        string[][] labels = [
            ["π", "e", "7", "8", "9", "÷"],
            ["Ans", "|x|", "4", "5", "6", "×"],
            ["C", "⌫", "1", "2", "3", "−"],
            ["1/x", "EXP", "0", ".", separator, "+"],
            ["(", ")", "10ˣ", "eˣ", "+/−", "="]
        ];
        for (var row = 0; row < labels.Length; row++)
            for (var col = 0; col < 6; col++)
            {
                var label = labels[row][col];
                var tone = label == "=" ? ButtonTone.Accent : char.IsAsciiDigit(label[0]) && label.Length == 1 ? ButtonTone.Number
                    : label is "+" or "−" or "×" or "÷" ? ButtonTone.Operator : ButtonTone.Normal;
                var button = Ui.Button(label == "⌫" ? "" : label == "EXP" ? "×10ⁿ" : label, () => Press(label), tone,
                    label == "⌫" ? IconKind.Backspace : IconKind.None, ScientificName(label));
                button.Margin = new(1); button.AccessibleDescription = FunctionHelp(label);
                Ui.SetFont(button, label.Length > 3 ? 10 : 13);
                keys.Controls.Add(button, col, row);
            }
        layout.Controls.Add(keys, 0, 5);
        layout.Controls.Add(MemoryControls(CurrentExact, ReuseExact), 0, 6);
        functionGroup.SelectedIndexChanged += (_, _) => BuildFunctionBank();
        angles.SelectedIndexChanged += (_, _) => InvalidateResult();
        Controls.Add(layout); BuildFunctionBank(); RefreshFormatting();
        FitMemoryRow(layout, 6);
        expression.PlaceholderText = "Enter an expression";
        void AdaptRows()
        {
            var compact = ClientSize.Height * 96f / DeviceDpi < 360;
            if (compactLayout == compact) return;
            compactLayout = compact; var scale = DeviceDpi / 96f;
            layout.RowStyles[0].Height = compact ? 14 : 20;
            layout.RowStyles[4].Height = compact ? 28 : 22;
            layout.RowStyles[5].Height = 58;
            layout.RowStyles[1].Height = (compact ? 24 : 32) * scale;
            layout.RowStyles[2].Height = (compact ? 0 : 18) * scale; inputHint.Visible = !compact;
            layout.RowStyles[3].Height = (compact ? 24 : 36) * scale;
            angleLabel.Text = compact ? "Angle" : "Angle mode";
            categoryLabel.Text = compact ? "Category" : "Function category";
            Ui.SetFont(expression, compact ? 9 : 12, family: "Consolas");
        }
        SizeChanged += (_, _) => AdaptRows(); AdaptRows();
        FinishLayout();
    }

    private static string ScientificName(string label) => label switch
    {
        "⌫" => "Backspace", "xʸ" => "Power", "EXP" => "Enter scientific exponent", "mod" => "Remainder",
        "root" => "Root with chosen degree", "logbase" => "Logarithm with chosen base",
        "Ans" => "Recall last successful answer", "ⁿ√x" => "Nth root", "logₐ(x)" => "Logarithm with chosen base", "π" => "Pi", "√x" => "Square root", "∛x" => "Cube root", "10ˣ" => "Ten to the power", "n!" => "Factorial", _ => label
    };
    private static Control LabeledSelector(Label label, int labelWidth, SegmentedControl selector)
    {
        var group = Ui.Table(2, -100);
        label.Margin = Padding.Empty;
        group.ColumnStyles.Clear(); group.ColumnStyles.Add(new(SizeType.Absolute, labelWidth)); group.ColumnStyles.Add(new(SizeType.Percent, 100));
        label.AccessibleName = label.Text + " label"; label.AccessibleDescription = label.Text;
        group.Controls.Add(label, 0, 0); group.Controls.Add(selector, 1, 0);
        return group;
    }
    private static string FunctionHelp(string label) => label switch
    {
        "root" => $"Nth root: root(value{ArgumentSeparator} degree). The value comes first; the root degree comes second. Odd integer roots accept negative values.",
        "logbase" => $"Logarithm: logbase(value{ArgumentSeparator} base). The value comes first; the base comes second. Value and base must be positive; base cannot be 1.",
        "mod" => $"Remainder: mod(dividend{ArgumentSeparator} divisor). The first value is divided by the second; the divisor cannot be zero. This is not percentage.",
        "EXP" => "Scientific exponent entry: 2E3 means 2 times 10 cubed. Edit the selected exponent. This differs from e to the power x.",
        "eˣ" => "Exponential function exp(x): Euler's number e raised to x. Use the exponent-entry key for powers of 10 in a number.",
        "Ans" => "Insert Ans, the last successful result. It survives clearing, edits and failed calculations. Scientific arithmetic uses approximately 15 significant digits.",
        "n!" => "Factorial of the preceding value. Supports whole numbers from 0 through 170.",
        "asin" or "acos" or "atan" => "Inverse trigonometric function; its result uses the selected degree/radian angle mode.",
        "asinh" or "acosh" or "atanh" => "Inverse hyperbolic function. Hyperbolic arguments and results are dimensionless.",
        "log" => "Common logarithm, base 10. Requires a positive argument.",
        "ln" => "Natural logarithm, base e. Requires a positive argument.",
        "log2" => "Binary logarithm, base 2. Requires a positive argument.",
        "C" => "Clear this expression and its current result; keep Ans available.",
        "⌫" => "Delete the selected text or the character before the insertion point.",
        "=" => "Evaluate using mathematical precedence. Display rounding does not change the stored binary64 result.",
        _ => ScientificName(label)
    };
    private static string ArgumentSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ",";
    private void ShowHint(string text)
    {
        inputHint.Text = text; inputHint.AccessibleDescription = text; Context.Status(text);
    }
    private double EvaluateExpression() => evaluator.Evaluate(
        ScientificEditing.ExpandAnswer(expression.Text, answer, CultureInfo.CurrentCulture), Angle, CultureInfo.CurrentCulture);

    private void InsertBinaryTemplate(string function)
    {
        var operand = afterEquals ? ScientificEditing.ExpressionLiteral(result, CultureInfo.CurrentCulture) : expression.SelectedText;
        if (afterEquals) { expression.Clear(); afterEquals = false; }
        var template = ScientificEditing.BinaryTemplate(function, operand, CultureInfo.CurrentCulture);
        var start = expression.SelectionStart;
        expression.SelectedText = template.Text;
        expression.Select(start + template.SelectionStart, template.SelectionLength);
        expression.Focus(); ShowHint(template.Prompt);
    }
    private void InsertExponent()
    {
        if (afterEquals) { expression.Text = ScientificEditing.ExpressionLiteral(result, CultureInfo.CurrentCulture); expression.SelectionStart = expression.TextLength; }
        var start = expression.SelectionStart;
        var operand = expression.SelectedText;
        if (operand.Length == 0)
        {
            var prefix = expression.Text[..start];
            var literal = System.Text.RegularExpressions.Regex.Match(prefix, @"(?<![\p{L}\p{Nd}_])\d+(?:[.,]\d*)?(?:[eE][+-]?\d+)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (literal.Success) { start = literal.Index; operand = literal.Value; expression.Select(start, operand.Length); }
        }
        var template = ScientificEditing.ExponentTemplate(operand, CultureInfo.CurrentCulture);
        afterEquals = false; expression.SelectedText = template.Text; expression.Select(start + template.SelectionStart, template.SelectionLength);
        expression.Focus(); ShowHint(FunctionHelp("EXP"));
    }
    public override string CopyText => hasResult && error is null ? result.ToString("G17", CultureInfo.CurrentCulture) : "";
    public override void Reuse(string value)
    {
        if (NumberText.TryDouble(value, CultureInfo.CurrentCulture, out var number))
        {
            if (double.IsFinite(number)) { Insert(ScientificEditing.ExpressionLiteral(number, CultureInfo.CurrentCulture)); return; }
        }
        Context.Status("Select a numeric result to reuse in Scientific.");
    }
    public void ReuseStoredResult(string value)
    {
        if (BigDecimal.TryParse(value, out var exact)) ReuseExact(exact);
        else Context.Status("Select a numeric result to reuse in Scientific.");
    }
    public void ReuseExact(BigDecimal value)
    {
        if (!value.TryToDouble(out var recalled)) { Context.Status("This stored number is outside Scientific's floating-point range."); return; }
        Insert(ScientificEditing.ExpressionLiteral(recalled, CultureInfo.CurrentCulture));
        if (value.SignificantDigits > 15) Context.Status("Scientific uses 15-digit precision. The stored value remains exact.");
    }
    public override void FocusInput() => expression.Focus();
    public override bool Command(Keys key)
    {
        if (key == Keys.Enter) { Calculate(); return true; }
        if (key == Keys.Escape) { Clear(); return true; }
        return false;
    }
    private void Press(string label) => Attempt(() =>
    {
        switch (label)
        {
            case "=": Calculate(); break;
            case "Ans":
                if (answer is null) { Context.Status("Ans is available after the first successful calculation."); break; }
                Insert("Ans"); ShowHint("Ans recalls the last successful result, including after clearing or correcting an error."); break;
            case "C": Clear(); break;
            case "⌫":
                if (expression.SelectionLength > 0) expression.SelectedText = "";
                else if (expression.SelectionStart > 0) { var start = expression.SelectionStart; expression.Text = expression.Text.Remove(start - 1, 1); expression.SelectionStart = start - 1; }
                expression.Focus(); break;
            case "sin": case "cos": case "tan": case "asin": case "acos": case "atan":
            case "sinh": case "cosh": case "tanh": case "asinh": case "acosh": case "atanh":
            case "ln": case "log": case "log2": Wrap(label); break;
            case "root": case "logbase": case "mod": InsertBinaryTemplate(label); break;
            case "√x": Wrap("sqrt"); break; case "∛x": Wrap("cbrt"); break;
            case "|x|": Wrap("abs"); break; case "1/x": Wrap("inv"); break;
            case "10ˣ": Wrap("pow10"); break; case "eˣ": Wrap("exp"); break; case "+/−": WrapNegative(); break;
            case "x²": Insert("^2"); break; case "xʸ": Insert("^"); break;
            case "x³": Insert("^3"); break;
            case "n!": Insert("!"); ShowHint("Factorial applies to the preceding value. Only whole numbers from 0 to 170 are supported."); break;
            case "EXP": InsertExponent(); break;
            case ".": Insert(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator); break;
            default: Insert(label); break;
        }
    });
    private BigDecimal CurrentExact()
    {
        var value = hasResult && error is null ? result : EvaluateExpression();
        return BigDecimal.Parse(value.ToString("G17", CultureInfo.InvariantCulture));
    }
    private AngleUnit Angle => angles.SelectedIndex == 0 ? AngleUnit.Degrees : AngleUnit.Radians;
    private void InvalidateResult() { afterEquals = false; error = null; hasResult = false; RefreshFormatting(); Context.Status("Press Enter to evaluate · Scientific uses 15-digit floating-point precision"); }
    private void Clear() { expression.Clear(); result = 0; error = null; hasResult = false; afterEquals = false; RefreshFormatting(); expression.Focus(); }
    private void Insert(string text)
    {
        if (afterEquals)
        {
            var previous = ScientificEditing.ExpressionLiteral(result, CultureInfo.CurrentCulture);
            expression.Text = text[0] is '+' or '-' or '−' or '*' or '×' or '/' or '÷' or '^' or '%' or '!' ? $"({previous})" : "";
            expression.SelectionStart = expression.TextLength;
        }
        expression.SelectedText = text;
        expression.Focus(); afterEquals = false;
    }
    private void Wrap(string name)
    {
        if (afterEquals) { expression.Text = $"{name}({ScientificEditing.ExpressionLiteral(result, CultureInfo.CurrentCulture)})"; expression.SelectionStart = expression.TextLength; }
        else if (expression.SelectionLength > 0) expression.SelectedText = $"{name}({expression.SelectedText})";
        else Insert(name + "(");
        expression.Focus(); afterEquals = false;
    }
    private void WrapNegative()
    {
        var operand = afterEquals ? ScientificEditing.ExpressionLiteral(result, CultureInfo.CurrentCulture) : expression.Text;
        expression.Text = operand.Length == 0 ? "-" : $"-({operand})";
        expression.SelectionStart = expression.TextLength; expression.Focus();
    }
    private void Calculate()
    {
        try
        {
            result = EvaluateExpression();
            answer = result; hasResult = true; afterEquals = true; error = null;
            Context.Record("Scientific", $"{expression.Text} [{(Angle == AngleUnit.Degrees ? "DEG" : "RAD")}] =", result.ToString("G17", CultureInfo.InvariantCulture));
            Context.Status($"Scientific: approximately 15-digit binary64 arithmetic; {Context.Settings.Precision} significant digits displayed. Ans updated.");
        }
        catch (CalculationException ex) { hasResult = false; afterEquals = false; error = ex.Message; Context.Status(ex.Message); }
        RefreshFormatting();
    }
    public override void RefreshFormatting()
    {
        display.AccessibleDescription = $"Scientific uses approximately 15 significant digits internally (binary64); the current display preference is {Context.Settings.Precision} significant digits. Copy and Ans retain the full stored result.";
        display.Set(error is null ? (hasResult ? "Result · " : "Enter expression · ") + (Angle == AngleUnit.Degrees ? "degrees" : "radians") + $" · {Context.Settings.Precision} displayed digits" : "Check your expression",
            error ?? (hasResult ? NumberText.Format(result, CultureInfo.CurrentCulture, Context.Settings.Precision, Context.Settings.GroupDigits) : expression.TextLength == 0 ? "0" : "—"), error is not null);
    }

    private void BuildFunctionBank()
    {
        functionBank.SuspendLayout();
        while (functionBank.Controls.Count > 0) { var control = functionBank.Controls[0]; functionBank.Controls.RemoveAt(0); control.Dispose(); }
        string[] labels = functionGroup.SelectedIndex switch
        {
            1 => ["sin", "cos", "tan", "asin", "acos", "atan", "π", "e", "√x", "x²", "|x|", "mod"],
            2 => ["sinh", "cosh", "tanh", "asinh", "acosh", "atanh", "eˣ", "ln", "10ˣ", "log", "1/x", "|x|"],
            _ => ["√x", "∛x", "x²", "x³", "xʸ", "root", "ln", "log", "log2", "logbase", "n!", "mod"]
        };
        for (var index = 0; index < labels.Length; index++)
        {
            var label = labels[index]; var text = label switch { "root" => "\u207F\u221Ax", "logbase" => "log\u2090(x)", "mod" => "rem", _ => label };
            var button = Ui.Button(text, () => Press(label), ButtonTone.Quiet, name: ScientificName(label));
            button.AccessibleDescription = FunctionHelp(label); button.Margin = new(1); Ui.SetFont(button, 11); functionBank.Controls.Add(button, index % 6, index / 6);
        }
        Ui.Theme(functionBank, Palette); functionBank.ResumeLayout(); Ui.AttachTooltips(functionBank);
    }
}
