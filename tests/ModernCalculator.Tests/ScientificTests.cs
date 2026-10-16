using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Expressions;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class ScientificTests
{
    private readonly ExpressionEvaluator evaluator = new();
    [Theory]
    [InlineData("2+3*4", 14)] [InlineData("(2+3)*4", 20)] [InlineData("2*(3+(4*5))", 46)]
    [InlineData("2^3^2", 512)] [InlineData("-2^2", -4)] [InlineData("2^-2", .25)]
    [InlineData("sqrt(144)", 12)] [InlineData("cbrt(-27)", -3)] [InlineData("root(-32,5)", -2)]
    [InlineData("root(16,4)", 2)] [InlineData("3!+fact(4)", 30)] [InlineData("0!", 1)]
    [InlineData("log(100)", 2)] [InlineData("ln(e)", 1)] [InlineData("log2(32)", 5)] [InlineData("logbase(81,3)", 4)]
    [InlineData("abs(-6)+inv(4)", 6.25)] [InlineData("pow10(3)+exp(0)", 1001)]
    [InlineData("sin(30)", .5)] [InlineData("cos(180)", -1)] [InlineData("tan(45)", 1)]
    [InlineData("asin(.5)", 30)] [InlineData("acos(0)", 90)] [InlineData("atan(1)", 45)]
    [InlineData("sinh(0)+tanh(0)+cosh(0)", 1)] [InlineData("asinh(0)+acosh(1)+atanh(0)", 0)]
    [InlineData("1.25e3+2E-2", 1250.02)] [InlineData("10%3", 1)] [InlineData("mod(-10,3)", -1)]
    [InlineData("π/pi", 1)] [InlineData("pow(2,3)", 8)] [InlineData("2−3×4÷2", -4)]
    public void Expressions(string expression, double expected) => AssertClose(expected, evaluator.Evaluate(expression));
    [Fact] public void Radians() => AssertClose(1, evaluator.Evaluate("sin(pi/2)", AngleUnit.Radians));
    [Fact] public void CultureSpecificArguments() => AssertClose(3, evaluator.Evaluate("logbase(8;2)+0,0", culture: CultureInfo.GetCultureInfo("sv-SE")));
    [Theory]
    [InlineData("1/0")] [InlineData("sqrt(-1)")] [InlineData("ln(0)")] [InlineData("logbase(4,1)")]
    [InlineData("(-1)^.5")] [InlineData("root(-4,2)")] [InlineData("root(4,0)")] [InlineData("0^0")]
    [InlineData("(-1)!")] [InlineData("1.5!")] [InlineData("171!")] [InlineData("tan(90)")]
    [InlineData("asin(2)")] [InlineData("acosh(.5)")] [InlineData("atanh(1)")] [InlineData("1%0")]
    [InlineData("exp(1000)")] [InlineData("exp(-1000)")] [InlineData("10^1000")] [InlineData("1e-400")]
    [InlineData("1e-300*1e-300")] [InlineData("1e-300/1e300")] [InlineData("1e-300^2")]
    [InlineData("2(3)")] [InlineData("(2+3")] [InlineData("2+")] [InlineData("unknown(2)")]
    [InlineData("sin(1,2)")] [InlineData("root(2)")] [InlineData("System.Exit(0)")] [InlineData("1e+")]
    public void RejectsInvalidDomainsAndSyntax(string expression) => Assert.Throws<CalculationException>(() => evaluator.Evaluate(expression));
    [Fact] public void BoundedWork()
    {
        Assert.Throws<CalculationException>(() => evaluator.Evaluate(new string('(', 100) + "1" + new string(')', 100)));
        Assert.Throws<CalculationException>(() => evaluator.Evaluate(new string('1', 3000)));
        Assert.Throws<CalculationException>(() => evaluator.Evaluate(new string('-', 100) + "1"));
    }
    [Fact] public void FloatingPointApproximation() => AssertClose(.3, evaluator.Evaluate(".1+.2"));
    [Fact] public void FactorialBoundary() => Assert.True(double.IsFinite(evaluator.Evaluate("170!")));
    [Fact] public void ZeroRoot() => Assert.Equal(0, evaluator.Evaluate("root(0,3)"));
    internal static void AssertClose(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, 1e-12 * Math.Max(1, Math.Abs(expected)));
}
