using System.Globalization;
using System.Text.RegularExpressions;
using ModernCalculator.Core.Calculations;

namespace ModernCalculator.WinForms.Views;

/// <summary>Scientific editor conveniences; all arithmetic remains in ExpressionEvaluator.</summary>
public static partial class ScientificEditing
{
    [GeneratedRegex(@"(?<![\p{L}\p{Nd}_])ans(?![\p{L}\p{Nd}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnswerToken();
    [GeneratedRegex(@"^[+\-−]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainDecimalLiteral();

    public static string ExpressionLiteral(double value, CultureInfo culture) =>
        value.ToString("G17", CultureInfo.InvariantCulture).Replace(".", culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);

    public static string ExpandAnswer(string expression, double? answer, CultureInfo culture)
    {
        if (!AnswerToken().IsMatch(expression)) return expression;
        if (answer is null) throw new CalculationException("Ans is available after the first successful calculation.");
        var literal = "(" + ExpressionLiteral(answer.Value, culture) + ")";
        return AnswerToken().Replace(expression, _ => literal);
    }

    public static ScientificTemplate ExponentTemplate(string operand, CultureInfo culture)
    {
        var normalized = operand.Replace(culture.NumberFormat.NumberDecimalSeparator, ".", StringComparison.Ordinal);
        var text = operand.Length == 0 ? "1E3" : PlainDecimalLiteral().IsMatch(normalized) ? operand + "E3" : "(" + operand + ")*10^3";
        return new(text, text.Length - 1, 1, "Edit the selected base-10 exponent. This differs from e to the power x.");
    }

    public static ScientificTemplate BinaryTemplate(string function, string selectedOperand, CultureInfo culture)
    {
        var (first, second, description) = function switch
        {
            "root" => ("8", "3", "root(value, degree): enter the value first, then the root degree. Example: root(8, 3) = 2."),
            "logbase" => ("81", "3", "logbase(value, base): enter the value first, then the logarithm base. Example: logbase(81, 3) = 4."),
            "mod" => ("10", "3", "mod(dividend, divisor): enter the dividend first, then a non-zero divisor. Example: mod(10, 3) = 1."),
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };
        var separator = culture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ",";
        var hasOperand = selectedOperand.Length > 0;
        if (hasOperand) first = selectedOperand;
        var text = $"{function}({first}{separator}{second})";
        var start = function.Length + 1 + (hasOperand ? first.Length + 1 : 0);
        return new(text, start, hasOperand ? second.Length : first.Length,
            description.Replace(", ", separator + " ", StringComparison.Ordinal) + " Edit the selected argument.");
    }
}

public sealed record ScientificTemplate(string Text, int SelectionStart, int SelectionLength, string Prompt);
