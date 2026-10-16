// Developer ::> Gehan Fernando
namespace ModernCalculator.Infrastructure.Settings;

public enum ThemePreference { System, Dark, Light }
public enum DensityPreference { Automatic, Compact }
public enum ModeShortcutPreference { ControlNumbers, ControlAltNumbers }
public sealed record ConversionFavorite(string Category, string FromUnit, string ToUnit);
public sealed record AppSettings
{
    public ThemePreference Theme { get; init; } = ThemePreference.Light;
    public int Precision { get; init; } = 12;
    public bool GroupDigits { get; init; } = true;
    public bool PersistHistory { get; init; }
    public bool AlwaysOnTop { get; init; }
    public bool ReducedMotion { get; init; }
    public bool HighContrast { get; init; }
    public DensityPreference Density { get; init; }
    public ModeShortcutPreference ModeShortcuts { get; init; }
    public string Mode { get; init; } = "Standard";
    // Replace invalid saved options before the UI consumes them.
    public AppSettings Validated() => this with
    {
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.Dark,
        Precision = Math.Clamp(Precision, 3, 15),
        Density = Enum.IsDefined(Density) ? Density : DensityPreference.Automatic,
        ModeShortcuts = Enum.IsDefined(ModeShortcuts) ? ModeShortcuts : ModeShortcutPreference.ControlNumbers,
        Mode = Mode is "Standard" or "Scientific" or "Programmer" or "Converter" or "Dates" ? Mode : "Standard"
    };
}
