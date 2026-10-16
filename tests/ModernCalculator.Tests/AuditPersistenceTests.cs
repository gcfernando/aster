using ModernCalculator.Core.Calculations;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class AuditPersistenceTests
{
    [Fact] public void SnapshotsPreservePendingAndRepeatedOperations()
    {
        var calculator = new StandardCalculator(); calculator.SetValue(125); calculator.Operator(BinaryOperation.Multiply);
        var pending = calculator.Snapshot(); calculator.Digit('8'); calculator.Equals(); Assert.Equal(1000, calculator.Value);
        var complete = calculator.Snapshot(); calculator.Clear(); calculator.Restore(pending); calculator.Digit('4'); calculator.Equals(); Assert.Equal(500, calculator.Value);
        calculator.Restore(complete); calculator.Equals(); Assert.Equal(8000, calculator.Value);
    }
    [Fact] public void SnapshotsRetainEditableTrailingZerosAndErrors()
    {
        var calculator = new StandardCalculator(); calculator.Digit('1'); calculator.DecimalPoint(); calculator.Digit('0'); calculator.Digit('0');
        var typed = calculator.Snapshot(); calculator.Clear(); calculator.Restore(typed); Assert.Equal("1.00", calculator.Entry); calculator.Backspace(); Assert.Equal("1.0", calculator.Entry);
        calculator.Operator(BinaryOperation.Divide); calculator.Digit('0'); calculator.Equals(); var invalid = calculator.Snapshot();
        calculator.Clear(); calculator.Restore(invalid); Assert.NotNull(calculator.Error); calculator.Digit('5'); Assert.Null(calculator.Error); Assert.Equal(5, calculator.Value);
    }
    [Fact] public void NewPreferencesAndFavoritePairsRoundTripWithoutHistoryOptIn() => InDirectory(directory =>
    {
        var store = new StateStore(directory);
        var settings = new AppSettings { ReducedMotion = true, HighContrast = true, Density = DensityPreference.Compact,
            ModeShortcuts = ModeShortcutPreference.ControlAltNumbers, AlwaysOnTop = true, Precision = 7 };
        ConversionFavorite[] pairs = [new("Length", "Metre", "Kilometre"), new("Temperature", "Celsius", "Fahrenheit")];
        Assert.Null(store.Save(settings, [], pairs)); var loaded = store.Load(); Assert.Null(loaded.Warning);
        Assert.Equal(settings, loaded.State.Settings); Assert.Equal(pairs, loaded.State.ConversionFavorites); Assert.Empty(loaded.State.History);
    });
    [Fact] public void FavoritesAreValidatedDeduplicatedAndBounded() => InDirectory(directory =>
    {
        var store = new StateStore(directory);
        var pairs = Enumerable.Repeat(new ConversionFavorite("Length", "Metre", "Kilometre"), 30)
            .Concat([new("Unknown", "Metre", "Kilometre"), new("Mass", "Metre", "Kilogram")]);
        Assert.Null(store.Save(new(), [], pairs)); Assert.Single(store.Load().State.ConversionFavorites!);
    });
    [Fact] public void LegacyStateWithoutFavoritesAndNewPreferencesStillLoads() => InDirectory(directory =>
    {
        File.WriteAllText(Path.Combine(directory, "state.json"), "{\"Settings\":{\"Theme\":2,\"Precision\":12},\"History\":[]}");
        var loaded = new StateStore(directory).Load(); Assert.Null(loaded.Warning); Assert.False(loaded.State.Settings.ReducedMotion);
        Assert.Equal(DensityPreference.Automatic, loaded.State.Settings.Density); Assert.Empty(loaded.State.ConversionFavorites!);
    });
    [Fact] public void UnsupportedPreferencesUseSafeDefaults()
    {
        var settings = new AppSettings { Density = (DensityPreference)900, ModeShortcuts = (ModeShortcutPreference)900 }.Validated();
        Assert.Equal(DensityPreference.Automatic, settings.Density); Assert.Equal(ModeShortcutPreference.ControlNumbers, settings.ModeShortcuts);
    }
    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterAuditTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try { action(directory); } finally { Directory.Delete(directory, true); }
    }
}
