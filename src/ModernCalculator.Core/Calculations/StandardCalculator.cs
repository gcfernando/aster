// Developer ::> Gehan Fernando
namespace ModernCalculator.Core.Calculations;

public enum BinaryOperation { Add, Subtract, Multiply, Divide }
public enum UnaryOperation { Square, SquareRoot, Reciprocal, Negate }
public sealed record CompletedCalculation(string Expression, string Result);

/// <summary>Immediate-execution 100-digit decimal calculator. UI input is culture-independent internally.</summary>
public sealed class StandardCalculator
{
    private BigDecimal? accumulator;
    private BinaryOperation? pending;
    private BinaryOperation? repeatedOperation;
    private BigDecimal repeatedOperand;
    private bool replaceEntry = true;
    private bool completed;
    private bool transformedEntry;

    public string Entry { get; private set; } = "0";
    public string Expression { get; private set; } = "";
    public string? Error { get; private set; }
    public BigDecimal ExactValue => BigDecimal.Parse(Entry);
    public decimal Value => ExactValue.TryToDecimal(out var value) ? value : throw new CalculationException("Use ExactValue to access values beyond decimal precision or range.");
    public bool IsEditing => !replaceEntry && Error is null;
    public event Action<CompletedCalculation>? CalculationCompleted;

    // Capture pending and repeated operations together so undo restores the full calculation.
    public StandardCalculator Snapshot() => new()
    {
        Entry = Entry, Expression = Expression, Error = Error, accumulator = accumulator, pending = pending,
        repeatedOperation = repeatedOperation, repeatedOperand = repeatedOperand, replaceEntry = replaceEntry,
        completed = completed, transformedEntry = transformedEntry
    };
    public void Restore(StandardCalculator snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Entry = snapshot.Entry; Expression = snapshot.Expression; Error = snapshot.Error; accumulator = snapshot.accumulator;
        pending = snapshot.pending; repeatedOperation = snapshot.repeatedOperation; repeatedOperand = snapshot.repeatedOperand;
        replaceEntry = snapshot.replaceEntry; completed = snapshot.completed; transformedEntry = snapshot.transformedEntry;
    }

    public void Digit(char digit)
    {
        if (!char.IsAsciiDigit(digit)) throw new ArgumentOutOfRangeException(nameof(digit));
        BeginInput();
        if (transformedEntry) { Entry = "0"; transformedEntry = false; }
        var next = replaceEntry || Entry == "0" ? digit.ToString() : Entry == "-0" ? "-" + digit : Entry + digit;
        if (NumberText.SignificantInputDigits(next) > BigDecimal.Precision)
            throw new CalculationException("You can enter up to 100 significant digits.");
        if (!BigDecimal.TryParse(next, out _))
            throw new CalculationException("The number exceeds the supported decimal range.");
        Entry = next;
        replaceEntry = false;
    }

    public void DecimalPoint()
    {
        BeginInput();
        if (transformedEntry) { Entry = "0"; transformedEntry = false; }
        if (replaceEntry) Entry = "0";
        if (!Entry.Contains('.')) Entry += ".";
        replaceEntry = false;
    }

    private void BeginInput()
    {
        if (Error is not null || completed) Clear();
        else DiscardCompletedExpressionForEdit();
    }

    private void DiscardCompletedExpressionForEdit()
    {
        if (pending is null && Expression.EndsWith('=')) Expression = "";
    }

    public void SetValue(decimal value) => SetValue(BigDecimal.FromDecimal(value));

    public void SetValue(BigDecimal value)
    {
        BeginInput();
        Entry = Invariant(value);
        replaceEntry = false;
        transformedEntry = false;
    }

    public void Operator(BinaryOperation operation) => Guard(() =>
    {
        if (Error is not null) return;
        var value = ExactValue;
        if (pending.HasValue && replaceEntry && accumulator.HasValue)
            value = accumulator.Value;
        if (pending.HasValue && !replaceEntry && accumulator.HasValue)
            value = Calculate(accumulator.Value, value, pending.Value);
        accumulator = value;
        Entry = Invariant(value);
        pending = operation;
        Expression = $"{Entry} {Symbol(operation)}";
        replaceEntry = true;
        completed = false;
        repeatedOperation = null;
        transformedEntry = false;
    });

