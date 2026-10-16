# Aster Numeric Behavior — Decimal Precision, Scientific Expressions and Integer Modes

Aster uses different calculation engines for decimal arithmetic, scientific expressions, fixed-width integers, unit conversion and dates. This reference describes their input limits, rounding, range checks and result reuse so you can choose the appropriate mode.

[Aster overview and setup](../README.md) · [Build and test guide](development.md)

## Choose a precision model

| Mode | Representation | Practical consequence |
|---|---|---|
| Standard | Bounded base-10 `BigDecimal` backed by `BigInteger` | Exact addition, subtraction and multiplication within bounds; division and square root round to 100 significant digits. |
| Scientific | Binary64 `double` | Approximate real-number expressions, with roughly 15 significant decimal digits. |
| Programmer | 8/16/32/64-bit integer word | Arithmetic wraps modulo the word size; signed decimal uses two's complement. |
| Converter | Binary64 factors and offsets | Approximate numeric results with domain and range checks. |
| Dates | Gregorian date-only arithmetic | Calendar dates from year 0001 to 9999, without time zones or daylight-saving adjustments. |

The 100-digit input limit belongs to Standard. It does not increase Scientific or Converter precision. The display-precision preference changes presentation rather than the underlying engine value.

## Contents

- [Standard](#standard)
- [Scientific](#scientific)
- [Programmer](#programmer)
- [Converter and Dates](#converter-and-dates)
- [Result display](#result-display)
- [Implementation and regression tests](#implementation-and-regression-tests)

## Standard

- Leading zeros do not count toward the 100-digit mantissa limit; entered trailing zeros do. Excess precision is rejected. Pasting a copied result follows the new-input limit; recalling a stored result can exceed it.
- Normalized coefficients are limited to **2,000 digits**, scale to **−1,000 through 2,000**, and nonzero adjusted decimal exponents to **−1,000 through +1,000**. `1e-1000` and `9e1000` are supported. Bounds apply to intermediate and final values; violations produce an error. Exact terminating division results need no padding.
- Replacing a pending operator does not execute it. Repeated equals repeats the last operation/right operand. `200 − 10 % =` gives `180`; multiplication gives `20`; division gives `2000`.
- A unary/percentage result is a complete operand; a new digit replaces it. A new digit after equals starts a calculation. Backspace edits only an editable input; CE preserves a pending operation, while C/Escape resets it.

## Scientific

- Expressions are limited to **2,048 characters** and **64 recursive levels**. The parser does not execute scripts or dynamically evaluate code.
- Factorial accepts integers **0–170**. `0^0` is rejected; `-2^2` is `-4`, and `2^-2` is `0.25`. Tangent is rejected near singularities when `|cos(angle)| < 10^-14`. Hyperbolic functions are dimensionless.
- Binary64 rounding, cancellation and subnormal behavior apply. Invalid domains, nonfinite results, overflow and detected underflow produce readable errors.
- Enter reevaluates the current expression. Keypad operators can continue from the last result; keypad numeric input after equals begins a fresh expression. Memory stores the decimal round-trip representation of the approximate result. Out-of-range or underflowing recall into Scientific is rejected.

## Programmer

- Parsed out-of-range integers and all results are masked to the chosen width. Division truncates toward zero. Right shift is arithmetic when signed and logical when unsigned.
- Shift counts must be `0..width−1`. Widening sign-extends negative signed values; narrowing masks. Changing width or signedness cancels a pending operation.
- Binary display is grouped in four-bit blocks and padded to the word size. Incomplete input cannot copy a stale result.

## Converter and Dates

- Valid identity conversions preserve the input after domain validation. Offset-free conversions use a direct scale ratio to avoid unnecessary intermediate overflow/underflow.
- Factors distinguish the international foot/pound, thermochemical calorie, BTU (IT), mechanical/metric horsepower, and US/Imperial volume. A Julian year is exactly 365.25 days. A small negative base-temperature value caused by offset cancellation is clamped to absolute zero within four machine epsilons of the input offset; Kelvin input below zero is rejected.
- Date differences are absolute and elapsed by default; inclusive counting adds one day. Interval adjustment applies years, then months, then weeks/days, clamping at each year/month boundary.

## Result display

Results are selectable and read-only. Text fits and wraps; previous/next digit pages expose any remaining characters without scrollbars. Selecting text copies that selection; **Copy result** and history-card copy buttons preserve the complete stored value. Long expression captions may be abbreviated.

Scientific and Converter copy/history use round-trip (`G17`) values. Standard memory and history retain the full bounded decimal result, including results longer than the limit for a new input. Recalling a value and pasting its text therefore follow different input rules.


## Implementation and regression tests

The implementation and tests are the maintained evidence for these contracts:

| Area | Implementation | Regression examples |
|---|---|---|
| Decimal arithmetic and input state | [BigDecimal](../src/ModernCalculator.Core/Calculations/BigDecimal.cs), [StandardCalculator](../src/ModernCalculator.Core/Calculations/StandardCalculator.cs) | [BigDecimal tests](../tests/ModernCalculator.Tests/BigDecimalTests.cs), [Standard tests](../tests/ModernCalculator.Tests/StandardTests.cs) |
| Scientific grammar and domains | [ExpressionEvaluator](../src/ModernCalculator.Core/Expressions/ExpressionEvaluator.cs) | [Scientific tests](../tests/ModernCalculator.Tests/ScientificTests.cs) |
| Integer arithmetic and bases | [ProgrammerCalculator](../src/ModernCalculator.Core/Modes/ProgrammerCalculator.cs) | [Programmer tests](../tests/ModernCalculator.Tests/ProgrammerTests.cs) |
| Units and calendar dates | [UnitConverter](../src/ModernCalculator.Core/Conversions/UnitConverter.cs), [DateCalculator](../src/ModernCalculator.Core/Modes/DateCalculator.cs) | [Conversion and date tests](../tests/ModernCalculator.Tests/ConversionAndDateTests.cs), [converter audit tests](../tests/ModernCalculator.Tests/ConverterAuditTests.cs) |
| Long result presentation | [ResultDisplay](../src/ModernCalculator.WinForms/Controls/ResultDisplay.cs) | [Hidden component-check methodology](../tests/ModernCalculator.UiChecks/README.md) |

See [the development guide](development.md#run-the-engine-and-persistence-tests) for the test command. A passing engine suite does not independently validate every displayed control or physical-input route; those need the component and interactive checks described there.
