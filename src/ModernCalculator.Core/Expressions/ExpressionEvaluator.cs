// Developer ::> Gehan Fernando
using System.Globalization;
using ModernCalculator.Core.Calculations;

namespace ModernCalculator.Core.Expressions;

public enum AngleUnit { Degrees, Radians }

/// <summary>Bounded recursive-descent grammar. No scripting, reflection or dynamic execution.</summary>
public sealed class ExpressionEvaluator
{
    public const int MaximumLength = 2048;
    public double Evaluate(string expression, AngleUnit angle = AngleUnit.Degrees, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new CalculationException("Enter an expression first.");
        if (expression.Length > MaximumLength) throw new CalculationException($"Expressions are limited to {MaximumLength} characters.");
        try { return new Parser(expression, angle, culture ?? CultureInfo.InvariantCulture).Evaluate(); }
        catch (OverflowException) { throw new CalculationException("The result is outside the supported range."); }
    }

    private sealed class Parser(string text, AngleUnit angle, CultureInfo culture)
    {
        private int position;
        private int depth;
        private readonly char decimalSeparator = culture.NumberFormat.NumberDecimalSeparator[0];
        private char ArgumentSeparator => decimalSeparator == ',' ? ';' : ',';

        public double Evaluate()
        {
            var result = Additive();
            SkipSpace();
            if (position != text.Length) throw Fail("Unexpected input. Use × or * for multiplication.");
            return Finite(result);
        }

        private double Additive()
        {
            var result = Multiplicative();
            while (true)
            {
                if (TakeSign(false)) result = Finite(result + Multiplicative());
                else if (TakeSign(true)) result = Finite(result - Multiplicative());
                else return result;
            }
        }

        private double Multiplicative()
        {
            var result = Unary();
            while (true)
            {
                if (Take('*') || Take('×')) result = Product(result, Unary());
                else if (Take('/') || Take('÷'))
                {
                    var divisor = Unary();
                    if (divisor == 0) throw Fail("Cannot divide by zero.");
                    var quotient = Finite(result / divisor);
                    if (quotient == 0 && result != 0) throw Fail("The result underflowed the floating-point range.");
                    result = quotient;
                }
                else if (Take('%'))
                {
                    var divisor = Unary();
                    if (divisor == 0) throw Fail("Remainder requires a non-zero divisor.");
                    result %= divisor;
                }
                else return result;
            }
        }

        private double Unary()
        {
            Enter();
            try
            {
                if (TakeSign(false)) return Unary();
                if (TakeSign(true)) return -Unary();
                return Power();
            }
            finally { depth--; }
        }

        private double Power()
        {
            var result = Postfix();
            // Parse the exponent recursively so chained powers associate from right to left.
            return Take('^') ? Pow(result, Unary()) : result;
        }

        private double Postfix()
        {
            var result = Primary();
            while (Take('!')) result = Factorial(result);
            return result;
        }

        private double Primary()
        {
            if (Take('('))
            {
                var result = Additive();
                Require(')');
                return result;
            }
            SkipSpace();
            if (position < text.Length && (char.IsLetter(text[position]) || text[position] == 'π')) return FunctionOrConstant();
            return Number();
        }

        private double Number()
        {
            SkipSpace();
            var start = position;
            while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
            if (position < text.Length && (text[position] == '.' || text[position] == decimalSeparator))
            {
                position++;
                while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
            }
            var literal = text[start..position].Replace(decimalSeparator, '.');
            if (position < text.Length && text[position] is 'e' or 'E')
            {
                position++;
                var sign = TakeSign(false, false) ? "+" : TakeSign(true, false) ? "-" : "";
                var exponentStart = position;
                while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
                literal += "e" + sign + text[exponentStart..position];
            }
            if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Fail("A number, constant or function was expected.");
            // Reject nonzero literals that binary64 parsing would silently underflow to zero.
            if (value == 0 && literal.Split('e', 'E')[0].Any(c => c is >= '1' and <= '9'))
                throw Fail("This literal is too small for floating-point arithmetic.");
            return Finite(value);
        }

