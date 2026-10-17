using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Forms;
using ModernCalculator.WinForms.Themes;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

public static class CommercialEngineeringChecks
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(Action<string, Action> check)
    {
        foreach (var mode in new[] { "Standard", "Scientific", "Programmer", "Converter", "Dates" })
        {
            var selected = mode;
            check("Commercial engineering: lazy saved startup " + mode, () => WithForm(mode, form =>
            {
                var views = Views(form);
                Require(views.Count == 1 && views.ContainsKey(selected), "Startup instantiated an unused mode.");
                Require(ReferenceEquals(Current(form), views[selected]), "Saved startup mode did not attach its cached view.");
            }));
        }
        check("Commercial engineering: lazy modes preserve input and use current settings", RetainStateAndSettings);
        check("Commercial engineering: dispose every lazily created view", DisposeViews);
        check("Commercial engineering: identical result preserves selection and live font", IdenticalResult);
        check("Commercial engineering: cached fit repairs native rectangle and preserves selection", CachedNativeRectangle);
        check("Commercial engineering: result fit invalidates on resize error font and value", FitInvalidation);
    }

    private static void RetainStateAndSettings() => WithForm("Standard", form =>
    {
        var standard = (StandardView)Current(form);
        standard.Character('9');
        var context = Field<WorkspaceContext>(form, "context");
        context.Settings = context.Settings with { Theme = ThemePreference.Dark, Precision = 5 };
        Invoke(form, "ApplyPalette");
        Invoke(form, "SwitchMode", "Scientific");
        var scientific = (ScientificView)Current(form);
        scientific.Reuse("7");
        Require(scientific.BackColor == Palette.Resolve(ThemePreference.Dark).Surface, "New lazy mode missed the current theme.");
        Require(Descendants(scientific).OfType<ResultDisplay>().Single().AccessibleDescription!.Contains("5 significant digits", StringComparison.Ordinal),
            "New lazy mode missed current precision settings.");
        Invoke(form, "SwitchMode", "Standard");
        Require(ReferenceEquals(Current(form), standard) && standard.CopyText == "9", "Returning to Standard replaced its cached input.");
        Invoke(form, "SwitchMode", "Scientific");
        var expression = Descendants(scientific).OfType<TextBox>().Single(control => control.AccessibleName == "Scientific expression");
        Require(ReferenceEquals(Current(form), scientific) && expression.Text == "7" && Views(form).Count == 2,
            "Returning to Scientific lost input or created unrelated modes.");
    });

    private static void DisposeViews() => WithForm("Standard", form =>
    {
        foreach (var mode in new[] { "Scientific", "Programmer", "Converter", "Dates" }) Invoke(form, "SwitchMode", mode);
        var cached = Views(form).Values.ToArray();
        Require(cached.Length == 5, "Visiting all modes did not construct every view.");
        form.Dispose();
        Require(cached.All(view => view.IsDisposed), "A detached cached view survived MainForm disposal.");
    });

    private static void IdenticalResult()
    {
        using var display = new ResultDisplay();
        using var host = Host(display);
        var value = new string('7', 100);
        display.Set("Before", value);
        var number = Field<TextBox>(display, "number");
        var font = number.Font;
        number.Select(2, 5);
        for (var index = 0; index < 20; index++) display.Set("Updated caption", value);
        Require(number.SelectionStart == 2 && number.SelectionLength == 5 && ReferenceEquals(number.Font, font),
            "An unchanged fit reset selection or replaced its owned font.");
        Require(number.AccessibleDescription!.StartsWith("Updated caption.", StringComparison.Ordinal), "Caching left stale expression help.");
        Require(TextRenderer.MeasureText(number.Text, number.Font).Height > 0, "Cached result font is unusable.");
    }

    private static void FitInvalidation()
    {
        using var display = new ResultDisplay();
        using var host = Host(display);
        var value = new string('7', 2000);
        display.Set("Large", value);
        var number = Field<TextBox>(display, "number");
        var initialPages = display.PageCount;
        host.ClientSize = new(300, 120);
        host.PerformLayout();
        display.Set("Large", value);
        Require(display.PageCount >= initialPages && display.PageCount > 1, "Smaller viewport reused stale fit capacity.");
        var next = Descendants(display).OfType<CalcButton>().Single(button => button.AccessibleName == "Next result digits");
        Click(next);
        Require(display.PageIndex == 1, "Cached fitting prevented digit paging.");
        display.Set("Changed", "8");
        Require(display.PageCount == 1 && display.PageIndex == 0 && number.Text == "8", "Changed value reused stale digits or paging.");
        display.Set("Invalid", "Input error", true);
        display.ApplyTheme(Palette.Resolve(ThemePreference.Dark));
        Require(number.ForeColor == Palette.Resolve(ThemePreference.Dark).Error, "Error/theme change reused ordinary result colors.");
        var owned = number.Font;
        using (var external = new Font("Consolas", 16)) number.Font = external;
        Require(ReferenceEquals(number.Font, owned) && TextRenderer.MeasureText(number.Text, number.Font).Height > 0,
            "An external/scaled font bypassed invalidation or left a disposed font.");
    }

    private static void CachedNativeRectangle()
    {
        using var display = new ResultDisplay();
        using var host = Host(display);
        var value = new string('7', 100);
        display.Set("Native formatting", value);
        var number = Field<TextBox>(display, "number");
        var font = number.Font;
        number.Select(2, 5);
        var altered = new NativeRectangle { Left = 22, Right = number.ClientSize.Width - 22, Bottom = number.ClientSize.Height };
        _ = SendRectangle(number.Handle, 0x00b3 /* EM_SETRECT */, 0, ref altered);
        var injected = new NativeRectangle();
        _ = SendRectangle(number.Handle, 0x00b2 /* EM_GETRECT */, 0, ref injected);
        Require(injected.Left >= 18 && injected.Right <= number.ClientSize.Width - 18, "Native rectangle fault injection did not take effect.");
        display.Set("Native formatting", value);
        var actual = new NativeRectangle();
        _ = SendRectangle(number.Handle, 0x00b2 /* EM_GETRECT */, 0, ref actual);
        Require(Math.Abs(actual.Left) <= 4 && Math.Abs(actual.Top) <= 4 &&
            Math.Abs(actual.Right - number.ClientSize.Width) <= 4 && Math.Abs(actual.Bottom - number.ClientSize.Height) <= 4,
            $"Cached fit retained stale native formatting: rectangle=({actual.Left},{actual.Top})-({actual.Right},{actual.Bottom}), client={number.ClientRectangle}.");
        Require(number.SelectionStart == 2 && number.SelectionLength == 5 && ReferenceEquals(number.Font, font),
            "Restoring native formatting recomputed the font or reset user selection.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendRectangle(nint window, uint message, nint parameter, ref NativeRectangle rectangle);

    private static Form Host(Control control)
    {
        var form = new Form { ClientSize = new(620, 220), AutoScaleDimensions = new(96, 96), AutoScaleMode = AutoScaleMode.Dpi };
        form.Controls.Add(control); Handles(form); form.PerformLayout(); return form;
    }
    private static void WithForm(string mode, Action<MainForm> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterCommercialUi-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(directory);
            Require(store.Save(new AppSettings { Mode = mode }, []) is null, "Unable to seed isolated startup mode.");
            using var form = new MainForm(store);
            Handles(form); form.PrepareWindowLayout(Screen.FromControl(form).WorkingArea);
            action(form);
            Require(!form.Visible, "Commercial engineering checks must not show windows.");
        }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Require(string.Equals(Path.GetDirectoryName(resolved), temporaryRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("AsterCommercialUi-", StringComparison.Ordinal), "Unexpected profile path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
    private static void Handles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) Handles(child); control.PerformLayout(); }
    private static IEnumerable<Control> Descendants(Control control) { foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static Dictionary<string, ModeView> Views(MainForm form) => Field<Dictionary<string, ModeView>>(form, "views");
    private static ModeView Current(MainForm form) => (ModeView)typeof(MainForm).GetProperty("Current", Private)!.GetValue(form)!;
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private)!.GetValue(instance)!;
    private static object? Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Private)!.Invoke(instance, args);
    private static void Click(Control control) => typeof(Control).GetMethod("OnClick", Private)!.Invoke(control, [EventArgs.Empty]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
