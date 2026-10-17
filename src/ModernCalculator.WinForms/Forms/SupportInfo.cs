using System.Globalization;
using System.Reflection;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Forms;

/// <summary>An allowlisted local support summary; it never reads calculation or profile data.</summary>
public static class SupportInfo
{
    public const string IssueTracker = "https://github.com/gcfernando/aster/issues";
    public static string Version => typeof(SupportInfo).Assembly.GetName().Version?.ToString(3) ?? "Unknown";
    public static string Build => typeof(SupportInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Version;
    public static string Capture(AppSettings settings, Palette palette, int dpi) => string.Join(Environment.NewLine,
        "Product: Aster Calculator", "Version: " + Version, "Build: " + Build,
        "Runtime: " + RuntimeInformation.FrameworkDescription,
        "OS: " + RuntimeInformation.OSDescription,
        $"Architecture: OS {RuntimeInformation.OSArchitecture}; process {RuntimeInformation.ProcessArchitecture}",
        "Culture: " + CultureInfo.CurrentCulture.Name, "Mode: " + settings.Mode,
        "Theme: " + (SystemInformation.HighContrast || settings.HighContrast ? "High contrast" : palette.Dark ? "Dark" : "Light"),
        "Theme preference: " + settings.Theme, "DPI: " + dpi.ToString(CultureInfo.InvariantCulture),
        "Display digits: " + settings.Precision.ToString(CultureInfo.InvariantCulture),
        "Density: " + settings.Density,
        "No calculations, history, memory, usernames or profile paths are included. Nothing is sent automatically.");

    public static string Copy(string text, Action<string>? write = null)
    {
        try { var copy = write ?? (value => Clipboard.SetText(value)); copy(text); return "Support information copied. Review it before sharing; nothing was sent."; }
        catch (ExternalException) { return "The clipboard is busy. Try copying support information again."; }
    }
    public static string OpenIssueTracker(Action<string>? open = null)
    {
        try
        {
            var navigate = open ?? (url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }));
            navigate(IssueTracker); return "Support site opened in your browser. No diagnostics were sent.";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { return "The support site could not be opened. Visit " + IssueTracker + " in your browser."; }
    }
}
