using System.Reflection;
using Microsoft.Win32;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.WinForms.Forms;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.UiChecks;

/// <summary>Managed window-size policy and lifecycle checks; native resize and monitor changes are not exercised.</summary>
public static class WindowStateAuditChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Window state: preserve manually resized bounds through mode sizing", PreserveManualSize);
        check("Window state: drawer adjusts width without resetting height", DrawerWidthAdjustment);
        check("Window state: drawer expansion respects working-area cap and reverses exactly", DrawerCappedExpansion);
        check("Window state: opening drawer from minimum size expands only once", DrawerMinimumExpansion);
        check("Window state: clamp preserved bounds to minimum and working area", ClampBounds);
        check("Window state: palette refresh retains storage warning color", WarningPalette);
        check("Window state: system preference callback tolerates disposed form", DisposedCallback);
    }

    private static void PreserveManualSize() => WithForm((form, _) =>
    {
        var area = new Rectangle(0, 0, 4096, 3072);
        form.PrepareWindowLayout(area);
        form.Size = new(Math.Max(form.Width, 1500), Math.Max(form.Height, 1600));
        var chosen = form.Size;
        foreach (var mode in new[] { "Scientific", "Programmer", "Converter", "Dates", "Standard" })
        {
            Invoke(form, "SwitchMode", mode);
            Invoke(form, "ApplyWindowSizing", area, false, 0);
            Require(form.Size == chosen, $"Mode sizing replaced the manually selected dimensions in {mode}: actual={form.Size}, expected={chosen}, DPI={form.DeviceDpi}.");
        }
    });

    private static void DrawerWidthAdjustment() => WithForm((form, _) =>
    {
        var area = Screen.FromControl(form).WorkingArea;
        form.PrepareWindowLayout(area);
        var delta = DesignTokens.DrawerWidth * form.DeviceDpi / 96;
        form.Width = Math.Max(form.Width, area.Width - delta - 32);
        var original = form.Size;
        Invoke(form, "SetDrawer", true);
        Invoke(form, "ResizeForDrawer", area, true);
        var expected = new Size(Math.Min(area.Width, original.Width + delta), original.Height);
        Require(form.Size == expected, $"Drawer expansion reset the window size: actual={form.Size}, expected={expected}, original={original}, delta={delta}, area={area}, DPI={form.DeviceDpi}.");
        Invoke(form, "SetDrawer", false);
        Invoke(form, "ResizeForDrawer", area, false);
        Require(form.Size == original, $"Drawer collapse failed to recover the original dimensions: actual={form.Size}, expected={original}, area={area}, DPI={form.DeviceDpi}.");
    });

    private static void DrawerCappedExpansion() => WithForm((form, _) =>
    {
        var area = Screen.FromControl(form).WorkingArea;
        form.PrepareWindowLayout(area);
        var delta = DesignTokens.DrawerWidth * form.DeviceDpi / 96;
        form.Width = Math.Max(form.MinimumSize.Width, area.Width - delta / 2);
        var original = form.Size;
        Invoke(form, "SetDrawer", true);
        Invoke(form, "ResizeForDrawer", area, true);
        Require(form.Width == area.Width, $"Drawer expansion did not reach the working-area cap: actual={form.Size}, expectedWidth={area.Width}, original={original}, delta={delta}, DPI={form.DeviceDpi}.");
        Invoke(form, "SetDrawer", false);
        Invoke(form, "ResizeForDrawer", area, false);
        Require(form.Size == original, $"Closing a capped drawer subtracted more width than opening added: actual={form.Size}, expected={original}, area={area}, DPI={form.DeviceDpi}.");
    });

    private static void DrawerMinimumExpansion() => WithForm((form, _) =>
    {
        var area = new Rectangle(0, 0, 4096, 3072);
        form.PrepareWindowLayout(area);
        form.Size = form.MinimumSize;
        var original = form.Size;
        var delta = DesignTokens.DrawerWidth * form.DeviceDpi / 96;
        Invoke(form, "SetDrawer", true);
        Invoke(form, "ResizeForDrawer", area, true);
        Require(form.Width == original.Width + delta, $"Updating the drawer minimum caused a second expansion: actual={form.Size}, expectedWidth={original.Width + delta}, original={original}, delta={delta}, DPI={form.DeviceDpi}.");
        Invoke(form, "SetDrawer", false);
        Invoke(form, "ResizeForDrawer", area, false);
        Require(form.Size == original, $"Minimum-size drawer changes failed to recover the original dimensions: actual={form.Size}, expected={original}, DPI={form.DeviceDpi}.");
    });

    private static void ClampBounds() => WithForm((form, _) =>
    {
        var area = new Rectangle(200, 150, 1100, 850);
        form.PrepareWindowLayout(new(0, 0, 4096, 3072));
        form.Size = new(2000, 1700);
        form.Location = new(-500, -400);
        Invoke(form, "ApplyWindowSizing", area, false, 0);
        Require(area.Contains(form.Bounds), "Resized bounds exceed the new monitor working area.");
        Invoke(form, "SwitchMode", "Programmer");
        form.MinimumSize = Size.Empty;
        form.Size = new(200, 200);
        Invoke(form, "ApplyWindowSizing", area, false, 0);
        Require(form.Width >= form.MinimumSize.Width && form.Height >= form.MinimumSize.Height,
            "Mode sizing failed to enforce its minimum dimensions.");
        Require(area.Contains(form.Bounds), "Minimum dimensions pushed the window outside the working area.");
    });

    private static void WarningPalette() => WithForm((form, directory) =>
    {
        // Turn the profile directory into a file to cause a recoverable storage failure.
        File.WriteAllText(directory, "blocked profile directory");
        Require(Invoke(form, "TrySaveState") is false, "Blocked profile unexpectedly saved.");
        Invoke(form, "ApplyPalette");
        var status = Field<Label>(form, "status");
        var palette = Field<Palette>(form, "palette");
        Require(status.ForeColor == palette.Error && status.Text.Contains("may still be on disk", StringComparison.Ordinal),
            "Palette refresh hid the active storage warning.");
    });

    private static void DisposedCallback() => WithForm((form, _) =>
    {
        Require(form.Handle != nint.Zero, "The hidden form did not create its native handle.");
        form.Dispose();
        Invoke(form, "OnSystemPreference", form, new UserPreferenceChangedEventArgs(UserPreferenceCategory.General));
    });

    private static void WithForm(Action<MainForm, string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterWindowState-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var form = new MainForm(new StateStore(directory));
            // HWND creation establishes actual monitor DPI and completes native bounds normalization
            // before fixtures choose physical sizes. Every window remains hidden.
            PrepareHiddenControls(form);
            action(form, directory);
            Require(!form.Visible, "Window-state component checks must never show native windows.");
        }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Require(string.Equals(Path.GetDirectoryName(resolved), temporaryRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("AsterWindowState-", StringComparison.Ordinal), "Unexpected test profile path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            else if (File.Exists(resolved)) File.Delete(resolved);
        }
    }

    private static void PrepareHiddenControls(Control control)
    {
        _ = control.Handle;
        control.PerformLayout();
        foreach (Control child in control.Controls) PrepareHiddenControls(child);
        control.PerformLayout();
    }

    private static object? Invoke(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
