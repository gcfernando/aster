using ModernCalculator.Core.History;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class ReviewPersistenceRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedSavePreservesPreviousProfileBytes(bool unreadableProfile) => InDirectory(directory =>
    {
        var store = new StateStore(directory);
        var path = Path.Combine(directory, "state.json");
        if (unreadableProfile) File.WriteAllText(path, "{incomplete profile");
        else Assert.Null(store.Save(new AppSettings { PersistHistory = true, Precision = 8 },
            [new(Guid.NewGuid(), DateTimeOffset.UtcNow, "Standard", "1+2", "3")]));
        var original = File.ReadAllBytes(path);

        var warning = store.Save(new AppSettings { PersistHistory = true }, OversizedHistory());

        Assert.NotNull(warning);
        Assert.Contains("previous saved data was kept", warning);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(directory));
        if (!unreadableProfile)
        {
            var loaded = store.Load();
            Assert.Null(loaded.Warning);
            Assert.Equal(8, loaded.State.Settings.Precision);
            Assert.Equal("3", Assert.Single(loaded.State.History).Result);
        }
    });

    [Fact]
    public void OversizedFirstSaveDoesNotCreateAnUnreadableProfile() => InDirectory(directory =>
    {
        var store = new StateStore(directory);
        Assert.NotNull(store.Save(new AppSettings { PersistHistory = true }, OversizedHistory()));
        Assert.Empty(Directory.GetFiles(directory));
        Assert.Null(store.Load().Warning);
    });

    [Fact]
    public void HistoryOptOutStillRemovesPreviouslySavedHistory() => InDirectory(directory =>
    {
        var store = new StateStore(directory);
        Assert.Null(store.Save(new AppSettings { PersistHistory = true },
            [new(Guid.NewGuid(), DateTimeOffset.UtcNow, "Standard", "private calculation", "3")]));
        Assert.Null(store.Save(new AppSettings { PersistHistory = false }, OversizedHistory()));
        var loaded = store.Load();
        Assert.Null(loaded.Warning);
        Assert.Empty(loaded.State.History);
        Assert.DoesNotContain("private calculation", File.ReadAllText(Path.Combine(directory, "state.json")));
    });

    private static IEnumerable<HistoryItem> OversizedHistory() => Enumerable.Range(0, HistoryJournal.Capacity)
        // Unicode whitespace fits the scientific input limit, but JSON escapes expand its saved size.
        .Select(index => new HistoryItem(Guid.NewGuid(), DateTimeOffset.UtcNow, "Scientific",
            "1" + new string('\u2003', 2047), index.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterReviewPersistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.Equal(temporaryRoot, Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("AsterReviewPersistence-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, true);
        }
    }
}