        private double FunctionOrConstant()
        {
            var start = position;
            while (position < text.Length && char.IsLetterOrDigit(text[position])) position++;
            var name = text[start..position].ToLowerInvariant();
            if (name is "pi" or "π") return Math.PI;
            if (name == "e") return Math.E;
            Require('(');
            var first = Additive();
            double? second = Take(ArgumentSeparator) ? Additive() : null;
            Require(')');
            if (name is "root" or "logbase" or "pow" or "mod")
            {
                if (second is null) throw Fail($"{name} requires two arguments separated by '{ArgumentSeparator}'.");
                return name switch
                {
                    "root" => Root(first, second.Value),
                    "pow" => Pow(first, second.Value),
                    "mod" => second == 0 ? throw Fail("Remainder requires a non-zero divisor.") : first % second.Value,
                    _ => Log(first, second.Value)
                };
            }
            if (second is not null) throw Fail($"{name} accepts one argument.");
            // Reduce degrees before converting to radians so large exact turns
            // do not lose their phase in the multiplication by pi.
            var reducedDegrees = angle == AngleUnit.Degrees ? Math.IEEERemainder(first, 360) : first;
            var radians = angle == AngleUnit.Degrees ? reducedDegrees * (Math.PI / 180) : first;
            if ((name is "sin" or "tan") && angle == AngleUnit.Degrees && reducedDegrees != 0 && radians == 0)
                throw Fail("The angle underflowed the floating-point range.");
            var result = name switch
            {
                "sin" => Math.Sin(radians),
                "cos" => Math.Cos(radians),
                "tan" => Math.Abs(Math.Cos(radians)) < 1e-14 ? throw Fail("Tangent is undefined at this angle.") : Math.Tan(radians),
                "asin" => FromRadians(Math.Asin(RequireRange(first, -1, 1))),
                "acos" => FromRadians(Math.Acos(RequireRange(first, -1, 1))),
                "atan" => FromRadians(Math.Atan(first)),
                "sinh" => Math.Sinh(first), "cosh" => Math.Cosh(first), "tanh" => Math.Tanh(first),
                "asinh" => Math.Asinh(first),
                "acosh" => first < 1 ? throw Fail("acosh requires a value of at least 1.") : Math.Acosh(first),
                "atanh" => Math.Abs(first) >= 1 ? throw Fail("atanh requires a value strictly between −1 and 1.") : Math.Atanh(first),
                "sqrt" => first < 0 ? throw Fail("A real square root requires a non-negative number.") : Math.Sqrt(first),
                "cbrt" => Math.Cbrt(first), "abs" => Math.Abs(first),
                "ln" => Log(first, Math.E), "log" or "log10" => Log(first, 10), "log2" => Log(first, 2),
                "exp" => NonZeroExponential(first), "pow10" => Pow(10, first),
                "fact" => Factorial(first),
                "inv" => first == 0 ? throw Fail("Cannot divide by zero.") : 1 / first,
                _ => throw Fail($"Unknown function '{name}'.")
            };
            return Finite(result);
        }

        private double FromRadians(double value) => angle == AngleUnit.Degrees ? value * (180 / Math.PI) : value;
        private double RequireRange(double value, double min, double max) => value < min || value > max
            ? throw Fail($"The argument must be between {min} and {max}.") : value;
        private double Log(double value, double @base)
        {
            if (value <= 0 || @base <= 0 || @base == 1) throw Fail("Logarithms require a positive value and a positive base other than 1.");
            return Finite(Math.Log(value, @base));
        }
        private double Pow(double value, double exponent)
        {
            if (value == 0 && exponent <= 0) throw Fail("Zero to a non-positive power is undefined.");
            if (value < 0 && exponent != Math.Truncate(exponent)) throw Fail("A negative base requires an integer exponent for real powers. Use root for odd roots.");
            var result = Finite(Math.Pow(value, exponent));
            if (result == 0 && value != 0) throw Fail("The power underflowed the floating-point range.");
            return result;
        }
        private double Root(double value, double degree)
        {
            if (degree == 0) throw Fail("A root degree cannot be zero.");
            if (value < 0 && (degree != Math.Truncate(degree) || Math.Abs(degree % 2) != 1))
                throw Fail("A negative radicand requires an odd integer root degree.");
            return value < 0 ? -Pow(-value, 1 / degree) : Pow(value, 1 / degree);
        }
        private double Factorial(double value)
        {
            if (value < 0 || value != Math.Truncate(value) || value > 170)
                throw Fail("Factorial requires a whole number from 0 to 170.");
            double result = 1;
            for (var i = 2; i <= (int)value; i++) result *= i;
            return result;
        }
        private double Product(double left, double right)
        {
            var result = Finite(left * right);
            if (result == 0 && left != 0 && right != 0) throw Fail("The product underflowed the floating-point range.");
            return result;
        }
        private double NonZeroExponential(double value)
        {
            var result = Finite(Math.Exp(value));
            return result == 0 ? throw Fail("The exponential underflowed the floating-point range.") : result;
        }
        private static double Finite(double value) => double.IsFinite(value) ? (value == 0 ? 0 : value)
            : throw new CalculationException("The result is outside the supported real-number range.");
        private void Enter() { if (++depth > 64) throw Fail("The expression is nested too deeply (maximum 64)."); }
        private void SkipSpace() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private bool Take(char character)
        {
            SkipSpace();
            if (position >= text.Length || text[position] != character) return false;
            position++; return true;
        }
        private bool TakeSign(bool negative, bool skipSpace = true)
        {
            if (skipSpace) SkipSpace();
            var native = negative ? culture.NumberFormat.NegativeSign : culture.NumberFormat.PositiveSign;
            if (native.Length > 0 && text.AsSpan(position).StartsWith(native, StringComparison.Ordinal))
            { position += native.Length; return true; }
            if (position >= text.Length || (negative ? text[position] is not ('-' or '−') : text[position] != '+')) return false;
            position++; return true;
        }
        private void Require(char character) { if (!Take(character)) throw Fail($"Expected '{character}'."); }
        private CalculationException Fail(string message) => new($"{message} (position {position + 1})");
    }
}
