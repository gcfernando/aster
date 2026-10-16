using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Expressions;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

/// <summary>Focused UI-component regressions for Scientific/Programmer audit changes.</summary>
public static class ScientificProgrammerAuditChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("Audit Scientific Ans token boundaries and localized binary templates", CheckScientificEditing);
        run("Audit Scientific labeled editor, one factorial, inverse functions and Ans retention", CheckScientificWorkflow);
        run("Audit Scientific function templates and exponent operand prompts", CheckScientificTemplates);
        run("Audit Scientific captured answer sign, Unicode operands and localized tiny Ans", CheckScientificResultEditing);
        run("Audit Programmer editable base, exact negative 64-bit values and invalid input", CheckProgrammerEditing);
        run("Audit Programmer arithmetic/logical shifts, wrapping and pending expression", CheckProgrammerOperations);
        run("Audit Programmer all 64 bits reachable with position and sign weights", CheckProgrammerBits);
        run("Audit Programmer compact 242/246-DIP keypad targets and text fit", CheckProgrammerCompactKeypad);
        run("Independent Scientific memory captures successful Ans results and rejects errors", CheckScientificMemoryResult);
        run("Independent Programmer NOT chaining and native leading-sign editing", CheckProgrammerUnaryAndNativeEditing);
        run("Independent Standard transformed result expression and undo", CheckStandardTransformedResult);
    }

    private static void CheckScientificEditing()
    {
        var culture = CultureInfo.InvariantCulture;
        var evaluator = new ExpressionEvaluator();
        var expanded = ScientificEditing.ExpandAnswer("Ans^2+asinh(0)", -3, culture);
        Require(evaluator.Evaluate(expanded) == 9, "Ans expansion changed precedence or replaced part of asinh.");
        Require(ScientificEditing.ExpandAnswer("answer+1", 3, culture) == "answer+1", "Ans replaced part of another identifier.");
        Throws<CalculationException>(() => ScientificEditing.ExpandAnswer("Ans+1", null, culture));
        foreach (var locale in new[] { "en-US", "sv-SE" })
        {
            var current = CultureInfo.GetCultureInfo(locale);
            foreach (var (function, expected) in new[] { ("root", 2d), ("logbase", 4d), ("mod", 1d) })
            {
                var template = ScientificEditing.BinaryTemplate(function, "", current);
                Require(evaluator.Evaluate(template.Text, culture: current) == expected, "Scientific template has incorrect argument order: " + template.Text);
                Require(template.SelectionLength > 0 && template.Prompt.Contains("first", StringComparison.Ordinal), "Template has no editable operand or order guidance.");
            }
        }
    }

    private static void CheckScientificWorkflow()
    {
        using var host = Host(new ScientificView(new WorkspaceContext()));
        var view = Find<ScientificView>(host);
        var editor = Named<TextBox>(view, "Scientific expression");
        Require(!editor.ReadOnly && Named<Label>(view, "Scientific expression label").Text == "Expression", "Scientific editor is not clearly labeled and editable.");
        Require(Named<Label>(view, "Scientific result label").Text == "Result", "Scientific result label is missing.");
        var factorials = All(view).OfType<CalcButton>().Count(button => button.AccessibleName == "Factorial");
        Require(factorials == 1, $"Expected exactly one reachable factorial; found {factorials}. Function controls: " + string.Join(", ", All(view).OfType<CalcButton>().Select(button => button.AccessibleName)));
        Require(Named<Label>(view, "Angle mode label").Text == "Angle mode" && Named<Label>(view, "Function category label").Text == "Function category", "Angle/category groups are not visibly labeled.");
        editor.Text = "2+3"; view.Command(Keys.Enter);
        Require(Parse(view.CopyText) == 5, "Initial Scientific answer did not evaluate.");
        editor.Text = "1/0"; view.Command(Keys.Enter); Require(view.CopyText.Length == 0, "Scientific error exposed an old result for copying.");
        view.Command(Keys.Escape);
        Click(Named<CalcButton>(view, "Recall last successful answer"));
        editor.Text += "+2"; view.Command(Keys.Enter);
        Require(Parse(view.CopyText) == 7, "Ans was lost after error, editing or clear.");
        var categories = Named<SegmentedControl>(view, "Scientific function group");
        categories.SelectedIndex = 1;
        foreach (var function in new[] { "asin", "acos", "atan" }) _ = Named<CalcButton>(view, function);
        categories.SelectedIndex = 2;
        foreach (var function in new[] { "asinh", "acosh", "atanh" }) _ = Named<CalcButton>(view, function);
        editor.Text = "-2^2 + 2^-2"; view.Command(Keys.Enter);
        Require(Parse(view.CopyText) == -3.75, "UI editing changed scientific precedence.");
        CheckScientificPrimaryLabels();
    }

    private static void CheckScientificPrimaryLabels()
    {
        foreach (var logicalWidth in new[] { 480, 650 })
        foreach (var logicalHeight in new[] { 242, 246, 650 })
        {
            using var host = Host(new ScientificView(new WorkspaceContext()));
            var view = Find<ScientificView>(host); var scale = view.DeviceDpi / 96d;
            host.ClientSize = new((int)Math.Round(logicalWidth * scale), (int)Math.Round(logicalHeight * scale));
            Layout(host);
            var compact = logicalHeight < 360;
            foreach (var (name, expected) in new[]
            {
                ("Scientific result label", "Result"), ("Scientific expression label", "Expression"),
                ("Angle mode label", compact ? "Angle" : "Angle mode"),
                ("Function category label", compact ? "Category" : "Function category")
            })
            {
                var label = Named<Label>(view, name);
                Require(label.Text == expected && !label.AutoEllipsis,
                    $"Scientific primary label was abbreviated or ellipsized: {name}, text='{label.Text}', expected='{expected}'.");
                Require(label.ClientSize.Width > 0 && label.ClientSize.Height > 0 && label.Font.Height > 0,
                    "Scientific primary label has empty bounds or invalid font: " + name);
                using var graphics = label.CreateGraphics();
                var measured = TextRenderer.MeasureText(graphics, label.Text, label.Font,
                    new Size(label.ClientSize.Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                Require(measured.Width <= label.ClientSize.Width && Math.Max(label.Font.Height, measured.Height) <= label.ClientSize.Height,
                    $"Scientific primary label is clipped: {name}, workspace={logicalWidth}x{logicalHeight}, client={label.ClientSize}, fullText={measured}, fontHeight={label.Font.Height}.");
                for (Control current = label; current.Parent is { } parent; current = parent)
                    Require(Rectangle.Inflate(parent.ClientRectangle, 1, 1).Contains(current.Bounds), "Scientific primary label extends outside an ancestor viewport: " + name);
            }
        }
    }

    private static void CheckScientificTemplates()
    {
        using var host = Host(new ScientificView(new WorkspaceContext()));
        var view = Find<ScientificView>(host);
        var editor = Named<TextBox>(view, "Scientific expression");
        foreach (var (name, expected) in new[] { ("Root with chosen degree", 2d), ("Logarithm with chosen base", 4d), ("Remainder", 1d) })
        {
            view.Command(Keys.Escape); Click(Named<CalcButton>(view, name));
            Require(editor.SelectionLength > 0, "Function template did not select an editable operand.");
            Require(Named<Label>(view, "Scientific syntax help").Text.Contains("first", StringComparison.Ordinal), "Function template did not explain argument order.");
            view.Command(Keys.Enter); Require(Math.Abs(Parse(view.CopyText) - expected) < 1e-14, "Function template evaluated incorrectly: " + name);
        }
        view.Command(Keys.Escape); editor.Text = "2"; editor.SelectionStart = editor.TextLength;
        Click(Named<CalcButton>(view, "Enter scientific exponent")); view.Command(Keys.Enter);
        Require(Parse(view.CopyText) == 2000, "Scientific exponent entry was confused with exp(x).");
        Require(Named<CalcButton>(view, "Enter scientific exponent").AccessibleDescription?.Contains("differs", StringComparison.Ordinal) == true, "Exponent tooltip does not distinguish e to the power x.");
    }

    private static void CheckScientificMemoryResult()
    {
        var context = new WorkspaceContext(); var status = ""; context.StatusChanged += text => status = text;
        using var host = Host(new ScientificView(context));
        var view = Find<ScientificView>(host); var editor = Named<TextBox>(view, "Scientific expression");
        editor.Text = "5"; view.Command(Keys.Enter); editor.Text = "Ans+2"; view.Command(Keys.Enter);
        Require(Parse(view.CopyText) == 7, "Scientific memory setup did not capture result 7.");
        Click(Named<CalcButton>(view, "Store in new memory slot"));
        Require(context.Memory.RecallExact() == BigDecimal.Parse("7"), "MS reevaluated Ans instead of storing displayed result 7.");
        Click(Named<CalcButton>(view, "Add to newest memory"));
        Require(context.Memory.RecallExact() == BigDecimal.Parse("14"), "M+ reevaluated a successful Ans expression.");
        Click(Named<CalcButton>(view, "Subtract from newest memory"));
        Require(context.Memory.RecallExact() == BigDecimal.Parse("7"), "M− reevaluated a successful Ans expression.");
        Click(Named<CalcButton>(view, "Recall newest memory")); view.Command(Keys.Enter);
        Require(Parse(view.CopyText) == 7, "MR did not return the successful displayed value.");
        editor.Text = "Ans+1"; Click(Named<CalcButton>(view, "Store in new memory slot"));
        Require(context.Memory.RecallExact() == BigDecimal.Parse("8") && view.CopyText.Length == 0,
            "Memory lost valid uncomputed-expression evaluation or falsely marked it calculated.");
        editor.Text = "1/0"; view.Command(Keys.Enter); Click(Named<CalcButton>(view, "Store in new memory slot"));
        Require(context.Memory.Items.Count == 2 && context.Memory.RecallExact() == BigDecimal.Parse("8") && status.Contains("zero", StringComparison.OrdinalIgnoreCase),
            "Scientific memory accepted a stale result after a failed expression.");
        editor.Text = "Ans"; view.Command(Keys.Enter); Require(Parse(view.CopyText) == 7, "Memory actions or an error unexpectedly updated Ans.");
    }

    private static void CheckProgrammerUnaryAndNativeEditing()
    {
        using var host = Host(new ProgrammerView(new WorkspaceContext()));
        var view = Find<ProgrammerView>(host);
        void PendingNot()
        {
            view.ReuseDecimalResult("5"); view.Character('+'); view.Character('3'); Click(Named<CalcButton>(view, "NOT"));
            Require(Named<Label>(view, "Programmer expression and integer model").Text.Contains("5 + -4", StringComparison.Ordinal), "NOT right operand is missing from the pending expression.");
        }
        PendingNot(); Click(Named<CalcButton>(view, "×")); view.Character('2'); view.Command(Keys.Enter);
        Require(view.CopyText == "2", "A binary operator after NOT dropped the pending left operand.");
        PendingNot(); view.Command(Keys.Enter); Require(view.CopyText == "1", "Equals stopped using the NOT-transformed right operand.");
        PendingNot(); view.Character('2'); view.Command(Keys.Enter); Require(view.CopyText == "7", "A new digit stopped replacing the NOT-transformed operand.");
        view.ReuseDecimalResult("5"); view.Character('+'); Click(Named<CalcButton>(view, "×")); view.Character('3'); view.Command(Keys.Enter);
        Require(view.CopyText == "15", "Replacing an unfilled pending operator changed behavior.");
        view.ReuseDecimalResult("5"); view.Character('+'); view.Character('3'); view.Command(Keys.Alt | Keys.D2);
        Click(Named<CalcButton>(view, "×")); view.Character('2'); view.Command(Keys.Enter); view.Command(Keys.Alt | Keys.D1);
        Require(view.CopyText == "16", "Changing base discarded an already entered pending right operand.");

        var dec = Named<TextBox>(view, "Decimal representation, calculated");
        view.ReuseDecimalResult("123"); dec.Select(0, 0);
        RaiseEditingKey(dec, "OnKeyDown", new KeyEventArgs(Keys.Home));
        var minus = new KeyPressEventArgs('-'); RaiseEditingKey(dec, "OnKeyPress", minus);
        Require(!minus.Handled, "Explicit Home-minus was consumed as subtraction instead of a native leading sign edit.");
        dec.SelectedText = "-";
        Require(view.CopyText == "-123" && !Named<Label>(view, "Programmer expression and integer model").Text.Contains("…", StringComparison.Ordinal),
            "Leading sign insertion changed the pending operation or failed to update the exact integer.");
        view.ReuseDecimalResult("5"); view.Character('+'); view.Character('3'); view.Command(Keys.Enter); dec.Select(0, 0);
        minus = new KeyPressEventArgs('-'); RaiseEditingKey(dec, "OnKeyPress", minus);
        Require(minus.Handled, "Post-equals subtraction was mistaken for a leading sign edit.");
        view.Character('2'); view.Command(Keys.Enter); Require(view.CopyText == "6", "Keyboard subtraction after a completed result changed behavior.");
    }

    private static void CheckStandardTransformedResult()
    {
        var context = new WorkspaceContext(); using var host = Host(new StandardView(context));
        var view = Find<StandardView>(host);
        foreach (var character in "5+2") view.Character(character); view.Command(Keys.Enter); view.Command(Keys.F9);
        var trace = Named<Label>(view, "Standard expression");
        Require(view.CopyText == "-7" && trace.Text == "Negate(7) =", "Standard sign change left the previous addition equation visible.");
        view.Command(Keys.Control | Keys.Z); Require(view.CopyText == "7" && trace.Text == "5 + 2 =", "Undo lost the original result trace.");
        view.Command(Keys.Control | Keys.Y); Require(view.CopyText == "-7" && trace.Text == "Negate(7) =", "Redo lost the transformed result trace.");
        view.Character('2'); Require(view.CopyText == "-72" && trace.Text == "Ready", "Editing a sign-changed result retained a stale completed equation.");
        Require(context.History.Items.Count == 1, "Sign editing or undo/redo duplicated completed history.");
    }

    private static void RaiseEditingKey(Control editor, string method, EventArgs args)
    {
        var handler = typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Control." + method + " is unavailable.");
        try { handler.Invoke(editor, [args]); }
        catch (TargetInvocationException exception) when (exception.InnerException is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); }
    }

    private static void CheckProgrammerEditing()
    {
        using var host = Host(new ProgrammerView(new WorkspaceContext()));
        var view = Find<ProgrammerView>(host);
        var dec = Named<TextBox>(view, "Decimal representation, calculated");
        Require(!dec.ReadOnly && dec.TabStop, "Active decimal input remains read-only or unreachable by Tab.");
        dec.Text = "-9223372036854775808";
        Require(view.CopyText == "-9223372036854775808", "The signed 64-bit minimum narrowed or rounded.");
        Require(Named<TextBox>(view, "Hexadecimal representation, calculated").Text == "8000000000000000", "Negative 64-bit two's complement does not synchronize exactly.");
        Require(view.Command(Keys.Alt | Keys.D2), "Programmer hexadecimal shortcut is missing.");
        var hex = Named<TextBox>(view, "Hexadecimal representation, calculated");
        Require(!hex.ReadOnly && dec.ReadOnly, "Changing base did not transfer editing to the selected row.");
        hex.Text = "FFFFFFFFFFFFFFFF"; Require(dec.Text == "-1", "All 64 raw bits set should represent signed -1.");
        hex.Text = "GG"; Require(view.CopyText.Length == 0 && dec.Text == "-1", "Invalid base digits changed the last valid integer or stayed copyable.");
        Require(!Named<CalcButton>(view, "Copy Hexadecimal value").Enabled, "Invalid active input leaves a stale representation copyable.");
        hex.Text = "-"; Require(view.CopyText.Length == 0 && !Named<CalcButton>(view, "Copy Decimal value").Enabled, "Incomplete signed input remains copyable.");
        hex.Text = "FF"; Require(view.CopyText == "FF" && dec.Text == "255", "Programmer input did not recover after invalid digits.");
        Require(Named<CalcButton>(view, "Copy Hexadecimal value").Enabled, "Valid input did not restore representation copying.");
        Require(view.Command(Keys.Alt | Keys.D3) && !Named<TextBox>(view, "Octal representation, calculated").ReadOnly, "Octal shortcut is missing.");
        Require(view.Command(Keys.Alt | Keys.D4) && !Named<TextBox>(view, "Binary representation, calculated").ReadOnly, "Binary shortcut is missing.");
        Require(view.Command(Keys.Alt | Keys.D1) && !dec.ReadOnly, "Decimal shortcut is missing.");
    }

    private static void CheckScientificResultEditing()
    {
        using (var host = Host(new ScientificView(new WorkspaceContext())))
        {
            var view = Find<ScientificView>(host); var editor = Named<TextBox>(view, "Scientific expression");
            editor.Text = "5"; view.Command(Keys.Enter);
            editor.Text = "Ans+2"; view.Command(Keys.Enter);
            Require(Parse(view.CopyText) == 7, "Ans setup did not produce 7.");
            Click(Named<CalcButton>(view, "+/−")); view.Command(Keys.Enter);
            Require(Parse(view.CopyText) == -7, "Sign change reevaluated an Ans expression using the newly updated answer.");
            foreach (var (operand, expected) in new[] { ("2÷3", 2000d / 3), ("2−3", -1000d), ("(2)", 2000d), ("2E2", 200000d) })
            {
                view.Command(Keys.Escape); editor.Text = operand; editor.SelectAll();
                Click(Named<CalcButton>(view, "Enter scientific exponent"));
                Require(editor.SelectionLength == 1 && editor.SelectedText == "3", "Exponent template lost its editable exponent.");
                view.Command(Keys.Enter);
                Require(Math.Abs(Parse(view.CopyText) - expected) <= Math.Abs(expected) * 1e-14,
                    "Exponent entry changed selected operand precedence: " + operand);
            }
        }
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var custom = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone(); custom.NumberFormat.NegativeSign = "~";
            foreach (var culture in new[] { CultureInfo.GetCultureInfo("sv-SE"), custom })
            {
                CultureInfo.CurrentCulture = culture;
                using var host = Host(new ScientificView(new WorkspaceContext()));
                var view = Find<ScientificView>(host); var editor = Named<TextBox>(view, "Scientific expression");
                editor.Text = "1e-100"; view.Command(Keys.Enter);
                editor.Text = "Ans"; view.Command(Keys.Enter);
                Require(Parse(view.CopyText) == 1e-100, "Localized negative exponent corrupted Ans: " + culture.Name);
                view.Command(Keys.Escape); view.ReuseExact(BigDecimal.Parse("-1e-100")); view.Command(Keys.Enter);
                Require(Parse(view.CopyText) == -1e-100, "Localized exact recall generated an unreadable signed exponent: " + culture.Name);
                Click(Named<CalcButton>(view, "x²")); view.Command(Keys.Enter);
                Require(Math.Abs(Parse(view.CopyText) - 1e-200) < 1e-213, "Result continuation generated an unreadable localized exponent: " + culture.Name);
            }
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private static void CheckProgrammerOperations()
    {
        var context = new WorkspaceContext(); var status = ""; context.StatusChanged += text => status = text;
        using var host = Host(new ProgrammerView(context));
        var view = Find<ProgrammerView>(host);
        view.ReuseDecimalResult("-2"); Click(Named<CalcButton>(view, ">>")); view.Character('1'); view.Command(Keys.Enter);
        Require(view.CopyText == "-1", "Signed right shift is not arithmetic.");
        Named<SegmentedControl>(view, "Integer interpretation").SelectedIndex = 1;
        view.ReuseDecimalResult("-2"); Click(Named<CalcButton>(view, ">>")); view.Character('1'); view.Command(Keys.Enter);
        Require(view.CopyText == "9223372036854775807", "Unsigned right shift is not logical.");
        Named<SegmentedControl>(view, "Integer width").SelectedIndex = 0;
        view.ReuseDecimalResult("255"); view.Character('+');
        Require(Named<Label>(view, "Programmer expression and integer model").Text.Contains("255 +", StringComparison.Ordinal), "Pending programmer operands are not visible.");
        view.Character('1'); view.Command(Keys.Enter);
        Require(view.CopyText == "0" && status.Contains("wrapped", StringComparison.OrdinalIgnoreCase), "Unsigned 8-bit overflow did not wrap with feedback.");
        view.ReuseDecimalResult("5"); view.Character('/'); view.Character('0'); view.Command(Keys.Enter);
        Require(view.CopyText.Length == 0 && status.Contains("zero", StringComparison.OrdinalIgnoreCase), "Integer divide-by-zero feedback is missing.");
    }

    private static void CheckProgrammerBits()
    {
        using var host = Host(new ProgrammerView(new WorkspaceContext()));
        var view = Find<ProgrammerView>(host);
        Named<SegmentedControl>(view, "Programmer workspace").SelectedIndex = 1;
        var range = Named<SegmentedControl>(view, "Bit range");
        var visited = new HashSet<int>();
        for (var page = 0; page < 2; page++)
        {
            range.SelectedIndex = page; Layout(host);
            var buttons = All(view).OfType<CalcButton>().Where(button => button.AccessibleName?.StartsWith("Toggle bit ", StringComparison.Ordinal) == true).ToArray();
            Require(buttons.Length == 32, "Bit page must expose 32 directly reachable controls.");
            foreach (var button in buttons)
            {
                var index = int.Parse(button.AccessibleName![11..], CultureInfo.InvariantCulture); visited.Add(index);
                Require(button.TabStop && button.AccessibleDescription?.Contains("Place value", StringComparison.Ordinal) == true, "Bit position/place-value guidance or keyboard access is missing.");
            }
        }
        Require(visited.SetEquals(Enumerable.Range(0, 64)), "Some of the 64 bits cannot be reached.");
        var sign = Named<CalcButton>(view, "Toggle bit 63"); Click(sign);
        Require(view.CopyText == "-9223372036854775808" && sign.Selected, "Sign-bit toggle did not preserve exact two's-complement meaning or selected state.");
        Require(sign.AccessibleDescription?.Contains("-9223372036854775808", StringComparison.Ordinal) == true, "Sign bit tooltip lacks its signed weight.");
        range.SelectedIndex = 0; Click(Named<CalcButton>(view, "Toggle bit 0"));
        Require(view.CopyText == "-9223372036854775807", "Low-bit page did not edit the same exact integer.");
    }

    private static void CheckProgrammerCompactKeypad()
    {
        foreach (var logicalHeight in new[] { 242, 246 })
        foreach (var logicalWidth in new[] { 480, 650 })
        {
            using var host = Host(new ProgrammerView(new WorkspaceContext()));
            var view = Find<ProgrammerView>(host);
            var scale = view.DeviceDpi / 96d;
            host.ClientSize = new((int)Math.Round(logicalWidth * scale), (int)Math.Round(logicalHeight * scale));
            Layout(host);
            var and = Named<CalcButton>(view, "AND");
            var keypad = and.Parent ?? throw new InvalidOperationException("Programmer keypad is detached.");
            var buttons = keypad.Controls.OfType<CalcButton>().ToArray();
            Require(buttons.Length == 30, "Compact layout lost a programmer keypad action.");
            var minimum = (int)Math.Ceiling(16 * scale);
            foreach (var button in buttons)
            {
                Require(button.Width >= minimum && button.Height >= minimum,
                    $"Compact {logicalHeight}-DIP keypad target is smaller than 16 DIP: {button.AccessibleName}, bounds={button.Bounds}, DPI={view.DeviceDpi}.");
                Require(keypad.ClientRectangle.Contains(button.Bounds), "Compact keypad action extends beyond its viewport: " + button.AccessibleName);
                if (string.IsNullOrWhiteSpace(button.Text)) continue;
                var measured = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                Require(button.ClientSize.Height >= Math.Max(button.Font.Height, measured.Height) && button.ClientSize.Width >= measured.Width,
                    $"Compact keypad label is clipped: {button.AccessibleName}, client={button.ClientSize}, measured={measured}, fontHeight={button.Font.Height}.");
            }
            Require(view.CopyText == "0", "Compact resize changed the programmer value.");
            view.ReuseDecimalResult("255");
            var compactSize = host.ClientSize;
            foreach (var size in new[] { new Size((int)Math.Round(650 * scale), (int)Math.Round(650 * scale)), compactSize })
            {
                // Keep the nonzero value through both sizes; do not repair native layout by reassigning it.
                host.ClientSize = size; Layout(host);
                foreach (var (inputBase, expected) in new[] { (Keys.D1, "255"), (Keys.D2, "FF"), (Keys.D3, "377"), (Keys.D4, "11111111") })
                {
                    view.Command(Keys.Alt | inputBase); Layout(host);
                    CheckProgrammerNativeRepresentations(view, $"target={logicalWidth}x{logicalHeight}, actual={size}, input={inputBase}");
                    Require(view.CopyText == expected, "Native representation resizing or base change altered integer255.");
                }
            }
            view.ReuseDecimalResult("-1");
            foreach (var inputBase in new[] { Keys.D1, Keys.D4 })
            {
                view.Command(Keys.Alt | inputBase); Layout(host);
                var binary = Named<TextBox>(view, "Binary representation, calculated");
                var first = binary.GetPositionFromCharIndex(0);
                var last = binary.GetPositionFromCharIndex(binary.TextLength - 1);
                Require(first.X >= 0 && first.Y >= 0 && last.X >= 0 && last.X < binary.ClientSize.Width && last.Y >= 0 && last.Y + binary.Font.Height <= binary.ClientSize.Height,
                    $"Compact grouped64-bit text is clipped: workspace={logicalWidth}x{logicalHeight}, editable={!binary.ReadOnly}, first={first}, last={last}, fontHeight={binary.Font.Height}, viewport={binary.ClientRectangle}.");
                Require(view.CopyText == (inputBase == Keys.D4 ? new string('1', 64) : "-1"), "Compact binary inspection changed exact64-bit value.");
            }
            Named<SegmentedControl>(view, "Programmer workspace").SelectedIndex = 1;
            Handles(view); Layout(host);
            var visited = new HashSet<int>();
            var range = Named<SegmentedControl>(view, "Bit range");
            for (var page = 0; page < 2; page++)
            {
                range.SelectedIndex = page; Handles(view); Layout(host);
                var bits = All(view).OfType<CalcButton>().Where(button => button.AccessibleName?.StartsWith("Toggle bit ", StringComparison.Ordinal) == true).ToArray();
                Require(bits.Length == 32, "Compact bit page lost a directly reachable target.");
                foreach (var line in bits.GroupBy(button => (TableLayoutPanel)button.Parent!))
                {
                    var ordered = line.OrderBy(button => line.Key.GetColumn(button)).ToArray();
                    var grid = (TableLayoutPanel)line.Key.Parent!;
                    var cellWidth = (line.Key.ClientSize.Width - line.Key.GetColumnWidths()[0]) / ordered.Length;
                    var availableWidth = cellWidth - ordered.Max(button => button.Margin.Horizontal);
                    var availableHeight = grid.ClientSize.Height / grid.RowCount - ordered.Max(button => button.Margin.Vertical);
                    var preferred = (int)Math.Ceiling(24 * scale);
                    var fitsPreferred = availableWidth >= preferred && availableHeight >= preferred;
                    if (logicalWidth == 650) Require(fitsPreferred, $"Compact 650-DIP bit workspace did not reserve room for 24-DIP targets: available={availableWidth}x{availableHeight}, required={preferred}.");
                    var required = fitsPreferred ? preferred : minimum;
                    foreach (var button in ordered)
                    {
                        visited.Add(int.Parse(button.AccessibleName![11..], CultureInfo.InvariantCulture));
                        Require(button.TabStop && button.Width >= required && button.Height >= required,
                            $"Compact bit target is too small or not keyboard reachable: {button.AccessibleName}, bounds={button.Bounds}, required={required}, workspace={logicalWidth}x{logicalHeight}.");
                        for (Control current = button; current.Parent is { } parent; current = parent)
                            Require(Rectangle.Inflate(parent.ClientRectangle, 1, 1).Contains(current.Bounds), "Compact bit control extends outside an ancestor viewport: " + button.AccessibleName);
                        var measured = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty,
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        Require(button.ClientSize.Width >= measured.Width && button.ClientSize.Height >= Math.Max(button.Font.Height, measured.Height),
                            "Compact native bit font is clipped: " + button.AccessibleName);
                    }
                    var ordinaryGap = Enumerable.Range(1, ordered.Length - 1).Where(index => index % 4 != 0)
                        .Max(index => ordered[index].Left - ordered[index - 1].Right);
                    foreach (var index in Enumerable.Range(1, ordered.Length - 1).Where(index => index % 4 == 0))
                        Require(ordered[index].Left - ordered[index - 1].Right > ordinaryGap, "Compact bit editor lost a visible nibble grouping boundary.");
                }
            }
            Require(visited.SetEquals(Enumerable.Range(0, 64)), "Compact bit ranges do not expose all 64 positions.");
            Require(view.CopyText == new string('1', 64), "Compact workspace navigation changed the exact binary value.");
        }
    }

    private static void CheckProgrammerNativeRepresentations(ProgrammerView view, string source)
    {
        foreach (var name in new[] { "Decimal", "Hexadecimal", "Octal", "Binary" })
        {
            var field = Named<TextBox>(view, name + " representation, calculated");
            Require(field.TextLength > 0 && field.Font.Height > 0, "Programmer representation is empty or has an invalid font: " + name);
            var formatting = new NativeEditRectangle();
            _ = GetNativeEditRectangle(field.Handle, 0x00B2, 0, ref formatting); // EM_GETRECT
            Require(formatting.Right > formatting.Left && formatting.Bottom > formatting.Top &&
                Math.Abs(formatting.Left) <= 4 && Math.Abs(formatting.Top) <= 4 &&
                Math.Abs(formatting.Right - field.ClientSize.Width) <= 4 && Math.Abs(formatting.Bottom - field.ClientSize.Height) <= 4,
                $"Native programmer formatting rectangle is stale: {name}, {source}, formatting={formatting}, client={field.ClientRectangle}.");
            var first = field.GetPositionFromCharIndex(0);
            var last = field.GetPositionFromCharIndex(field.TextLength - 1);
            using var graphics = field.CreateGraphics();
            var advance = TextRenderer.MeasureText(graphics, field.Text[^1].ToString(), field.Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
            Require(first.X >= 0 && first.Y >= 0 && first.Y + field.Font.Height <= field.ClientSize.Height &&
                last.X >= 0 && last.Y >= 0 && last.X + advance <= field.ClientSize.Width + 1 && last.Y + field.Font.Height <= field.ClientSize.Height,
                $"Native programmer glyph is clipped: {name}, {source}, first={first}, last={last}, advance={advance}, fontHeight={field.Font.Height}, client={field.ClientRectangle}.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeEditRectangle
    {
        public int Left, Top, Right, Bottom;
        public override readonly string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint GetNativeEditRectangle(nint window, uint message, nint wParam, ref NativeEditRectangle rectangle);

    private static double Parse(string text) => double.Parse(text, CultureInfo.CurrentCulture);
    private static Form Host(Control child)
    {
        var form = new Form { ClientSize = new Size(1000, 900), AutoScaleMode = AutoScaleMode.None };
        child.Dock = DockStyle.Fill; form.Controls.Add(child); Handles(form); Layout(form); return form;
    }
    private static void Handles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) Handles(child); }
    private static void Layout(Control control) { control.PerformLayout(); foreach (Control child in control.Controls) Layout(child); control.PerformLayout(); }
    private static IEnumerable<Control> All(Control control) { foreach (Control child in control.Controls) { yield return child; foreach (var nested in All(child)) yield return nested; } }
    private static T Find<T>(Control control) where T : Control => All(control).OfType<T>().Single();
    private static T Named<T>(Control control, string name) where T : Control => All(control).OfType<T>().Single(item => item.AccessibleName == name);
    private static void Click(CalcButton button)
    {
        Require(button.Enabled, "Test cannot activate disabled control " + button.AccessibleName);
        var method = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Control.OnClick is unavailable.");
        try { method.Invoke(button, [EventArgs.Empty]); }
        catch (TargetInvocationException exception) when (exception.InnerException is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); }
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (Exception exception) when (exception is T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
