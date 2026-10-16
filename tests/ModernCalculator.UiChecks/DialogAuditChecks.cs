using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Forms;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.UiChecks;

/// <summary>Hidden, native-handle geometry checks for every Preferences section and Help page.</summary>
public static class DialogAuditChecks
{
    private static readonly Lazy<(MethodInfo Method, object Flag)> ownVisibility = new(FindOwnVisibility);
    public static void Run(Action<string, Action> check)
    {
        foreach (var theme in new[] { ThemePreference.Light, ThemePreference.Dark })
        foreach (var compact in new[] { false, true })
        {
            var geometry = compact ? "1366x768 target192 effective workspace" : "normal design size";
            foreach (var section in new[] { "Appearance", "Numbers", "History", "Window" })
            {
                var selectedSection = section;
                check($"Preferences geometry {theme} {geometry} {selectedSection}", () => CheckPreferences(theme, compact, selectedSection));
            }
            foreach (var topic in new[] { "Keyboard", "Functions", "Numbers", "Window" })
            {
                var selectedTopic = topic;
                check($"Help geometry {theme} {geometry} {selectedTopic} all pages", () => CheckHelp(theme, compact, selectedTopic));
            }
        }
    }
    private static void CheckPreferences(ThemePreference theme, bool compact, string section)
    {
        using var form = new SettingsForm(new AppSettings { Theme = theme, Precision = 12 }, Palette.Resolve(theme));
        Prepare(form, compact, new(570, 500));
        var sections = Named<SegmentedControl>(form, "Preferences section");
        sections.SelectedIndex = Array.IndexOf(new[] { "Appearance", "Numbers", "History", "Window" }, section);
        LayoutTree(form); EnsureHiddenHandles(form); LayoutTree(form);
        CheckGeometry(form, "Preferences / " + section);
        if (section == "Numbers")
        {
            var precision = Named<FlatNumberInput>(form, "Scientific significant display digits");
            precision.Value = 15; LayoutTree(form); CheckGeometry(form, "Preferences / Numbers / maximum precision");
            precision.Editor.Text = "invalid"; LayoutTree(form); CheckGeometry(form, "Preferences / Numbers / invalid precision feedback");
        }
        if (section == "History")
        {
            var clear = Named<CalcButton>(form, "Clear saved history");
            Click(clear); LayoutTree(form); CheckGeometry(form, "Preferences / History / confirmation");
            Click(clear); LayoutTree(form); CheckGeometry(form, "Preferences / History / staged clear");
            Require(form.ClearHistoryRequested, "Confirming history clear did not stage its request.");
        }
    }
    private static void CheckHelp(ThemePreference theme, bool compact, string topic)
    {
        using var form = new HelpForm(new AppSettings { Theme = theme }, Palette.Resolve(theme));
        Prepare(form, compact, new(620, 520));
        var sections = Named<SegmentedControl>(form, "Help topic");
        sections.SelectedIndex = Array.IndexOf(new[] { "Keyboard", "Functions", "Numbers", "Window" }, topic);
        LayoutTree(form); EnsureHiddenHandles(form); LayoutTree(form);
        var pagesSeen = new HashSet<string>();
        for (var step = 0; step < 30; step++)
        {
            var label = Field<Label>(form, "pageLabel");
            Require(pagesSeen.Add(label.Text), "Help pagination repeated a page before reaching its end: " + label.Text);
            CheckGeometry(form, "Help / " + topic + " / page " + label.Text);
            var next = Named<CalcButton>(form, "Next help page");
            if (!next.Enabled)
            {
                var parts = label.Text.Split('/');
                Require(parts.Length == 2 && int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pageCount) && pagesSeen.Count == pageCount,
                    "Help page traversal did not reach every advertised page.");
                return;
            }
            Click(next); LayoutTree(form); EnsureHiddenHandles(form); LayoutTree(form);
        }
        throw new InvalidOperationException("Help pagination did not terminate.");
    }
    private static void Prepare(Form form, bool compact, Size normalLogicalSize)
    {
        // Create HWNDs while keeping the top-level window hidden. OnLoad is intentionally
        // not invoked: apply the effective work-area cap explicitly, without desktop state.
        _ = form.Handle;
        var dpi = form.DeviceDpi;
        form.Size = compact
            ? new((1366 - 64) * dpi / 192, (768 - 64) * dpi / 192)
            : new(normalLogicalSize.Width * dpi / 96, normalLogicalSize.Height * dpi / 96);
        LayoutTree(form); EnsureHiddenHandles(form); LayoutTree(form);
        Require(!form.Visible, "Dialog verification must not show native windows.");
    }
    private static void CheckGeometry(Form form, string context)
    {
        foreach (var control in Descendants(form).Prepend(form))
        {
            if (control is ScrollableControl scroll) Require(!scroll.AutoScroll, context + ": AutoScroll enabled on " + Identify(control));
            if (control.IsHandleCreated)
            {
                var style = GetWindowLongPtr(control.Handle, -16).ToInt64();
                Require((style & 0x00300000) == 0, context + ": native scrollbar style on " + Identify(control));
            }
            if (control == form || !ComponentVisible(control)) continue;
            if (control is Label label && label.Text.Length > 0)
            {
                CheckBounds(label, context); CheckLabel(label, context);
            }
            if (!Interactive(control)) continue;
            Require(!string.IsNullOrWhiteSpace(control.AccessibleName), context + ": unnamed interactive control " + Identify(control));
            CheckBounds(control, context);
            if (control is TextBox editor) CheckEditor(editor, context);
            else if (control is ButtonBase button) CheckButton(button, context);
        }
    }
    private static bool Interactive(Control control) => control is ButtonBase or TextBoxBase or SegmentedControl or FlatNumberInput;
    private static void CheckBounds(Control control, string context)
    {
        Require(control.Width > 0 && control.Height > 0, context + ": nonpositive bounds for " + Identify(control) + " " + control.Bounds);
        for (var child = control; child.Parent is { } parent; child = parent)
        {
            Require(parent.ClientRectangle.Contains(child.Bounds),
                $"{context}: {Identify(control)} exceeds ancestor {Identify(parent)}: child {child.Bounds}; viewport {parent.ClientRectangle}.");
            if (parent is Form) break;
        }
    }
    private static void CheckLabel(Label label, string context)
    {
        var available = new Size(Math.Max(1, label.ClientSize.Width - label.Padding.Horizontal), Math.Max(1, label.ClientSize.Height - label.Padding.Vertical));
        var measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(available.Width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
        Require(measured.Width <= available.Width && measured.Height <= available.Height,
            $"{context}: label text clipped: {Identify(label)}; needs {measured}, available {available}; font {label.Font.SizeInPoints}pt; text={label.Text}.");
    }
    private static void CheckEditor(TextBox editor, string context)
    {
        Require(editor.ScrollBars == ScrollBars.None, context + ": text editor scrollbars enabled: " + Identify(editor));
        if (editor.Text.Length == 0) return;
        var measured = TextRenderer.MeasureText(editor.Text, editor.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        Require(editor.ClientSize.Width >= measured.Width && editor.ClientSize.Height >= editor.Font.Height,
            $"{context}: editor text clipped: {Identify(editor)}; text needs {measured.Width}x{editor.Font.Height}, available {editor.ClientSize}.");
    }
    private static void CheckButton(ButtonBase button, string context)
    {
        if (button.Text.Length == 0) return;
        // Arithmetic symbols are drawn as vectors by CalcButton rather than through its Font.
        if (button is CalcButton && button.Text is "+" or "−" or "×" or "÷" or "=") return;
        var scale = button.DeviceDpi / 96f;
        var width = button.ClientSize.Width;
        var height = button.ClientSize.Height;
        if (button is CalcButton { Icon: not IconKind.None }) width -= (int)(44 * scale);
        if (button is FlatCheckBox) width -= (int)(32 * scale);
        var measured = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        Require(width >= measured.Width && height >= button.Font.Height,
            $"{context}: button text clipped: {Identify(button)}; text needs {measured.Width}x{button.Font.Height}, available {width}x{height}; font {button.Font.SizeInPoints}pt.");
    }
    private static (MethodInfo Method, object Flag) FindOwnVisibility()
    {
        var method = typeof(Control).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(candidate => candidate.Name == "GetState" && candidate.ReturnType == typeof(bool) && candidate.GetParameters().Length == 1 && candidate.GetParameters()[0].ParameterType.IsEnum)
            ?? throw new InvalidOperationException("Control.GetState(enum) is unavailable; hidden visibility cannot be verified.");
        var type = method.GetParameters()[0].ParameterType;
        var name = Enum.GetNames(type).SingleOrDefault(item => item.Equals("Visible", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Control state has no named Visible flag.");
        return (method, Enum.Parse(type, name));
    }
    private static bool ComponentVisible(Control control)
    {
        var access = ownVisibility.Value;
        for (Control? current = control; current is not null && current is not Form; current = current.Parent)
            if (!(bool)access.Method.Invoke(current, [access.Flag])!) return false;
        return true;
    }
    private static void EnsureHiddenHandles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) EnsureHiddenHandles(child); }
    private static void LayoutTree(Control control) { control.PerformLayout(); foreach (Control child in control.Controls) LayoutTree(child); control.PerformLayout(); }
    private static IEnumerable<Control> Descendants(Control control) { foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static T Named<T>(Control control, string name) where T : Control => Descendants(control).OfType<T>().Single(item => item.AccessibleName == name);
    private static T Field<T>(object instance, string name) => (T)(instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Missing dialog field " + name)).GetValue(instance)!;
    private static void Click(Control control) => typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [EventArgs.Empty]);
    private static string Identify(Control control) => control.GetType().Name + " " + (control.AccessibleName ?? control.Text);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
}