    public void Equals() => Guard(() =>
    {
        if (Error is not null) return;
        var operation = pending ?? repeatedOperation;
        if (operation is null) return;
        var left = pending.HasValue ? accumulator ?? ExactValue : ExactValue;
        // Reuse the saved right operand when equals repeats the last completed operation.
        var right = pending.HasValue ? ExactValue : repeatedOperand;
        var result = Calculate(left, right, operation.Value);
        Expression = $"{Invariant(left)} {Symbol(operation.Value)} {Invariant(right)} =";
        Entry = Invariant(result);
        repeatedOperation = operation;
        repeatedOperand = right;
        pending = null;
        accumulator = null;
        replaceEntry = true;
        completed = true;
        CalculationCompleted?.Invoke(new(Expression, Entry));
    });

    public void Unary(UnaryOperation operation) => Guard(() =>
    {
        if (Error is not null) return;
        var input = ExactValue;
        var hadCompletedExpression = pending is null && Expression.EndsWith('=');
        var result = operation switch
        {
            UnaryOperation.Negate => -input,
            UnaryOperation.Square => Calculate(input, input, BinaryOperation.Multiply),
            UnaryOperation.Reciprocal => Calculate(BigDecimal.One, input, BinaryOperation.Divide),
            UnaryOperation.SquareRoot => BigDecimal.SquareRoot(input),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        Entry = Invariant(result);
        // A transformed right operand must participate in the pending binary operation.
        replaceEntry = false;
        transformedEntry = operation != UnaryOperation.Negate;
        if (completed) { completed = false; repeatedOperation = null; }
        if (hadCompletedExpression && operation == UnaryOperation.Negate)
            Expression = $"Negate({Invariant(input)}) =";
        if (pending is null && operation != UnaryOperation.Negate)
        {
            Expression = $"{operation}({Invariant(input)}) =";
            CalculationCompleted?.Invoke(new(Expression, Entry));
            completed = true;
            replaceEntry = true;
        }
    });

    public void Percent() => Guard(() =>
    {
        if (Error is not null) return;
        var input = ExactValue;
        var hadCompletedExpression = pending is null && Expression.EndsWith('=');
        var percent = Calculate(input, BigDecimal.Parse("100"), BinaryOperation.Divide);
        if (pending is BinaryOperation.Add or BinaryOperation.Subtract) percent = Calculate(percent, accumulator ?? BigDecimal.Zero, BinaryOperation.Multiply);
        Entry = Invariant(percent);
        replaceEntry = false;
        completed = false;
        // A standalone transformation starts a new operand, so equals must not
        // revive the binary operation that produced the previous result.
        if (pending is null) repeatedOperation = null;
        transformedEntry = true;
        if (hadCompletedExpression) Expression = $"Percent({Invariant(input)}) =";
    });

    public void Clear()
    {
        Entry = "0"; Expression = ""; Error = null; accumulator = null; pending = null;
        repeatedOperation = null; repeatedOperand = BigDecimal.Zero; replaceEntry = true; completed = false; transformedEntry = false;
    }

    public void ClearEntry()
    {
        if (Error is not null || completed) { Clear(); return; }
        DiscardCompletedExpressionForEdit();
        Entry = "0"; replaceEntry = true; transformedEntry = false;
    }

    public void Backspace()
    {
        if (Error is not null) { Clear(); return; }
        if (replaceEntry || completed || transformedEntry) return;
        DiscardCompletedExpressionForEdit();
        Entry = Entry.Length <= 1 ? "0" : Entry[..^1];
        if (Entry == "-") Entry = "0";
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (CalculationException ex) { Error = ex.Message; }
        catch (OverflowException) { Error = "The result exceeds the supported numeric range."; }
    }

    private static BigDecimal Calculate(BigDecimal left, BigDecimal right, BinaryOperation operation) => operation switch
    {
        BinaryOperation.Add => left + right,
        BinaryOperation.Subtract => left - right,
        BinaryOperation.Multiply => left * right,
        BinaryOperation.Divide => left / right,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    public static string Symbol(BinaryOperation operation) => operation switch
    {
        BinaryOperation.Add => "+", BinaryOperation.Subtract => "−", BinaryOperation.Multiply => "×", BinaryOperation.Divide => "÷",
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
    // Entry is always editable fixed-point text, including tiny recalled and pasted values.
    private static string Invariant(BigDecimal value) => value.ToString();
}
