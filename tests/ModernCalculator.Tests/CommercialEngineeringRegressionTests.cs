using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ModernCalculator.Core.History;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class CommercialEngineeringRegressionTests
{
    [Theory]
    [InlineData("2")]
    [InlineData("999")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("null")]
    [InlineData("\"future\"")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public void UnsupportedSchemasSurviveLoadAndDirectSave(string schema) => InDirectory(directory =>
    {
        var path = Path.Combine(directory, "state.json");
        File.WriteAllText(path, "{\"SchemaVersion\":" + schema + ",\"FutureData\":{\"Keep\":true},\"Settings\":{},\"History\":[]}", new UTF8Encoding(true));
        var original = File.ReadAllBytes(path);
        Assert.NotNull(new StateStore(directory).Load().Warning);
        Assert.NotNull(new StateStore(directory).Save(new AppSettings(), []));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(directory));
    });

    [Fact]
    public void FutureSchemaAfterDeepPayloadOrLeadingWhitespaceRemainsProtected() => InDirectory(directory =>
    {
        var path = Path.Combine(directory, "state.json");
        var original = " \r\n{\"Future\":" + new string('[', 100) + "0" + new string(']', 100) + ",\"SchemaVersion\":2,\"Settings\":{},\"History\":[]}";
        File.WriteAllText(path, original, Encoding.Unicode);
        var bytes = File.ReadAllBytes(path);
        Assert.NotNull(new StateStore(directory).Load().Warning);
        Assert.NotNull(new StateStore(directory).Save(new(), []));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    });

    [Fact]
    public void OversizedUnknownProfileCannotBypassSchemaProtection() => InDirectory(directory =>
    {
        var path = Path.Combine(directory, "state.json");
        var original = Encoding.UTF8.GetBytes("{\"SchemaVersion\":2,\"Future\":\"" + new string('x', 2 * 1024 * 1024) + "\"}");
        File.WriteAllBytes(path, original);
        Assert.NotNull(new StateStore(directory).Save(new(), []));
        Assert.Equal(original, File.ReadAllBytes(path));
    });

    [Fact]
    public void NewerSchemaWrittenAfterLoadIsStillProtected() => InDirectory(directory =>
    {
        var store = new StateStore(directory);
        Assert.Null(store.Save(new(), []));
        Assert.Null(store.Load().Warning);
        var path = Path.Combine(directory, "state.json");
        const string future = "{\"SchemaVersion\":2,\"Settings\":{},\"History\":[],\"Future\":true}";
        File.WriteAllText(path, future);
        Assert.NotNull(store.Save(new AppSettings { Precision = 4 }, []));
        Assert.Equal(future, File.ReadAllText(path));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyAndCurrentProfilesLoadAndWriteCurrentSchema(bool includeVersion) => InDirectory(directory =>
    {
        var path = Path.Combine(directory, "state.json");
        File.WriteAllText(path, "{" + (includeVersion ? "\"SchemaVersion\":1," : "") + "\"Settings\":{\"Precision\":8},\"History\":[]}");
        var store = new StateStore(directory);
        var loaded = store.Load();
        Assert.Null(loaded.Warning);
        Assert.Equal(8, loaded.State.Settings.Precision);
        Assert.Equal(1, loaded.State.SchemaVersion);
        Assert.Null(store.Save(loaded.State.Settings, loaded.State.History));
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, saved.RootElement.GetProperty("SchemaVersion").GetInt32());
    });

    [Fact]
    public void SearchCacheTracksActualMutableNumberFormattingAndDoesNotChangeItemEquality()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
            CultureInfo.CurrentCulture = culture;
            var journal = new HistoryJournal();
            journal.Record("Standard", "fraction", "-1.25");
            var item = Assert.Single(journal.Items);
            var equal = item with { };
            var hash = item.GetHashCode();
            Assert.Single(journal.Search("-1.25"));
            Assert.Empty(journal.Search("missing")); // populate localization cache
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NegativeSign = "minus:";
            Assert.Single(journal.Search("minus:1,25"));
            Assert.Empty(journal.Search("-1,25"));
            culture.NumberFormat.NegativeSign = "negative:";
            Assert.Single(journal.Search("negative:1,25"));
            Assert.Empty(journal.Search("minus:1,25"));
            Assert.Equal(equal, item);
            Assert.Equal(hash, item.GetHashCode());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void SearchCacheTracksPositiveExponentSignAndCultureSwitches()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            CultureInfo.CurrentCulture = culture;
            var journal = new HistoryJournal();
            journal.Record("Scientific", "large", "1.25E+50");
            Assert.Empty(journal.Search("missing"));
            culture.NumberFormat.PositiveSign = "plus:";
            Assert.Single(journal.Search("Eplus:50"));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            Assert.Single(journal.Search("1,25"));
            Assert.Empty(journal.Search("Eplus:50"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void SearchCacheIsBoundedAndInvalidatedForEveryHistoryMutation()
    {
        var journal = new HistoryJournal();
        for (var i = 0; i < 250; i++)
        {
            journal.Record("Standard", "value", i.ToString(CultureInfo.InvariantCulture));
            Assert.Empty(journal.Search("missing"));
        }
        var cache = (Dictionary<Guid, string>)typeof(HistoryJournal).GetField("localizedResults", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(journal)!;
        Assert.Equal(HistoryJournal.Capacity, cache.Count);
        var old = journal.Items[0];
        journal.Delete(old.Id);
        Assert.False(cache.ContainsKey(old.Id));
        var replacement = old with { Result = "1.5" };
        journal.Restore([replacement]);
        Assert.Empty(cache);
        Assert.Empty(journal.Search("missing"));
        Assert.Single(cache);
        journal.Clear();
        Assert.Empty(cache);
    }

    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterCommercialEngineering-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.Equal(temporaryRoot, Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("AsterCommercialEngineering-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, true);
        }
    }
}
