using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.History;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Forms;
using ModernCalculator.WinForms.Themes;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

internal static class Program
{
    private static readonly List<CheckResult> results = [];
    private static readonly List<DisplayDiagnostic> displayDiagnostics = [];
    private static readonly List<RenderDiagnostic> renderDiagnostics = [];
    private static readonly List<WindowPolicyDiagnostic> windowPolicyDiagnostics = [];
    private static readonly List<ActualWindowDiagnostic> actualWindowDiagnostics = [];
    private static readonly List<MemoryBarDiagnostic> memoryBarDiagnostics = [];
    private static readonly List<EffectiveWorkspaceDiagnostic> effectiveWorkspaceDiagnostics = [];
    private static readonly Lazy<(MethodInfo Method, object VisibleFlag)> visibleState = new(CreateVisibleStateAccess);
    private static readonly MethodInfo click = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Control.OnClick is unavailable.");
    private static string output = "";
    private static string runDirectory = "";
    private static int screenshotCount;
    private static int actualDpi;
    private static string focus = "";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("artifacts", "UiChecks"));
        focus = args.Length > 1 ? args[1] : "";
        runDirectory = Path.Combine(output, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(runDirectory);
        Console.WriteLine("Hidden in-process WinForms component checks. No windows are shown and no OS input is sent.");
        Console.WriteLine("100/125/150/175/200% are relative Control.Scale layout simulations; actual Windows DPI is not changed.");
        Console.WriteLine("Output: " + runDirectory);

        foreach (var theme in new[] { ThemePreference.Light, ThemePreference.Dark })
        {
            foreach (var scale in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f })
            {
                var label = $"{theme}-{scale * 100:0}pct-simulated";
                Run("MainForm matrix setup " + label, () => CheckMainForm(theme, scale, label));
                Run("SettingsForm " + label, () => CheckSettings(theme, scale, label));
            }
            var minimumLabel = theme + "-minimum-window-size";
            Run("MainForm minimum-size matrix setup " + theme, () => CheckMainForm(theme, 1, minimumLabel, minimumSize: true));
        }
        Run("Working-area and target-DPI sizing policy", CheckWindowLayoutPolicy);
        Run("Caption command handlers without native windows", CheckCaptionHandlers);
        foreach (var area in new[] { new Size(1366, 768), new Size(1920, 1080) })
        foreach (var targetDpi in new[] { 96, 120, 144, 168, 192 })
        foreach (var mode in new[] { "Standard", "Scientific", "Programmer", "Converter", "Dates" })
            Run($"Effective workspace geometry {mode} {area.Width}x{area.Height} target {targetDpi} DPI",
                () => CheckEffectiveWorkspaceGeometry(mode, area, targetDpi));
        foreach (var theme in new[] { ThemePreference.Light, ThemePreference.Dark })
            Run("MainForm stored-result pager " + theme, () => CheckStoredResultPages(theme));
        Run("Standard exact 100-digit memory and show-memory request", CheckStandardMemory);
        Run("ResultDisplay font lifetime and rendering", CheckResultFonts);
        Run("ResultDisplay full bounded numeric paging", CheckResultPaging);
        Run("ResultDisplay short-result resize alignment", CheckShortResultResizeAlignment);
        Run("Scientific angle and function segments", CheckScientificSegments);
        Run("Programmer synchronized base representations", CheckProgrammerBases);
        Run("Converter choices, swap, conversion and history", CheckConverter);
        DateAuditChecks.Run(Run);
        ConverterViewAuditChecks.Run(Run);
        ScientificProgrammerAuditChecks.Run(Run);
        SharedAuditChecks.Run(Run);
        DialogAuditChecks.Run(Run);
        IndependentSharedChecks.Run(Run);
        UsabilityAuditChecks.Run(Run);
        WindowStateAuditChecks.Run(Run);
        DeepUsabilityAuditChecks.Run(Run);
        DeepWorkspaceAuditChecks.Run(Run);
        DeepSharedAuditChecks.Run(Run);
        CommercialUiChecks.Run(Run);
        CommercialEngineeringChecks.Run(Run);

        if (results.Count == 0)
        {
            const string detail = "No checks matched the requested filter. Use a top-level check name or run without a filter.";
            results.Add(new CheckResult("Check selection", "FAIL", detail));
            Console.WriteLine("FAIL Check selection: " + detail);
        }

        var report = new
        {
            generatedUtc = DateTime.UtcNow,
            focus,
            actualDeviceDpi = actualDpi,
            scaleVerification = "Relative Control.Scale(SizeF) at 100/125/150/175/200%; pure target-DPI sizing-policy calculations; and effective-workspace geometry simulations converting target sizes into actual-DPI equivalents. None changes Windows DPI or verifies monitor transitions.",
            hiddenComponentRendering = true,
            snapshotTarget = "MainForm PNGs capture the client-layout root; other component hosts use their root control. No desktop/window-manager capture.",
            desktopAutomation = false,
            screenshots = screenshotCount,
            passed = results.Count(item => item.Status == "PASS"),
            failed = results.Count(item => item.Status == "FAIL"),
            skipped = results.Count(item => item.Status == "SKIP"),
            resultDisplayDiagnostics = displayDiagnostics,
            renderDiagnostics,
            windowPolicyDiagnostics,
            actualWindowDiagnostics,
            memoryBarDiagnostics,
            effectiveWorkspaceDiagnostics,
            limitations = new[]
            {
                "Forms are never shown. Window-manager composition, desktop focus, physical keyboard/mouse input, native monitor DPI transitions and screen-reader output are not exercised.",
                "Caption action checks invoke handlers on fresh forms before native handle creation; they verify managed minimize/maximize/restore/close state, not OS window-manager transitions.",
                "DrawToBitmap is a WinForms component snapshot. MainForm client-layout snapshots omit synthetic OS captions that hidden Form rendering can produce from native snap/resize style bits; live nonclient composition is not verified by these PNGs.",
                "Layout assertions check positive interactive bounds, immediate-parent and all-ancestor viewport containment, no scrollbars and non-overlap. Paged content is permitted only when its current-page controls remain fully accessible.",
                "Clipboard, external URLs, destructive controls and preferences dialogs are not activated. State files are isolated under this output directory."
            },
            checks = results
        };
        File.WriteAllText(Path.Combine(runDirectory, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllLines(Path.Combine(runDirectory, "report.txt"), results.Select(item => $"{item.Status}: {item.Name}{Environment.NewLine}{item.Detail}"));
        Console.WriteLine($"Checks: {report.passed} passed, {report.failed} failed, {report.skipped} skipped; PNGs: {screenshotCount}; actual DeviceDpi: {actualDpi}.");
        return report.failed == 0 ? 0 : 1;
    }

    private static void CheckMainForm(ThemePreference theme, float scale, string label, bool minimumSize = false)
    {
        var modes = new[] { "Standard", "Scientific", "Programmer", "Converter", "Dates" };
        var expected = new[] { typeof(StandardView), typeof(ScientificView), typeof(ProgrammerView), typeof(ConverterView), typeof(DateView) };
        for (var index = 0; index < modes.Length; index++)
        {
            var modeIndex = index;
            Run($"MainForm mode {label} {modes[modeIndex]}", () =>
            {
                // Every mode gets a fresh form and its own launch policy. Scaling one detached view
                // after another would inherit Standard's dimensions and accumulate unrelated scales.
                var caseLabel = label + "-" + modes[modeIndex];
                var store = new StateStore(Path.Combine(runDirectory, "state", caseLabel));
                Require(store.Save(new AppSettings { Theme = theme, PersistHistory = false, Mode = modes[modeIndex] }, []) is null,
                    "Could not seed isolated preferences.");
                using var form = new MainForm(store);
                Require(form.WindowState == FormWindowState.Normal, "MainForm must start in the normal state before handle creation.");
                Prepare(form, 1);
                var navigation = Named<SegmentedControl>(form, "Calculator mode");
                Require(navigation.SelectedIndex == modeIndex, "Startup mode selector disagrees with saved preferences.");
                Require(Descendants(form).OfType<ModeView>().Single().GetType() == expected[modeIndex], "Saved mode did not attach its view.");
                var workingArea = Screen.FromControl(form).WorkingArea;
                form.PrepareWindowLayout(workingArea);
                if (minimumSize) form.Size = form.MinimumSize;
                LayoutTree(form);
                if (scale != 1) form.Scale(new SizeF(scale, scale));
                EnsureHiddenHandles(form);
                LayoutTree(form);
                actualDpi = form.DeviceDpi;
                CheckFrameChrome(form);
                actualWindowDiagnostics.Add(new(caseLabel, form.DeviceDpi, scale, minimumSize, form.Size, form.ClientSize,
                    form.MinimumSize, workingArea, form.FormBorderStyle.ToString(), form.WindowState.ToString()));
                if (scale == 1)
                {
                    var metrics = WindowLayoutPolicy.Calculate(modes[modeIndex], workingArea.Size, form.DeviceDpi);
                    Require(form.MinimumSize == metrics.MinimumSize,
                        $"Actual-DPI minimum disagrees with sizing policy: actual={form.MinimumSize}, expected={metrics.MinimumSize}, DPI={form.DeviceDpi}.");
                    Require(form.Size == (minimumSize ? metrics.MinimumSize : metrics.InitialSize),
                        $"Actual-DPI {(minimumSize ? "minimum" : "default")} size disagrees with policy: actual={form.Size}, expected={(minimumSize ? metrics.MinimumSize : metrics.InitialSize)}, DPI={form.DeviceDpi}.");
                    Require(form.Width <= workingArea.Width && form.Height <= workingArea.Height,
                        $"Actual-DPI {(minimumSize ? "minimum" : "default")} window does not fit the working area: size={form.Size}, DPI={form.DeviceDpi}, area={workingArea}.");
                    Require(form.MinimumSize.Width <= form.Width && form.MinimumSize.Height <= form.Height,
                        "Actual-DPI minimum size exceeds the initial window size.");
                }
                CaptureDisplayDiagnostics(form, "main-" + caseLabel + "-after-hidden-prepare");
                var view = Descendants(form).OfType<ModeView>().Single();
                Require(view.GetType() == expected[modeIndex], $"Mode {modes[modeIndex]} did not attach its view.");
                Snapshot(form, $"main-{label}-{modes[modeIndex]}.png", view);
                CheckControls(form);
                CheckBounds(view);
                CheckBounds(navigation);
                CheckViewportContainment(view);
                CheckViewportContainment(navigation);
                CheckNoScrollbars(view);
                CheckMemoryBar(view, caseLabel);
                // A removed/re-attached HWND remains native-hidden in a never-shown form, even
                // when its managed own-visible state is true. Verify event routing after the
                // fresh startup-mode snapshot instead of claiming that transition was rendered.
                var alternate = (modeIndex + 1) % modes.Length;
                navigation.SelectedIndex = alternate;
                Require(Descendants(form).OfType<ModeView>().Single().GetType() == expected[alternate], "Mode selector did not attach its alternate view.");
                navigation.SelectedIndex = modeIndex;
                Require(Descendants(form).OfType<ModeView>().Single().GetType() == expected[modeIndex], "Mode selector did not restore this case's view.");
            });
        }
    }

    private static void CheckSettings(ThemePreference theme, float scale, string label)
    {
        var initial = new AppSettings { Theme = theme, Precision = 12, GroupDigits = true, PersistHistory = false };
        using var form = new SettingsForm(initial, Palette.Resolve(theme));
        Prepare(form, scale);
        Snapshot(form, "settings-" + label + ".png");
        CheckControls(form);
        CheckBounds(form);
        CheckViewportContainment(form);
        CheckNoScrollbars(form);
        var selector = Named<SegmentedControl>(form, "Theme preference");
        Require(selector.SelectedIndex == (int)theme, "Settings theme selection disagrees with preferences.");
        Named<SegmentedControl>(form, "Preferences section").SelectedIndex = 1;
        var precision = Named<FlatNumberInput>(form, "Scientific significant display digits");
        Require(precision.Minimum == 3 && precision.Maximum == 15, "Scientific precision range changed.");
        Require(form.AcceptButton is CalcButton && form.CancelButton is CalcButton, "Dialog accept/cancel buttons are missing.");
        // Directly invoke the save handler on this hidden instance; no dialog is opened.
        selector.SelectedIndex = (int)ThemePreference.System;
        precision.Value = 15;
        Click(Named<CalcButton>(form, "Save preferences"));
        Require(form.Result.Theme == ThemePreference.System && form.Result.Precision == 15, "Saving preferences did not capture selections.");
        Require(!form.Result.PersistHistory, "History persistence unexpectedly became enabled.");
    }

    private static void CheckCaptionHandlers()
    {
        var store = new StateStore(Path.Combine(runDirectory, "state", "caption-commands"));
        using (var form = new MainForm(store))
        {
            Require(!form.IsHandleCreated && form.WindowState == FormWindowState.Normal, "Caption fixture unexpectedly created a native window or started non-normal.");
            Click(Named<CalcButton>(form, "Minimize window"));
            Require(form.WindowState == FormWindowState.Minimized && !form.IsHandleCreated, "Minimize handler did not update hidden component state without creating a native window.");
            form.WindowState = FormWindowState.Normal;
            Click(Named<CalcButton>(form, "Maximize window"));
            Require(form.WindowState == FormWindowState.Maximized && !form.IsHandleCreated, "Maximize handler did not update hidden component state without creating a native window.");
            Click(Named<CalcButton>(form, "Maximize window"));
            Require(form.WindowState == FormWindowState.Normal && !form.IsHandleCreated, "Maximize handler did not restore normal component state.");
        }
        using var closing = new MainForm(store);
        Require(!closing.IsHandleCreated, "Close fixture unexpectedly created a native window.");
        Click(Named<CalcButton>(closing, "Close window"));
        Require(closing.IsDisposed, "Close handler did not dispose the isolated hidden form.");
    }

    private static void CheckEffectiveWorkspaceGeometry(string mode, Size targetWorkingArea, int targetDpi)
    {
        var label = $"effective-workspace-{targetWorkingArea.Width}x{targetWorkingArea.Height}-{targetDpi}dpi-{mode}";
        var store = new StateStore(Path.Combine(runDirectory, "state", label));
        Require(store.Save(new AppSettings { Mode = mode, Theme = ThemePreference.Light, PersistHistory = false }, []) is null,
            "Could not seed isolated effective-workspace preferences.");
        using var form = new MainForm(store);
        Prepare(form, 1);
        form.PrepareWindowLayout(Screen.FromControl(form).WorkingArea);
        var view = Descendants(form).OfType<ModeView>().Single();
        var number = string.Concat(Enumerable.Repeat("1234567890", 10));
        if (view is StandardView standard) standard.Reuse(number);
        else if (view is ScientificView)
        {
            Named<TextBox>(view, "Scientific expression").Text = "sin(30)";
            view.Command(Keys.Enter);
        }
        else if (view is ProgrammerView programmer) programmer.ReuseDecimalResult("255");
        else if (view is ConverterView) Named<TextBox>(view, "Value to convert").Text = "1";
        var target = WindowLayoutPolicy.Calculate(mode, targetWorkingArea, targetDpi);
        Size Equivalent(Size physical) => new(Math.Max(1, (int)Math.Round((double)physical.Width * form.DeviceDpi / targetDpi, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round((double)physical.Height * form.DeviceDpi / targetDpi, MidpointRounding.AwayFromZero)));
        var equivalentInitial = Equivalent(target.InitialSize);
        var equivalentMinimum = Equivalent(target.MinimumSize);
        form.MinimumSize = Size.Empty;
        form.MinimumSize = equivalentMinimum;
        foreach (var minimumCase in new[] { false, true })
        {
            var geometry = minimumCase ? "minimum" : "initial";
            var expectedSize = minimumCase ? equivalentMinimum : equivalentInitial;
            form.Size = expectedSize;
            LayoutTree(form);
            EnsureHiddenHandles(form);
            view.RefreshFormatting();
            LayoutTree(form);
            effectiveWorkspaceDiagnostics.Add(new(label, geometry, form.DeviceDpi, targetDpi, targetWorkingArea,
                target.InitialSize, target.MinimumSize, equivalentInitial, equivalentMinimum, form.Size, form.ClientSize));
            CaptureDisplayDiagnostics(form, label + "-" + geometry);
            Require(form.Size == expectedSize, $"Effective-workspace {geometry} dimensions were not applied: expected={expectedSize}, actual={form.Size}.");
            CheckFrameChrome(form);
            CheckControls(form);
            CheckBounds(view);
            CheckBounds(Named<SegmentedControl>(form, "Calculator mode"));
            CheckViewportContainment(view);
            CheckViewportContainment(Named<SegmentedControl>(form, "Calculator mode"));
            CheckNoScrollbars(view);
            CheckMemoryBar(view, label + "-" + geometry);
            if (view is not ProgrammerView || Descendants(view).OfType<ResultDisplay>().Any(ComponentVisible)) CheckCurrentResultPageText(view, label + "-" + geometry);
            if (view is StandardView) Require(view.CopyText == number, "Resizing narrowed or hid the Standard 100-digit stored value.");
            else if (view is ScientificView) Require(Math.Abs(double.Parse(view.CopyText, CultureInfo.CurrentCulture) - .5) < 1e-14, "Resizing changed or cleared Scientific sin(30).");
            else if (view is ProgrammerView) Require(view.CopyText == "255" && Descendants(view).OfType<TextBox>().Any(text => !text.ReadOnly && text.Text == "255"), "Resizing lost editable exact programmer input.");
            else if (view is ConverterView) Require(Math.Abs(double.Parse(view.CopyText, CultureInfo.CurrentCulture) - .001) < 1e-14, "Resizing changed conversion.");
            else Require(view.CopyText == "30", "Resizing changed the populated date difference.");
            if (minimumCase && targetWorkingArea == new Size(1366, 768) && targetDpi == 192)
                Snapshot(form, label + "-minimum.png", view);
        }
    }

    private static void CheckCurrentResultPageText(Control scope, string source)
    {
        var displays = Descendants(scope).Prepend(scope).OfType<ResultDisplay>().Where(ComponentVisible).ToArray();
        Require(displays.Length > 0, "No visible result component exists: " + source);
        foreach (var display in displays)
        {
            var text = Descendants(display).OfType<TextBox>().Single();
            Require(text.TextLength > 0 && display.FullValue.Length > 0, "Result text is blank after resizing: " + source);
            var first = text.GetPositionFromCharIndex(0);
            var last = text.GetPositionFromCharIndex(text.TextLength - 1);
            Require(first.X >= 0 && first.Y >= 0,
                $"First result glyph is scrolled out of its viewport: {source}, position={first}, viewport={text.ClientRectangle}.");
            Require(last.X >= 0 && last.X < text.ClientSize.Width && last.Y >= 0 && last.Y + text.Font.Height <= text.ClientSize.Height + 1,
                $"Last result glyph is clipped: {source}, position={last}, Font.Height={text.Font.Height}, viewport={text.ClientRectangle}.");
            Require(text.Font.Height > 0, "Result font was disposed or became invalid after resizing: " + source);
            _ = TextRenderer.MeasureText(text.Text, text.Font);
        }
    }

    private static void CheckStoredResultPages(ThemePreference theme)
    {
        var items = Enumerable.Range(0, HistoryJournal.Capacity).Select(index => new HistoryItem(Guid.NewGuid(), DateTimeOffset.UtcNow,
            "Standard", "Stored fixture " + index, index.ToString(CultureInfo.InvariantCulture))).ToArray();
        var store = new StateStore(Path.Combine(runDirectory, "state", "pager-" + theme));
        Require(store.Save(new AppSettings { Theme = theme, Mode = "Standard", PersistHistory = true }, items) is null, "Could not seed isolated pager history.");
        using var form = new MainForm(store);
        Prepare(form, 1);
        form.PrepareWindowLayout(Screen.FromControl(form).WorkingArea);
        Click(Named<CalcButton>(form, "Show or hide history and memory"));
        form.PrepareWindowLayout(Screen.FromControl(form).WorkingArea);
        form.Size = form.MinimumSize;
        LayoutTree(form);
        var tabs = Named<SegmentedControl>(form, "Secondary panel");
        tabs.SelectedIndex = 0;
        var panel = tabs.Parent ?? throw new InvalidOperationException("Stored-result panel is missing.");
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var next = Named<CalcButton>(panel, "Next stored results");
        var previous = Named<CalcButton>(panel, "Previous stored results");
        _ = Named<Label>(panel, "Stored results page");
        Require(!previous.Enabled, "Previous stored-results page is enabled on the first page.");
        var pages = 0;
        while (true)
        {
            EnsureHiddenHandles(panel);
            LayoutTree(form);
            CheckControls(panel);
            CheckBounds(panel);
            CheckViewportContainment(panel);
            CheckNoScrollbars(panel);
            var reuse = Descendants(panel).OfType<CalcButton>().Where(button => ComponentVisible(button) && button.AccessibleName?.StartsWith("Reuse result ", StringComparison.Ordinal) == true).ToArray();
            Require(reuse.Length > 0, "A nonempty stored-results page has no accessible result actions.");
            foreach (var button in reuse) visited.Add(button.AccessibleName!);
            pages++;
            Require(pages <= HistoryJournal.Capacity, "Stored-result paging did not terminate.");
            next = Named<CalcButton>(panel, "Next stored results");
            if (!next.Enabled) break;
            Click(next);
        }
        var expected = items.Select(item => "Reuse result " + item.LocalizedResult(CultureInfo.CurrentCulture)).ToHashSet(StringComparer.Ordinal);
        Require(visited.SetEquals(expected), "Some stored results are unreachable through the visible pager.");
        previous = Named<CalcButton>(panel, "Previous stored results");
        Require(pages > 1 && previous.Enabled, "Stored-result fixture did not exercise more than one page.");
        Click(previous);
        next = Named<CalcButton>(panel, "Next stored results");
        Require(next.Enabled, "Next stored-results page did not become available after returning one page.");
        Snapshot(form, "main-stored-results-pager-" + theme + ".png", panel);
    }

    private static void CheckStandardMemory()
    {
        var context = new WorkspaceContext { Settings = new AppSettings { Theme = ThemePreference.Light } };
        var memoryRequests = 0;
        context.ShowMemoryRequested += () => memoryRequests++;
        using var host = Host(new StandardView(context), new Size(720, 650));
        var view = Descendants(host).OfType<StandardView>().Single();
        var number = string.Concat(Enumerable.Repeat("1234567890", 10));
        Require(number.Length == 100, "Regression fixture must contain exactly 100 digits.");
        view.Reuse(number);
        Require(view.CopyText == number, "Standard reuse changed the 100-digit integer.");
        Click(Named<CalcButton>(view, "Store in new memory slot"));
        Require(context.Memory.Items.Count == 1, "MS did not store a value.");
        Require(context.Memory.RecallExact().ToString(CultureInfo.InvariantCulture) == number, "MS narrowed or rounded the exact value.");
        Click(Named<CalcButton>(view, "Open memory list"));
        Require(memoryRequests == 1, "Memory list request was not emitted.");
        view.Reuse("7");
        Click(Named<CalcButton>(view, "Recall newest memory"));
        Require(view.CopyText == number, "MR narrowed or rounded the exact value.");
        context.Memory.Store(BigDecimal.Parse(number));
        Require(context.Memory.Items[0].ExactValue.ToString(CultureInfo.InvariantCulture) == number, "Direct exact memory storage narrowed the value.");
        var status = "";
        context.StatusChanged += message => status = message;
        var nines = new string('9', 100);
        view.Reuse(nines);
        view.Character('9');
        Require(view.CopyText == nines && status.Contains("100 significant", StringComparison.Ordinal), "The 101st typed digit did not preserve the value and report the limit.");
        view.Character('+'); view.Character('1'); view.Command(Keys.Enter);
        Require(view.CopyText == "1" + new string('0', 100), "A 100-digit carry was rounded or Enter evaluated twice.");
        Require(!status.Contains("100 significant", StringComparison.Ordinal), "A recovered calculation retained the earlier input error status.");
        view.Reuse(number);
        CheckBounds(view);
        CheckViewportContainment(view);
        CheckNoScrollbars(view);
        Snapshot(host, "standard-100-digit-memory.png");
    }

    private static void CheckResultFonts()
    {
        using var display = new ResultDisplay();
        using var host = Host(display, new Size(690, 190));
        var themes = new[] { ThemePreference.Light, ThemePreference.Dark };
        var values = new[] { "123.45", "123.46", new string('9', 100), "0", "Correct the input to continue" };
        for (var iteration = 0; iteration < 120; iteration++)
        {
            host.ClientSize = new Size(iteration % 2 == 0 ? 690 : 500, iteration % 3 == 0 ? 210 : 190);
            LayoutTree(host);
            Ui.Theme(display, Palette.Resolve(themes[iteration % themes.Length]));
            var value = values[iteration % values.Length];
            display.Set("Font lifetime regression", value, iteration % values.Length == values.Length - 1);
            display.Set("Repeated same-font update", value, iteration % values.Length == values.Length - 1);
            foreach (var text in Descendants(display).OfType<TextBox>())
            {
                Require(text.Font.Height > 0, "Textbox font is invalid.");
                _ = TextRenderer.MeasureText(text.Text, text.Font);
            }
            CheckResultDisplayLabels(display);
            CheckNoScrollbars(display);
            // Exercise every iteration's paint path without saving 120 redundant files.
            using var bitmap = new Bitmap(host.Width, host.Height);
            host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        }
        Snapshot(host, "result-font-lifetime.png");
    }

    private static void CheckResultPaging()
    {
        using var display = new ResultDisplay();
        using var host = Host(display, new Size(400, 120));
        var value = BigDecimal.Parse(new string('9', 1000) + "." + string.Concat(Enumerable.Repeat("1234567890", 100))).ToString();
        display.Set("Full bounded numeric paging regression", value);
        LayoutTree(host);
        Require(display.FullValue == value && display.PageIndex == 0 && display.PageCount > 1, "A long bounded value was not retained and paged from its first page.");
        var pageCount = display.PageCount;
        var parts = new List<string>();
        var number = Descendants(display).OfType<TextBox>().Single();
        for (var index = 0; index < pageCount; index++)
        {
            LayoutTree(host);
            Require(display.PageIndex == index && display.PageCount == pageCount, "Result paging skipped a page or changed its capacity unexpectedly.");
            Require(display.FullValue == value, "Result paging changed the full stored value.");
            CheckBounds(display);
            CheckViewportContainment(display);
            CheckNoScrollbars(display);
            parts.Add(string.Concat(number.Lines));
            Require(number.TextLength > 0, "A numeric result page is empty.");
            var last = number.GetPositionFromCharIndex(number.TextLength - 1);
            Require(last.Y >= 0 && last.Y + number.Font.Height <= number.ClientSize.Height + 1,
                $"Result page {index + 1} has vertically clipped text: last glyph={last}, Font.Height={number.Font.Height}, viewport={number.ClientRectangle}.");
            var previous = Named<CalcButton>(display, "Previous result digits");
            var next = Named<CalcButton>(display, "Next result digits");
            Require(previous.Enabled == (index > 0) && next.Enabled == (index + 1 < pageCount), "Result pager enabled states disagree with the current page.");
            if (next.Enabled) Click(next);
        }
        Require(string.Concat(parts) == value, "Some characters are missing, duplicated or reordered across result pages.");
        Click(Named<CalcButton>(display, "Previous result digits"));
        Require(display.PageIndex == pageCount - 2, "Previous result page did not return one page.");
        Snapshot(host, "result-full-bounded-paging.png");
        display.Set("Short result reset", "42");
        Require(display.FullValue == "42" && display.PageIndex == 0 && display.PageCount == 1, "A new short result retained the previous paging state.");
        Require(!ComponentVisible(Named<CalcButton>(display, "Previous result digits")) && !ComponentVisible(Named<CalcButton>(display, "Next result digits")),
            "Result pager buttons remain displayed for a one-page value.");
        CheckBounds(display);
        CheckNoScrollbars(display);
    }

    private static void CheckShortResultResizeAlignment()
    {
        using var display = new ResultDisplay();
        using var host = Host(display, new Size(650, 160));
        var number = Descendants(display).OfType<TextBox>().Single();
        foreach (var value in new[] { "0", "0.5" })
        {
            display.Set("Short result alignment regression", value);
            foreach (var size in new[] { new Size(650, 160), new Size(480, 80), new Size(650, 160) })
            {
                host.ClientSize = size;
                LayoutTree(host);
                // Deliberately do not call Set/RefreshFormatting: resizing must refit the persistent value.
                Require(display.FullValue == value && number.Text == value && display.PageCount == 1,
                    $"Resizing changed the short stored value: value={value}, host={size}.");
                CheckBounds(display);
                CheckViewportContainment(display);
                CheckNoScrollbars(display);
                CheckCurrentResultPageText(display, $"short value {value}, host={size}");
                var formatting = new NativeRect();
                _ = SendMessageRectangle(number.Handle, 0x00B2, 0, ref formatting); // EM_GETRECT
                Require(Math.Abs(formatting.Left) <= 4 && Math.Abs(formatting.Top) <= 4 &&
                    Math.Abs(formatting.Right - number.ClientSize.Width) <= 4 && Math.Abs(formatting.Bottom - number.ClientSize.Height) <= 4,
                    $"Native result formatting rectangle is stale: value={value}, host={size}, rectangle={formatting}, client={number.ClientRectangle}.");
                var last = number.GetPositionFromCharIndex(number.TextLength - 1);
                using var graphics = number.CreateGraphics();
                var advance = TextRenderer.MeasureText(graphics, number.Text[^1].ToString(), number.Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
                Require(Math.Abs(last.X + advance - formatting.Right) <= 4,
                    $"Short result is not right-aligned after resize: value={value}, host={size}, last={last}, glyphAdvance={advance}, formattingRight={formatting.Right}.");
                Require(number.ClientSize.Height >= number.Font.Height,
                    $"Short result font exceeds its actual textbox height: value={value}, host={size}, fontHeight={number.Font.Height}, client={number.ClientSize}.");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public override readonly string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessageRectangle(nint window, uint message, nint wParam, ref NativeRect rectangle);

    private static void CheckScientificSegments()
    {
        using var host = Host(new ScientificView(new WorkspaceContext()), new Size(720, 650));
        var view = Descendants(host).OfType<ScientificView>().Single();
        var angle = Named<SegmentedControl>(view, "Angle unit");
        var functions = Named<SegmentedControl>(view, "Scientific function group");
        var expression = Named<TextBox>(view, "Scientific expression");
        view.ReuseStoredResult(BigDecimal.Parse("1e-300").ToString());
        view.Command(Keys.Enter);
        Require(double.Parse(view.CopyText, CultureInfo.CurrentCulture) == 1e-300, "A long stored fixed-point value was rejected by the external clipboard length limit.");
        view.ReuseExact(BigDecimal.Parse("1e1000"));
        Require(double.Parse(view.CopyText, CultureInfo.CurrentCulture) == 1e-300, "An out-of-range recall changed the existing Scientific value.");
        expression.Text = "2+2";
        view.Command(Keys.Enter);
        Require(double.Parse(view.CopyText, CultureInfo.CurrentCulture) == 4, "Scientific expression did not evaluate.");
        expression.Text = "2+3";
        Require(view.CopyText.Length == 0, "Editing an expression left a stale result available for copying.");
        expression.Text = "sin(30)";
        view.Command(Keys.Enter);
        Require(Math.Abs(double.Parse(view.CopyText, CultureInfo.CurrentCulture) - .5) < 1e-14, "DEG calculation disagrees.");
        angle.SelectedIndex = 1;
        Require(view.CopyText.Length == 0, "Changing angle unit left a result from the previous unit.");
        view.Command(Keys.Enter);
        Require(Math.Abs(double.Parse(view.CopyText, CultureInfo.CurrentCulture) - Math.Sin(30)) < 1e-14, "RAD calculation disagrees.");
        Require(Descendants(view).OfType<Label>().Any(item => item.Text.Contains("radians", StringComparison.Ordinal)), "RAD did not update the result caption.");
        angle.SelectedIndex = 0;
        Require(Descendants(view).OfType<Label>().Any(item => item.Text.Contains("degrees", StringComparison.Ordinal)), "DEG did not update the result caption.");
        foreach (var pair in new[] { (0, "logbase"), (1, "sin"), (2, "sinh") })
        {
            functions.SelectedIndex = pair.Item1;
            LayoutTree(host);
            Require(Descendants(view).OfType<CalcButton>().Any(item => item.Text == pair.Item2 || pair.Item2 == "logbase" && item.AccessibleName == "Logarithm with chosen base"), "Scientific function bank did not update.");
            CheckControls(view);
            CheckBounds(view);
        }
        Snapshot(host, "scientific-segments.png");
    }

    private static void CheckProgrammerBases()
    {
        using var host = Host(new ProgrammerView(new WorkspaceContext()), new Size(720, 650));
        var view = Descendants(host).OfType<ProgrammerView>().Single();
        view.Reuse("255");
        var expected = new[] { ("Decimal", "255"), ("Hexadecimal", "FF"), ("Octal", "377"), ("Binary", new string('0', 56) + "11111111") };
        foreach (var (name, value) in expected)
        {
            Click(Named<CalcButton>(view, "Use " + name + " input"));
            Require(view.CopyText == (name == "Binary" ? "11111111" : value), $"Selecting {name} changed the integer.");
            foreach (var (rowName, rowValue) in expected)
            {
                var text = Named<TextBox>(view, rowName + " representation, calculated").Text.Replace(" ", "", StringComparison.Ordinal);
                Require(text == rowValue, $"{rowName} representation disagrees after selecting {name}.");
            }
        }
        CheckControls(view);
        CheckBounds(view);
        Snapshot(host, "programmer-synchronized-bases.png");
    }

    private static void CheckConverter()
    {
        var context = new WorkspaceContext();
        using var host = Host(new ConverterView(context), new Size(720, 650));
        var view = Descendants(host).OfType<ConverterView>().Single();
        var input = Named<TextBox>(view, "Value to convert");
        var from = Named<Control>(view, "Source unit");
        var to = Named<Control>(view, "Destination unit");
        Require(UnitItems(from).Count > 1 && UnitItems(to).Count == UnitItems(from).Count, "Length units were not populated.");
        input.Text = "1000";
        SelectUnit(from, 0);
        SelectUnit(to, 1);
        Require(double.Parse(view.CopyText, CultureInfo.CurrentCulture) == 1, "1000 metres should convert to one kilometre.");
        Click(Named<CalcButton>(view, "Swap units"));
        Require(SelectedUnit(from) == 1 && SelectedUnit(to) == 0, "Swap did not exchange unit choices.");
        Click(Named<CalcButton>(view, "Save this conversion to history"));
        Require(context.History.Items.Count == 1, "Explicit save did not add a conversion to history.");
        foreach (var category in ModernCalculator.Core.Conversions.UnitConverter.Categories)
        {
            Click(Named<CalcButton>(view, category.Name));
            Require(UnitItems(from).Count == category.Units.Count && UnitItems(to).Count == category.Units.Count, "Category did not populate its units: " + category.Name);
            CheckUnitPages(from);
            CheckUnitPages(to);
            CheckNoScrollbars(view);
            CheckViewportContainment(view);
        }
        var count = context.History.Items.Count;
        input.Text = "1e-999";
        Require(!Named<CalcButton>(view, "Save this conversion to history").Enabled, "Invalid conversion still exposes an enabled Save action.");
        view.Command(Keys.Enter);
        Require(view.CopyText.Length == 0 && context.History.Items.Count == count, "Underflow was silently converted or saved as zero.");
        input.Text = "1";
        CheckControls(view);
        CheckBounds(view);
        CheckNoScrollbars(view);
        CheckViewportContainment(view);
        Snapshot(host, "converter-visible-unit-choices.png");
    }

    private static IReadOnlyList<ModernCalculator.Core.Conversions.UnitDefinition> UnitItems(Control choices) =>
        ReadProperty<IReadOnlyList<ModernCalculator.Core.Conversions.UnitDefinition>>(choices, "Items");

    private static int SelectedUnit(Control choices) => ReadProperty<int>(choices, "SelectedIndex");

    private static void SelectUnit(Control choices, int index)
    {
        var property = choices.GetType().GetProperty("SelectedIndex", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Unit choices do not expose SelectedIndex.");
        property.SetValue(choices, index);
    }

    private static T ReadProperty<T>(object instance, string name) =>
        instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance) is T value
            ? value : throw new InvalidOperationException(instance.GetType().Name + " does not expose " + name + ".");

    private static void CheckUnitPages(Control choices)
    {
        SelectUnit(choices, 0);
        var selected = SelectedUnit(choices);
        var prefix = choices.AccessibleName + " choices";
        Named<TextBox>(choices, prefix + " search").Clear();
        var expected = UnitItems(choices).Select(unit => prefix + ": " + unit).ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var next = Named<CalcButton>(choices, prefix + " more matches");
        var previous = Named<CalcButton>(choices, prefix + " previous matches");
        Require(!previous.Enabled, "Previous unit page should be disabled on the first page.");
        var pages = 0;
        while (true)
        {
            LayoutTree(choices);
            CheckBounds(choices);
            CheckViewportContainment(choices);
            var current = Descendants(choices).OfType<CalcButton>()
                .Where(button => ComponentVisible(button) && button.Enabled && button.AccessibleName is not null && expected.Contains(button.AccessibleName)).ToArray();
            Require(current.Length is >= 1 and <= 3, "Unit page does not expose one to three choices.");
            foreach (var button in current) visited.Add(button.AccessibleName!);
            pages++;
            Require(pages <= expected.Count, "Unit search fallback paging did not terminate.");
            if (!next.Enabled) break;
            Click(next);
            Require(SelectedUnit(choices) == selected, "Changing a unit page changed the selected conversion unit.");
        }
        Require(visited.SetEquals(expected), "Some conversion units are unreachable through the visible pager: " + choices.AccessibleName);
        if (pages > 1)
        {
            Require(previous.Enabled, "Previous unit page is disabled after advancing.");
            Click(previous);
            Require(next.Enabled, "Next unit page did not become available after going back.");
        }
        SelectUnit(choices, 0);
    }

    private static Form Host(Control child, Size size)
    {
        var host = new Form { ClientSize = size, AutoScaleMode = AutoScaleMode.None };
        child.Dock = DockStyle.Fill;
        host.Controls.Add(child);
        Ui.Theme(host, Palette.Resolve(ThemePreference.Light));
        Prepare(host, 1);
        return host;
    }

    private static void Prepare(Form form, float scale)
    {
        // Create only hidden HWNDs; no Show(), ShowDialog(), Application.Run() or desktop automation.
        _ = form.Handle;
        LayoutTree(form);
        if (scale != 1) form.Scale(new SizeF(scale, scale));
        LayoutTree(form);
        EnsureHiddenHandles(form);
        LayoutTree(form);
    }

    private static void EnsureHiddenHandles(Control control)
    {
        // A mode added after initial preparation did not receive a HWND while its top-level form is hidden.
        // Creating the managed handle directly does not show the form or send desktop input.
        _ = control.Handle;
        foreach (var child in control.Controls.Cast<Control>().ToArray()) EnsureHiddenHandles(child);
    }

    private static void LayoutTree(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) LayoutTree(child);
        control.PerformLayout();
    }

    private static void CheckControls(Control scope)
    {
        foreach (var control in Descendants(scope).Prepend(scope))
        {
            Require(control is not ComboBox && control is not ContextMenuStrip, "Dropdown/menu control found: " + control.GetType().Name);
            Require(control.ContextMenuStrip is null, "Context menu is assigned to " + Identify(control));
            if (!Interactive(control)) continue;
            Require(!string.IsNullOrWhiteSpace(control.AccessibleName), "Interactive control lacks an accessible name: " + Identify(control));
        }
    }

    private static bool Interactive(Control control) => ComponentVisible(control) &&
        (control is CalcButton or SegmentedControl or RadioButton or CheckBox or ListBox or NumericUpDown
        || control is TextBox && control.Parent is not UpDownBase);

    private static (MethodInfo Method, object VisibleFlag) CreateVisibleStateAccess()
    {
        // Control.Visible includes hidden ancestors, so it cannot describe intentional visibility in
        // a never-shown host. Read the framework's own named Visible state without inventing a bit value.
        var method = typeof(Control).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SingleOrDefault(candidate => candidate.Name == "GetState" && candidate.ReturnType == typeof(bool) &&
                candidate.GetParameters().Length == 1 && candidate.GetParameters()[0].ParameterType.IsEnum);
        if (method is null) throw new InvalidOperationException("This WinForms runtime does not expose Control.GetState(enum); component visibility cannot be verified.");
        var stateType = method.GetParameters()[0].ParameterType;
        var name = Enum.GetNames(stateType).SingleOrDefault(value => value.Equals("Visible", StringComparison.OrdinalIgnoreCase));
        if (name is null) throw new InvalidOperationException("Control.GetState's enum does not define a named Visible state.");
        return (method, Enum.Parse(stateType, name));
    }

    private static bool ComponentVisible(Control control)
    {
        var access = visibleState.Value;
        for (Control? current = control; current is not null && current is not Form; current = current.Parent)
            if (access.Method.Invoke(current, [access.VisibleFlag]) is not true) return false;
        return true;
    }

    private static bool WithinResultDisplay(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent)
            if (current is ResultDisplay) return true;
        return false;
    }

    private static void CheckFrameChrome(MainForm form)
    {
        Require(form.FormBorderStyle == FormBorderStyle.None, "MainForm still has a native window border or title bar.");
        Require(form.WindowState == FormWindowState.Normal, "MainForm did not retain normal startup state after hidden preparation.");
        Require(form.Size == form.ClientSize, $"Frameless client/window sizes differ: window={form.Size}, client={form.ClientSize}.");
        foreach (var name in new[] { "Minimize window", "Maximize window", "Close window" })
        {
            var button = Named<CalcButton>(form, name);
            Require(ComponentVisible(button) && button.Enabled && button.TabStop, "Custom caption action is not displayed and keyboard accessible: " + name);
            CheckViewportContainment(button);
        }
    }

    private static void CheckWindowLayoutPolicy()
    {
        var workingAreas = new[] { new Size(1920, 1080), new Size(1366, 768), new Size(2560, 1440) };
        var modes = new[] { "Standard", "Scientific", "Programmer", "Converter", "Dates" };
        foreach (var area in workingAreas)
        foreach (var dpi in new[] { 96, 120, 144, 168, 192 })
        foreach (var mode in modes)
        foreach (var drawerVisible in new[] { false, true })
        {
            var metrics = WindowLayoutPolicy.Calculate(mode, area, dpi, drawerVisible);
            var prefix = $"{mode}, workarea={area}, targetDPI={dpi}, drawer={drawerVisible}";
            windowPolicyDiagnostics.Add(new(mode, area, dpi, drawerVisible, metrics.InitialSize, metrics.MinimumSize));
            Require(metrics.InitialSize.Width > 0 && metrics.InitialSize.Height > 0 && metrics.MinimumSize.Width > 0 && metrics.MinimumSize.Height > 0,
                "Nonpositive policy size: " + prefix);
            Require(metrics.MinimumSize.Width <= metrics.InitialSize.Width && metrics.MinimumSize.Height <= metrics.InitialSize.Height,
                "Minimum policy size exceeds initial size: " + prefix);
            Require(metrics.InitialSize.Width < area.Width && metrics.InitialSize.Height < area.Height,
                "Initial policy size does not leave normal-window working-area margin: " + prefix);
            Require(metrics.MinimumSize.Width <= area.Width && metrics.MinimumSize.Height <= area.Height,
                "Minimum policy size exceeds working area: " + prefix);
            Require(metrics == WindowLayoutPolicy.Calculate(mode, area, dpi, drawerVisible), "Sizing policy is not deterministic: " + prefix);
        }
        Require(windowPolicyDiagnostics.Count == 150, "Target-DPI policy matrix is incomplete.");
    }

    private static void CheckMemoryBar(ModeView view, string source)
    {
        if (view is not StandardView && view is not ScientificView) return;
        var names = new[] { "Clear all memory", "Recall newest memory", "Store in new memory slot", "Add to newest memory", "Subtract from newest memory", "Open memory list" };
        var memory = names.Select(name => Named<CalcButton>(view, name)).ToArray();
        var reference = memory[0];
        var row = reference.Parent ?? throw new InvalidOperationException("Memory row has no parent.");
        memoryBarDiagnostics.Add(new(source, view.DeviceDpi, view.Size, row.Bounds,
            row is TableLayoutPanel table ? table.GetRowHeights() : [],
            memory.Select(button => new MemoryButtonDiagnostic(button.AccessibleName ?? button.Text, button.Bounds,
                BoundsInScope(button, view), ComponentVisible(button))).ToArray()));
        foreach (var button in memory)
        {
            Require(ComponentVisible(button), "A required memory action is intentionally hidden: " + Identify(button));
            Require(Math.Abs(button.Width - reference.Width) <= 1 && button.Height == reference.Height,
                $"Memory actions must have equal sizes (one physical pixel of table-column rounding allowed): {Identify(button)}={button.Size}, reference={reference.Size}.");
            Require(BoundsInScope(button, view).Top == BoundsInScope(reference, view).Top, "Memory actions do not share a single aligned row.");
        }
        var firstTop = memory.Min(button => BoundsInScope(button, view).Top);
        var lastBottom = memory.Max(button => BoundsInScope(button, view).Bottom);
        var otherButtons = Descendants(view).OfType<CalcButton>().Except(memory).Where(button => ComponentVisible(button) && !WithinResultDisplay(button)).ToArray();
        Require(otherButtons.Length > 0, "No calculator buttons available to verify memory placement.");
        if (view is StandardView)
        {
            Require(lastBottom <= otherButtons.Min(button => BoundsInScope(button, view).Top), "Standard memory row is not above its keypad.");
            var display = Descendants(view).OfType<ResultDisplay>().Single();
            Require(BoundsInScope(display, view).Bottom <= firstTop, "Standard memory row overlaps or precedes the result display.");
        }
        else
            Require(firstTop >= otherButtons.Max(button => BoundsInScope(button, view).Bottom), "Scientific memory row is not below all calculator buttons.");
    }

    private static void CheckViewportContainment(Control scope)
    {
        var leaves = Descendants(scope).Prepend(scope).Where(control => Interactive(control) && control is not SegmentedControl);
        foreach (var control in leaves)
        {
            Require(control.Width > 0 && control.Height > 0, "Empty viewport control: " + Identify(control));
            var bounds = new Rectangle(Point.Empty, control.Size);
            for (Control? current = control; current?.Parent is { } ancestor; current = ancestor)
            {
                bounds.Offset(current.Location);
                Require(Rectangle.Inflate(ancestor.ClientRectangle, 1, 1).Contains(bounds),
                    $"Interactive control is clipped by an ancestor viewport: {Identify(control)}, ancestor={Identify(ancestor)}, bounds={bounds}, viewport={ancestor.ClientRectangle}.");
            }
        }
    }

    private static void CheckNoScrollbars(Control scope)
    {
        const int scrollbarStyles = 0x00100000 | 0x00200000;
        foreach (var control in Descendants(scope).Prepend(scope).Where(ComponentVisible))
        {
            if (control is ScrollableControl scroll)
                Require(!scroll.AutoScroll && !scroll.HorizontalScroll.Visible && !scroll.VerticalScroll.Visible,
                    "Scrollable layout remains active: " + Identify(control));
            if (control is TextBox text)
                Require(text.ScrollBars == ScrollBars.None, "Textbox scrollbar is configured: " + Identify(control));
            if (control is ListBox list)
                Require(!list.HorizontalScrollbar, "Listbox horizontal scrollbar is configured: " + Identify(control));
            if (control.IsHandleCreated)
                Require((GetWindowLong(control.Handle, -16) & scrollbarStyles) == 0, "Native scrollbar style remains on " + Identify(control));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    private static void CheckBounds(Control scope)
    {
        CheckResultDisplayLabels(scope);
        var leaves = Descendants(scope).Where(item => Interactive(item) && item is not SegmentedControl).ToArray();
        foreach (var control in leaves)
        {
            Require(control.Width > 0 && control.Height > 0, "Empty interactive bounds: " + Identify(control));
            if (control.Parent is not null)
            {
                var allowed = Rectangle.Inflate(control.Parent.ClientRectangle, 1, 1);
                Require(allowed.Contains(control.Bounds), $"Control extends beyond immediate parent: {Identify(control)} {control.Bounds}, parent {control.Parent.ClientRectangle}");
            }
            if (control is RadioButton segment && !string.IsNullOrWhiteSpace(segment.Text))
            {
                var icon = segment.GetType().GetProperty("Icon")?.GetValue(segment) is IconKind kind ? kind : IconKind.None;
                var textHeight = segment.ClientSize.Height - 2 - (icon != IconKind.None && segment.Height >= 44 * segment.DeviceDpi / 96 ? 25 * segment.DeviceDpi / 96 : 0);
                var measured = TextRenderer.MeasureText(segment.Text, segment.Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                Require(textHeight >= Math.Max(segment.Font.Height, measured.Height),
                    $"Selector text is vertically clipped: {Identify(segment)}, textHeight={textHeight}, fontHeight={segment.Font.Height}, measured={measured.Height}.");
                Require(segment.ClientSize.Width - 2 >= measured.Width,
                    $"Selector text is horizontally clipped: {Identify(segment)}, width={segment.ClientSize.Width - 2}, required={measured.Width}.");
            }
            if (control is CalcButton button)
            {
                const int logicalMinimum = 16;
                var physicalMinimum = (int)Math.Ceiling(logicalMinimum * control.DeviceDpi / 96d);
                Require(control.Width >= physicalMinimum && control.Height >= physicalMinimum,
                    $"Button is smaller than {logicalMinimum} logical pixels ({physicalMinimum} at actual DPI {control.DeviceDpi}): {Identify(control)} {control.Bounds}");
                if (!string.IsNullOrWhiteSpace(button.Text) && button.Text is not "+" and not "−" and not "×" and not "÷" and not "=")
                {
                    var measured = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty,
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    var availableHeight = button.VerticalIcon && button.Icon != IconKind.None ? button.Height / 2 - 7 * button.DeviceDpi / 96 : button.ClientSize.Height;
                    Require(availableHeight >= Math.Max(button.Font.Height, measured.Height),
                        $"Button text is vertically clipped: {Identify(button)}, height={availableHeight}, Font.Height={button.Font.Height}, measured={measured.Height}.");
                    if (button.Text.Length <= 4 && button.Icon == IconKind.None)
                        Require(button.ClientSize.Width >= measured.Width, $"Essential short button label is horizontally clipped: {Identify(button)}, width={button.ClientSize.Width}, required={measured.Width}.");
                }
            }
        }
        for (var first = 0; first < leaves.Length; first++) for (var second = first + 1; second < leaves.Length; second++)
        {
            var a = BoundsInScope(leaves[first], scope);
            var b = BoundsInScope(leaves[second], scope);
            var overlap = Rectangle.Intersect(a, b);
            Require(overlap.Width <= 1 || overlap.Height <= 1, $"Interactive controls overlap: {Identify(leaves[first])} {a} and {Identify(leaves[second])} {b}");
        }
    }

    private static void CheckResultDisplayLabels(Control scope)
    {
        foreach (var display in Descendants(scope).Prepend(scope).OfType<ResultDisplay>().Where(ComponentVisible))
        {
            foreach (var label in Descendants(display).OfType<Label>().Where(item => ComponentVisible(item) && !string.IsNullOrWhiteSpace(item.Text)))
            {
                var textBounds = Rectangle.FromLTRB(label.Padding.Left, label.Padding.Top,
                    label.ClientSize.Width - label.Padding.Right, label.ClientSize.Height - label.Padding.Bottom);
                var measured = TextRenderer.MeasureText(label.Text, label.Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                var requiredHeight = Math.Max(label.Font.Height, measured.Height);
                Require(textBounds.Width > 0 && textBounds.Height >= requiredHeight,
                    $"ResultDisplay label is vertically clipped: '{label.Text}', available text height={textBounds.Height}, " +
                    $"Font.Height={label.Font.Height}, measured height={measured.Height}, bounds={label.Bounds}, actual DPI={label.DeviceDpi}.");
                if (label.Parent is not null)
                    Require(Rectangle.Inflate(label.Parent.ClientRectangle, 1, 1).Contains(label.Bounds),
                        $"ResultDisplay label extends outside its layout parent: '{label.Text}', bounds={label.Bounds}, parent={label.Parent.ClientRectangle}.");
            }
        }
    }

    private static Rectangle BoundsInScope(Control control, Control scope)
    {
        var location = Point.Empty;
        for (Control? current = control; current is not null && current != scope; current = current.Parent)
            location.Offset(current.Location);
        return new Rectangle(location, control.Size);
    }

    private static void Snapshot(Control control, string file, Control? expectedContent = null)
    {
        EnsureHiddenHandles(control);
        LayoutTree(control);
        CaptureDisplayDiagnostics(control, file);
        try
        {
            // Native styles retained for Windows snap/resize can make hidden Form.DrawToBitmap
            // synthesize an OS caption absent from the displayed borderless window. Capture its
            // client layout directly; window/client viewport assertions still use the real form.
            var captureRoot = control is MainForm main
                ? main.Controls.Cast<Control>().Single(child => child.Dock == DockStyle.Fill)
                : control;
            using var bitmap = new Bitmap(captureRoot.Width, captureRoot.Height);
            captureRoot.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(runDirectory, file), ImageFormat.Png);
            screenshotCount++;
            CheckRenderedContent(bitmap, captureRoot, expectedContent ?? captureRoot, file);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
        {
            results.Add(new CheckResult("Render " + file, "SKIP", ex.Message));
        }
    }

    private static void CheckRenderedContent(Bitmap bitmap, Control root, Control content, string source)
    {
        var candidates = Descendants(content).Prepend(content).OfType<CalcButton>()
            .Where(button => ComponentVisible(button) && button.Enabled && !string.IsNullOrWhiteSpace(button.Text))
            .Select(button => (Control: button, Bounds: VisibleBitmapBounds(button, root, bitmap.Size)))
            .Where(item => item.Bounds.Width >= 12 && item.Bounds.Height >= 12)
            .Take(12).ToArray();
        if (candidates.Length == 0)
        {
            Require(content is not ModeView && root is not SettingsForm,
                "No visible custom button regions were available to validate expected snapshot content: " + source);
            // The font regression host contains only the native result textbox, whose bitmap fidelity is
            // recorded as a limitation. MainForm modes and Preferences always have custom-painted buttons.
            renderDiagnostics.Add(new RenderDiagnostic(source, content.GetType().Name, 0, 0,
                "No text-bearing custom buttons available for a render probe; bitmap is retained without this probe."));
            return;
        }
        var painted = 0;
        foreach (var candidate in candidates)
        {
            Require(candidate.Control.IsHandleCreated, "Missing hidden handle for snapshot content: " + Identify(candidate.Control));
            var bounds = candidate.Bounds;
            var reference = bitmap.GetPixel(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            var different = 0;
            for (var y = bounds.Top + 2; y < bounds.Bottom - 2; y += 2)
            {
                for (var x = bounds.Left + 2; x < bounds.Right - 2; x += 2)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (Math.Abs(color.R - reference.R) + Math.Abs(color.G - reference.G) + Math.Abs(color.B - reference.B) > 24)
                        different++;
                }
            }
            if (different < 8)
            {
                // Tiny enabled chevrons can fall between a two-pixel sampling grid. Inspect the
                // whole same inset region before declaring it blank; keep the eight-pixel threshold.
                different = 0;
                for (var y = bounds.Top + 2; y < bounds.Bottom - 2; y++)
                    for (var x = bounds.Left + 2; x < bounds.Right - 2; x++)
                    {
                        var color = bitmap.GetPixel(x, y);
                        if (Math.Abs(color.R - reference.R) + Math.Abs(color.G - reference.G) + Math.Abs(color.B - reference.B) > 24)
                            different++;
                    }
            }
            if (different >= 8) painted++;
        }
        renderDiagnostics.Add(new RenderDiagnostic(source, content.GetType().Name, candidates.Length, painted,
            "Samples are restricted to enabled expected-content buttons, excluding headers/navigation. Sparse small-glyph regions receive full-pixel sampling with the same eight-pixel detail threshold."));
        Require(painted >= Math.Min(3, candidates.Length),
            $"Snapshot content appears blank or incomplete: {source}; {painted} of {candidates.Length} expected button regions contain painted detail. PNG retained for inspection.");
    }

    private static Rectangle VisibleBitmapBounds(Control control, Control root, Size bitmapSize)
    {
        var bounds = BoundsInScope(control, root);
        for (var ancestor = control.Parent; ancestor is not null && ancestor != root; ancestor = ancestor.Parent)
        {
            var ancestorBounds = BoundsInScope(ancestor, root);
            bounds = Rectangle.Intersect(bounds, new Rectangle(ancestorBounds.Location, ancestor.ClientSize));
        }
        if (root is Form form)
        {
            // Form.DrawToBitmap includes nonclient borders, while BoundsInScope uses client coordinates.
            var clientOrigin = form.PointToScreen(Point.Empty);
            bounds.Offset(clientOrigin.X - form.Left, clientOrigin.Y - form.Top);
        }
        return Rectangle.Intersect(bounds, new Rectangle(Point.Empty, bitmapSize));
    }

    private static void CaptureDisplayDiagnostics(Control root, string source)
    {
        foreach (var display in Descendants(root).Prepend(root).OfType<ResultDisplay>())
        {
            var children = Descendants(display).Prepend(display).Select(control =>
            {
                var rows = control is TableLayoutPanel table
                    ? table.RowStyles.Cast<RowStyle>().Select(row => $"{row.SizeType}: {row.Height.ToString(CultureInfo.InvariantCulture)}").ToArray()
                    : [];
                var actualRows = control is TableLayoutPanel sizedTable ? sizedTable.GetRowHeights() : [];
                string font;
                try
                {
                    font = $"{control.Font.Name}; {control.Font.SizeInPoints.ToString(CultureInfo.InvariantCulture)}pt; {control.Font.Style}; height={control.Font.Height}";
                }
                catch (Exception ex) { font = "FONT ERROR: " + ex.GetType().Name + ": " + ex.Message; }
                return new DisplayChildDiagnostic(control.GetType().Name, control.AccessibleName, control.Text,
                    control.Bounds.ToString(), control.ClientRectangle.ToString(), control.Parent?.GetType().Name,
                    control.Dock.ToString(), control.Margin.ToString(), control.Padding.ToString(), control.DeviceDpi,
                    control.IsHandleCreated, ComponentVisible(control), font, rows, actualRows);
            }).ToArray();
            displayDiagnostics.Add(new DisplayDiagnostic(source, display.Bounds.ToString(), display.DeviceDpi, children));
        }
    }

    private static T Named<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>()
        .Single(item => item.AccessibleName == name);

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static string Identify(Control control) => control.GetType().Name + " '" + (control.AccessibleName ?? control.Text) + "'";

    private static void Click(CalcButton button)
    {
        Require(button.Enabled, "Cannot activate a disabled component: " + Identify(button));
        Require(ComponentVisible(button), "Cannot activate an intentionally hidden component: " + Identify(button));
        // PerformClick() intentionally ignores hidden buttons. Invoke the control event in-process instead.
        try { click.Invoke(button, [EventArgs.Empty]); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); }
    }

    private static void Run(string name, Action action)
    {
        if (focus.Length > 0 && !name.Contains(focus, StringComparison.OrdinalIgnoreCase)) return;
        try { action(); results.Add(new CheckResult(name, "PASS", "Completed.")); Console.WriteLine("PASS " + name); }
        catch (Exception ex)
        {
            var detail = ex.ToString();
            results.Add(new CheckResult(name, "FAIL", detail));
            Console.WriteLine("FAIL " + name + ": " + ex.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record CheckResult(string Name, string Status, string Detail);
    private sealed record ActualWindowDiagnostic(string Source, int ActualDeviceDpi, float RelativeScale, bool MinimumSizeCase,
        Size WindowSize, Size ClientSize, Size MinimumSize, Rectangle WorkingArea, string BorderStyle, string WindowState);
    private sealed record WindowPolicyDiagnostic(string Mode, Size WorkingArea, int TargetDpi, bool DrawerVisible, Size InitialSize, Size MinimumSize);
    private sealed record MemoryBarDiagnostic(string Source, int ActualDeviceDpi, Size ViewSize, Rectangle RowBounds,
        int[] ActualRowHeights, MemoryButtonDiagnostic[] Buttons);
    private sealed record MemoryButtonDiagnostic(string Name, Rectangle Bounds, Rectangle BoundsInView, bool ComponentVisible);
    private sealed record EffectiveWorkspaceDiagnostic(string Source, string Geometry, int ActualDeviceDpi, int TargetDpi, Size TargetWorkingArea,
        Size TargetInitialSize, Size TargetMinimumSize, Size EquivalentInitialSize, Size EquivalentMinimumSize, Size AppliedWindowSize, Size AppliedClientSize);
    private sealed record RenderDiagnostic(string Source, string ContentType, int ProbedButtons, int PaintedButtons, string Detail);
    private sealed record DisplayDiagnostic(string Source, string Bounds, int ActualDeviceDpi, DisplayChildDiagnostic[] Controls);
    private sealed record DisplayChildDiagnostic(string Type, string? AccessibleName, string Text, string Bounds,
        string ClientRectangle, string? ParentType, string Dock, string Margin, string Padding, int ActualDeviceDpi,
        bool IsHandleCreated, bool ComponentVisible, string Font, string[] RowStyles, int[] ActualRowHeights);
}
