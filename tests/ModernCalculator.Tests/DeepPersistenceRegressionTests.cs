using System.Text;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class DeepPersistenceRegressionTests
{
    private const int MaximumFileBytes = 2 * 1024 * 1024;
    private const string Profile = """
        {"Settings":{"Precision":8,"PersistHistory":true,"Mode":"Scientific"},"History":[{"Id":"11111111-1111-1111-1111-111111111111","Timestamp":"2026-10-17T12:00:00+00:00","Mode":"Scientific","Expression":"π × 2","Result":"6.28"}]}
        """;

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public void BoundedLoadRetainsExistingBomAndUnicodeSupport(string encodingName) => InDirectory(directory =>
    {
        Encoding encoding = encodingName switch
        {
            "utf8" => new UTF8Encoding(false),
            "utf8-bom" => new UTF8Encoding(true),
            "utf16-le" => new UnicodeEncoding(false, true),
            "utf16-be" => new UnicodeEncoding(true, true),
            "utf32-le" => new UTF32Encoding(false, true),
            "utf32-be" => new UTF32Encoding(true, true),
            _ => throw new ArgumentOutOfRangeException(nameof(encodingName))
        };
        var path = Path.Combine(directory, "state.json");
        File.WriteAllText(path, Profile, encoding);
        var original = File.ReadAllBytes(path);

        var loaded = new StateStore(directory).Load();

        Assert.Null(loaded.Warning);
        Assert.Equal(8, loaded.State.Settings.Precision);
        Assert.Equal("Scientific", loaded.State.Settings.Mode);
        Assert.Equal("π × 2", Assert.Single(loaded.State.History).Expression);
        Assert.Equal(original, File.ReadAllBytes(path));
    });

    [Fact]
    public void NonFileProfilePathProducesAReadWarning() => InDirectory(directory =>
    {
        var profileDirectory = Path.Combine(directory, "state.json");
        Directory.CreateDirectory(profileDirectory);
        var loaded = new StateStore(directory).Load();
        Assert.NotNull(loaded.Warning);
        Assert.Equal(new AppSettings(), loaded.State.Settings);
        Assert.Empty(loaded.State.History);
        Assert.True(Directory.Exists(profileDirectory));
    });

    [Fact]
    public void LockedProfileProducesAReadWarningWithoutChangingItsBytes() => InDirectory(directory =>
    {
        var path = Path.Combine(directory, "state.json");
        File.WriteAllText(path, Profile);
        var original = File.ReadAllBytes(path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var loaded = new StateStore(directory).Load();
            Assert.NotNull(loaded.Warning);
            Assert.Equal(new AppSettings(), loaded.State.Settings);
        }
        Assert.Equal(original, File.ReadAllBytes(path));
    });

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ReadLimitCountsActualUtf8BytesIncludingWhitespace(int extraBytes, bool accepted) => InDirectory(directory =>
    {
        var prefix = Encoding.UTF8.GetBytes(Profile);
        var bytes = new byte[MaximumFileBytes + extraBytes];
        prefix.CopyTo(bytes, 0);
        Array.Fill(bytes, (byte)' ', prefix.Length, bytes.Length - prefix.Length);
        var path = Path.Combine(directory, "state.json");
        File.WriteAllBytes(path, bytes);

        var loaded = new StateStore(directory).Load();

        Assert.Equal(accepted, loaded.Warning is null);
        if (accepted) Assert.Equal(8, loaded.State.Settings.Precision);
        else Assert.Contains("too large", loaded.Warning!);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    });

    [Fact]
    public void MissingFileAndMissingParentRemainCleanFirstRunDefaults() => InDirectory(directory =>
    {
        Assert.Null(new StateStore(directory).Load().Warning);
        Assert.Null(new StateStore(Path.Combine(directory, "not-created")).Load().Warning);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    });

    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterDeepPersistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.Equal(temporaryRoot, Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("AsterDeepPersistence-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, true);
        }
    }
}
