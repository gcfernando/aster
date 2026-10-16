using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.History;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class InputAndPersistenceTests
{
    [Theory]
    [InlineData("en-US", "1,234.50", "1234.5")]
    [InlineData("sv-SE", "12,5", "12.5")]
    [InlineData("de-DE", "1.234,5", "1234.5")]
    [InlineData("sv-SE", "1.5", "1.5")]
    [InlineData("en-US", "−12.5", "-12.5")]
    [InlineData("en-US", "1.2e3", "1200")]
    [InlineData("hi-IN", "12,34,567.8", "1234567.8")]
    public void ParsesCultureAwareClipboard(string culture, string input, string expected)
    {
        Assert.True(NumberText.TryDecimal(input, CultureInfo.GetCultureInfo(culture), out var actual));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), actual);
    }
    [Theory] [InlineData("1,23")] [InlineData("12 34")] [InlineData("1/2")] [InlineData("NaN")]
    [InlineData("Infinity")] [InlineData("1e100")] [InlineData("1e-40")] [InlineData("1+2")] [InlineData("1,234.5,6")]
    public void InvalidNumericClipboard(string input) => Assert.False(NumberText.TryDecimal(input, CultureInfo.GetCultureInfo("en-US"), out _));
    [Theory] [InlineData("0.12345678901234567890123456789")] [InlineData("1.00000000000000000000000000001")]
    public void RejectsSilentDecimalRounding(string input) => Assert.False(NumberText.TryDecimal(input, CultureInfo.InvariantCulture, out _));
    [Fact] public void ExactDecimalBoundaryIsAccepted()
    {
        Assert.True(NumberText.TryDecimal(decimal.MaxValue.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out var value));
        Assert.Equal(decimal.MaxValue, value);
    }
    [Theory] [InlineData("1,23")] [InlineData("NaN")] [InlineData("1e400")] [InlineData("1e-400")]
    public void RejectsInvalidFloatingClipboard(string input) => Assert.False(NumberText.TryDouble(input, CultureInfo.GetCultureInfo("en-US"), out _));
    [Theory]
    [InlineData("en-US", "1e-999", false)]
    [InlineData("en-US", "1,000", false)]
    [InlineData("de-DE", "1.000,5", false)]
    [InlineData("de-DE", "1,5", true)]
    [InlineData("en-US", "1e-300", true)]
    [InlineData("en-US", "0e-999", true)]
    public void UngroupedFloatingInputRejectsUnderflowAndGrouping(string culture, string text, bool valid)
        => Assert.Equal(valid, NumberText.TryDouble(text, CultureInfo.GetCultureInfo(culture), out _, allowGrouping: false));
    [Fact] public void FormattingDoesNotChangeNumbers()
    {
        Assert.Equal("1,234.5", NumberText.Format(1234.5m, CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("0", NumberText.Format(-0d, CultureInfo.InvariantCulture));
        Assert.Contains("E", NumberText.Format(1e100, CultureInfo.InvariantCulture));
    }
    [Fact] public void JournalIsBoundedSearchableAndRestorable()
    {
        var journal = new HistoryJournal();
        for (var i = 0; i < 210; i++) journal.Record("Standard", $"{i}+1", (i + 1).ToString(CultureInfo.InvariantCulture));
        Assert.Equal(200, journal.Items.Count); Assert.Single(journal.Search("209+"));
        var other = new HistoryJournal(); other.Restore(journal.Items.Concat(journal.Items)); Assert.Equal(200, other.Items.Count);
        other.Delete(other.Items[0].Id); Assert.Equal(199, other.Items.Count); other.Clear(); Assert.Empty(other.Items);
    }
    [Fact] public void ScientificHistoryLocalizationRetainsTinyResultPrecision()
    {
        const string result = "1.2345678912345678E-25";
        var item = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "Scientific", "1/large", result);
        var culture = CultureInfo.GetCultureInfo("sv-SE");
        Assert.Equal(double.Parse(result, CultureInfo.InvariantCulture), double.Parse(item.LocalizedResult(culture), culture));
    }
    [Fact] public void DecimalHistoryLocalizationIsExact()
    {
        var item = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "Standard", "large", decimal.MaxValue.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(decimal.MaxValue.ToString(CultureInfo.GetCultureInfo("de-DE")), item.LocalizedResult(CultureInfo.GetCultureInfo("de-DE")));
    }
    [Fact] public void SaveDefaultsDoNotPersistHistory() => InTemporaryDirectory(directory =>
    {
        var store = new StateStore(directory); var journal = new HistoryJournal(); journal.Record("Standard", "1+2", "3");
        Assert.Null(store.Save(new(), journal.Items)); Assert.Empty(store.Load().State.History);
        Assert.DoesNotContain("1+2", File.ReadAllText(Path.Combine(directory, "state.json")));
    });
    [Fact] public void PreferencesAndOptedInHistoryRoundTrip() => InTemporaryDirectory(directory =>
    {
        var store = new StateStore(directory); var settings = new AppSettings { Theme = ThemePreference.Light, Precision = 8, PersistHistory = true };
        var journal = new HistoryJournal(); journal.Record("Scientific", "sin(30)", ".5");
        Assert.Null(store.Save(settings, journal.Items)); var loaded = store.Load(); Assert.Null(loaded.Warning);
        Assert.Equal(settings, loaded.State.Settings); Assert.Single(loaded.State.History); Assert.Equal(".5", loaded.State.History[0].Result);
        Assert.Single(Directory.GetFiles(directory));
    });
    [Fact] public void CorruptStateRecoversAndCanBeReplaced() => InTemporaryDirectory(directory =>
    {
        File.WriteAllText(Path.Combine(directory, "state.json"), "{broken"); var store = new StateStore(directory);
        Assert.NotNull(store.Load().Warning); Assert.Equal(new AppSettings(), store.Load().State.Settings);
        Assert.Null(store.Save(new(), [])); Assert.Null(store.Load().Warning);
    });
    [Fact] public void InvalidSettingsAreValidated()
    {
        var validated = new AppSettings { Theme = (ThemePreference)123, Mode = "Unknown", Precision = 123 }.Validated();
        Assert.Equal(ThemePreference.Dark, validated.Theme); Assert.Equal("Standard", validated.Mode); Assert.Equal(15, validated.Precision);
    }
    [Fact] public void MissingFileUsesDefaults() => InTemporaryDirectory(directory => Assert.Null(new StateStore(directory).Load().Warning));
    private static void InTemporaryDirectory(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try { test(directory); } finally { Directory.Delete(directory, true); }
    }
}
