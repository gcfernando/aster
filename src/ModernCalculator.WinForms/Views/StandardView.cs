using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.WinForms.Controls;

namespace ModernCalculator.WinForms.Views;

public sealed class StandardView : ModeView
{
    private readonly StandardCalculator calculator = new();
    private readonly ResultDisplay display = new();
    private readonly Label expressionPreview = Ui.Label("Ready", 9);
    private readonly Stack<StandardCalculator> undo = new();
    private readonly Stack<StandardCalculator> redo = new();
    public StandardView(WorkspaceContext context) : base(context)
    {
        AccessibleName = "Standard calculator";
        var layout = Ui.Table(1, 24, -24, -7, -69);
        expressionPreview.AccessibleName = "Standard expression"; expressionPreview.AutoEllipsis = true;
        expressionPreview.TextAlign = ContentAlignment.MiddleRight;
        layout.Controls.Add(expressionPreview, 0, 0);
        layout.Controls.Add(display, 0, 1);
        layout.Controls.Add(MemoryControls(() => calculator.Error is null ? calculator.ExactValue : throw new CalculationException(calculator.Error),
            value => Execute(() => calculator.SetValue(value))), 0, 2);
        var keys = Ui.Table(4, -1, -1, -1, -1, -1, -1);
        void Key(string text, int col, int row, Action action, ButtonTone tone = ButtonTone.Normal, IconKind icon = IconKind.None, string? name = null)
        {
            var button = Ui.Button(text, () => Execute(action), tone, icon, name);
            if (tone is ButtonTone.Number or ButtonTone.Accent or ButtonTone.Operator) Ui.SetFont(button, 20);
            keys.Controls.Add(button, col, row);
        }
        Key("%", 0, 0, calculator.Percent, name: "Percentage");
        Key("CE", 1, 0, calculator.ClearEntry, name: "Clear entry (Delete)");
        Key("C", 2, 0, calculator.Clear, name: "Clear calculation (Escape)");
        Key("", 3, 0, calculator.Backspace, icon: IconKind.Backspace, name: "Backspace");
        Key("1/x", 0, 1, () => calculator.Unary(UnaryOperation.Reciprocal), name: "Reciprocal");
        Key("x²", 1, 1, () => calculator.Unary(UnaryOperation.Square), name: "Square");
        Key("√x", 2, 1, () => calculator.Unary(UnaryOperation.SquareRoot), name: "Square root");
        Key("÷", 3, 1, () => calculator.Operator(BinaryOperation.Divide), ButtonTone.Operator, name: "Divide");
        var numbers = new[] { "789", "456", "123" };
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 3; col++)
            {
                var digit = numbers[row][col]; Key(digit.ToString(), col, row + 2, () => calculator.Digit(digit), ButtonTone.Number);
            }
        Key("×", 3, 2, () => calculator.Operator(BinaryOperation.Multiply), ButtonTone.Operator, name: "Multiply");
        Key("−", 3, 3, () => calculator.Operator(BinaryOperation.Subtract), ButtonTone.Operator, name: "Subtract");
        Key("+", 3, 4, () => calculator.Operator(BinaryOperation.Add), ButtonTone.Operator, name: "Add");
        Key("+/−", 0, 5, () => calculator.Unary(UnaryOperation.Negate), name: "Change sign (F9)");
        Key("0", 1, 5, () => calculator.Digit('0'), ButtonTone.Number);
        Key(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, 2, 5, calculator.DecimalPoint, ButtonTone.Number, name: "Decimal separator");
        Key("=", 3, 5, calculator.Equals, ButtonTone.Accent, name: "Equals (Enter)");
        layout.Controls.Add(keys, 0, 3); Controls.Add(layout);
        FitMemoryRow(layout, 2);
        foreach (Control key in keys.Controls)
            key.AccessibleDescription = key.AccessibleName switch
            {
                "Clear entry (Delete)" => "CE clears the current input and keeps a pending operation. Delete key.",
                "Clear calculation (Escape)" => "C clears the entire calculation, including any pending operation. Escape key.",
                "Backspace" => "Deletes the last typed digit from the current entry. Backspace key.",
                "Percentage" => "For addition or subtraction, calculate a percentage of the first operand. Example: 200 + 10 % = 220. For multiplication or division, use the entry divided by 100.",
                _ => key.AccessibleDescription
            };
        calculator.CalculationCompleted += result => Context.Record("Standard", result.Expression, result.Result);
        UpdateDisplay();
        FinishLayout();
    }
    public override string CopyText => calculator.Error is null ? calculator.ExactValue.ToString(CultureInfo.CurrentCulture) : "";
    public override void Reuse(string value)
    {
        if (!NumberText.TryBigDecimal(value, CultureInfo.CurrentCulture, out var number)) { Context.Status("Enter a valid number of up to 100 significant digits."); return; }
        Execute(() => calculator.SetValue(number));
    }
    public void ReuseStoredResult(string value) => Execute(() => calculator.SetValue(BigDecimal.Parse(value)));
    private void Execute(Action action)
    {
        undo.Push(calculator.Snapshot()); if (undo.Count > 100) { var retained = undo.Take(100).Reverse().ToArray(); undo.Clear(); foreach (var item in retained) undo.Push(item); }
        redo.Clear();
        var succeeded = false;
        Attempt(() => { action(); succeeded = true; }); UpdateDisplay();
        if (calculator.Error is not null) Context.Status(calculator.Error + " Enter a digit or press Escape to start again.");
        else if (succeeded) Context.Status("100-digit input · Enter to calculate · Ctrl+Z to undo");
    }
    private void UpdateDisplay()
    {
        var culture = CultureInfo.CurrentCulture;
        var value = calculator.IsEditing ? calculator.Entry.Replace(".", culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal)
            : NumberText.Format(calculator.ExactValue, culture, BigDecimal.MaximumCoefficientDigits, Context.Settings.GroupDigits);
        expressionPreview.Text = calculator.Expression.Length == 0 ? "Ready" : calculator.Expression.EndsWith('=') ? calculator.Expression
            : calculator.Expression + (calculator.IsEditing ? " " + calculator.Entry.Replace(".", culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal) : " …");
        expressionPreview.AccessibleDescription = expressionPreview.Text;
        display.Set(calculator.Expression.EndsWith('=') ? "Result" : "Current input",
            calculator.Error ?? value, calculator.Error is not null);
    }
    public override void RefreshFormatting() => UpdateDisplay();
    public override bool Character(char character)
    {
        if (char.IsAsciiDigit(character)) Execute(() => calculator.Digit(character));
        else
        {
            Action? action = character switch
            {
                '+' => () => calculator.Operator(BinaryOperation.Add), '-' => () => calculator.Operator(BinaryOperation.Subtract),
                '*' => () => calculator.Operator(BinaryOperation.Multiply), '/' => () => calculator.Operator(BinaryOperation.Divide),
                '.' or ',' => calculator.DecimalPoint, '%' => calculator.Percent, '=' => calculator.Equals, _ => null
            };
            if (action is null) return false;
            Execute(action);
        }
        return true;
    }
    public override bool Command(Keys key)
    {
        if (key == (Keys.Control | Keys.Z) || key == (Keys.Control | Keys.Y))
        {
            var source = key == (Keys.Control | Keys.Z) ? undo : redo; var target = ReferenceEquals(source, undo) ? redo : undo;
            if (source.Count == 0) { Context.Status("No further edit to " + (ReferenceEquals(source, undo) ? "undo." : "redo.")); return true; }
            target.Push(calculator.Snapshot()); calculator.Restore(source.Pop()); UpdateDisplay(); Context.Status(ReferenceEquals(source, undo) ? "Undid the last calculator action." : "Redid the calculator action."); return true;
        }
        Action? action = key switch { Keys.Enter => calculator.Equals, Keys.Escape => calculator.Clear,
            Keys.Delete => calculator.ClearEntry, Keys.Back => calculator.Backspace,
            Keys.F9 => () => calculator.Unary(UnaryOperation.Negate), _ => null };
        if (action is null) return false; Execute(action); return true;
    }
}
