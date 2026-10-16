// Developer ::> Gehan Fernando
using Microsoft.Win32;
using ModernCalculator.Infrastructure.Settings;

namespace ModernCalculator.WinForms.Themes;

public sealed record Palette(Color Window, Color Surface, Color Key, Color NumberKey, Color Border,
    Color Text, Color Muted, Color Accent, Color AccentText, Color Warm, Color Error, bool Dark)
{
    public static Palette Resolve(ThemePreference preference, bool highContrast = false)
    {
        // Resolve system contrast colors before applying the requested light or dark theme.
        if (SystemInformation.HighContrast || highContrast)
            return new(SystemColors.Window, SystemColors.Control, SystemColors.Control, SystemColors.Control,
                SystemColors.WindowText, SystemColors.WindowText, SystemColors.WindowText, SystemColors.Highlight,
                SystemColors.HighlightText, SystemColors.WindowText, SystemColors.WindowText, false);
        var dark = preference == ThemePreference.Dark || preference == ThemePreference.System && SystemIsDark();
        return dark
            ? new(Color.FromArgb(24, 25, 28), Color.FromArgb(31, 33, 37), Color.FromArgb(41, 43, 48),
                Color.FromArgb(50, 53, 58), Color.FromArgb(111, 121, 141), Color.FromArgb(245, 245, 247),
                Color.FromArgb(177, 182, 194), Color.FromArgb(159, 190, 255), Color.FromArgb(15, 32, 66),
                Color.FromArgb(159, 190, 255), Color.FromArgb(255, 156, 151), true)
            : new(Color.FromArgb(246, 247, 250), Color.White, Color.FromArgb(239, 242, 247),
                Color.FromArgb(248, 249, 252), Color.FromArgb(119, 130, 149), Color.FromArgb(17, 31, 58),
                Color.FromArgb(79, 94, 119), Color.FromArgb(25, 105, 236), Color.White,
                Color.FromArgb(25, 105, 236), Color.FromArgb(177, 40, 40), false);
    }

    private static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (System.Security.SecurityException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}

public interface IThemedControl { void ApplyTheme(Palette palette); }

public static class ThemePainter
{
    public static void Apply(Control control, Palette palette)
    {
        control.BackColor = palette.Surface;
        control.ForeColor = palette.Text;
        if (control is TextBox textBox) { textBox.BackColor = textBox.ReadOnly ? palette.Surface : palette.Key; textBox.ForeColor = palette.Text; }
        foreach (Control child in control.Controls) Apply(child, palette);
        if (control is IThemedControl themed) themed.ApplyTheme(palette);
    }
}
