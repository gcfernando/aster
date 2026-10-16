using System.Globalization;
using System.Numerics;
using ModernCalculator.Core.Calculations;

namespace ModernCalculator.Core.Modes;

public enum IntegerOperation { Add, Subtract, Multiply, Divide, Remainder, And, Or, Xor, LeftShift, RightShift }

public sealed class ProgrammerCalculator
{
    public int Width { get; private set; } = 64;
    public bool Signed { get; set; } = true;
    public BigInteger Bits { get; private set; }
    public BigInteger Mask => (BigInteger.One << Width) - 1;
    public BigInteger Value => Signed && (Bits & (BigInteger.One << (Width - 1))) != 0 ? Bits - (BigInteger.One << Width) : Bits;

    public void SetWidth(int width)
    {
        if (width is not (8 or 16 or 32 or 64)) throw new ArgumentOutOfRangeException(nameof(width));
        var previous = Value;
        Width = width;
        SetValue(previous);
    }
    public void SetValue(BigInteger value) => Bits = value & Mask;
    public void Parse(string input, int numberBase)
    {
        ValidateBase(numberBase);
        if (string.IsNullOrWhiteSpace(input) || input.Length > 128) throw new CalculationException("Enter an integer of at most 128 characters.");
        var text = input.Trim();
        var negative = text.StartsWith('-');
        if (negative || text.StartsWith('+')) text = text[1..];
        if (text.Length == 0) throw new CalculationException("Enter a valid integer.");
        BigInteger value = 0;
        foreach (var character in text.ToUpperInvariant())
        {
            var digit = character is >= '0' and <= '9' ? character - '0' : character is >= 'A' and <= 'F' ? character - 'A' + 10 : -1;
            if (digit < 0 || digit >= numberBase) throw new CalculationException($"'{character}' is not a base-{numberBase} digit.");
            value = value * numberBase + digit;
        }
        SetValue(negative ? -value : value);
    }
    public void ParseDecimalResult(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 128) throw new CalculationException("Enter an integer of at most 128 characters.");
        if (BigInteger.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) { SetValue(integer); return; }
        if (NumberText.TryDecimal(input, CultureInfo.InvariantCulture, out var value) && value == decimal.Truncate(value))
        { SetValue(new BigInteger(value)); return; }
        throw new CalculationException("Only whole-number results in the supported input range can be reused in Programmer.");
    }
    public string Format(int numberBase)
    {
        ValidateBase(numberBase);
        if (numberBase == 10) return Value.ToString(CultureInfo.InvariantCulture);
        if (Bits == 0) return "0";
        var value = Bits;
        var result = "";
        while (value > 0) { result = "0123456789ABCDEF"[(int)(value % numberBase)] + result; value /= numberBase; }
        return result;
    }
    public void Not() => SetValue(~Bits);
    public void ToggleBit(int index)
    {
        if (index < 0 || index >= Width) throw new ArgumentOutOfRangeException(nameof(index));
        SetValue(Bits ^ (BigInteger.One << index));
    }
    public void Apply(IntegerOperation operation, BigInteger operand)
    {
        var left = Value;
        var rightBits = operand & Mask;
        var right = Signed && (rightBits & (BigInteger.One << (Width - 1))) != 0 ? rightBits - (BigInteger.One << Width) : rightBits;
        var result = operation switch
        {
            IntegerOperation.Add => left + right,
            IntegerOperation.Subtract => left - right,
            IntegerOperation.Multiply => left * right,
            IntegerOperation.Divide => right == 0 ? throw new CalculationException("Cannot divide by zero.") : left / right,
            IntegerOperation.Remainder => right == 0 ? throw new CalculationException("Remainder requires a non-zero divisor.") : left % right,
            IntegerOperation.And => Bits & rightBits,
            IntegerOperation.Or => Bits | rightBits,
            IntegerOperation.Xor => Bits ^ rightBits,
            IntegerOperation.LeftShift => Bits << ShiftCount(operand),
            IntegerOperation.RightShift => left >> ShiftCount(operand),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        SetValue(result);
    }
    private int ShiftCount(BigInteger count) => count < 0 || count >= Width
        ? throw new CalculationException($"Shift count must be between 0 and {Width - 1}.") : (int)count;
    private static void ValidateBase(int numberBase)
    {
        if (numberBase is not (2 or 8 or 10 or 16)) throw new ArgumentOutOfRangeException(nameof(numberBase));
    }
}
