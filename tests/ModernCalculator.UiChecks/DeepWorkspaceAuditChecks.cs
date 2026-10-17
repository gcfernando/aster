using ModernCalculator.Core.Conversions;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;

namespace ModernCalculator.UiChecks;

/// <summary>Session favorite state, enumeration failure, and notification contracts; no native windows are created.</summary>
public static class DeepWorkspaceAuditChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Deep workspace: favorite self restore preserves pairs and notifies once", SelfRestore);
        check("Deep workspace: lazy favorite query restores its selected pairs", LazyRestore);
        check("Deep workspace: failing favorite enumeration preserves existing state", FailedRestore);
        check("Deep workspace: null favorite source preserves existing state", NullSource);
        check("Deep workspace: malformed favorites are removed before deduplication and capacity", ValidateAndBound);
    }

    private static ConversionFavorite Pair(string from = "Metre", string to = "Kilometre") => new("Length", from, to);

    private static void SelfRestore()
    {
        var context = new WorkspaceContext();
        ConversionFavorite[] expected = [Pair(), Pair("Kilometre", "Metre")];
        context.RestoreConversionFavorites(expected);
        var notifications = 0;
        context.ConversionFavoritesChanged += () =>
        {
            notifications++;
            Require(context.ConversionFavorites.SequenceEqual(expected), "Observers saw partially restored favorite state.");
        };
        context.RestoreConversionFavorites(context.ConversionFavorites);
        Require(context.ConversionFavorites.SequenceEqual(expected) && notifications == 1,
            "Self restoration lost favorites or did not produce one completed-state notification.");
    }

    private static void LazyRestore()
    {
        var context = new WorkspaceContext();
        var expected = Pair("Kilometre", "Metre");
        context.RestoreConversionFavorites([Pair(), expected]);
        context.RestoreConversionFavorites(context.ConversionFavorites.Where(pair => pair.FromUnit == "Kilometre"));
        Require(context.ConversionFavorites.SequenceEqual([expected]), "A lazy query was cleared before it could restore its favorites.");
    }

    private static void FailedRestore()
    {
        var context = new WorkspaceContext();
        ConversionFavorite[] expected = [Pair(), Pair("Kilometre", "Metre")];
        context.RestoreConversionFavorites(expected);
        var notifications = 0;
        context.ConversionFavoritesChanged += () => notifications++;
        var threw = false;
        try { context.RestoreConversionFavorites(FailingPairs()); }
        catch (InvalidOperationException) { threw = true; }
        Require(threw && context.ConversionFavorites.SequenceEqual(expected) && notifications == 0,
            "Failed restoration changed existing favorites or notified persistence.");
    }

    private static IEnumerable<ConversionFavorite> FailingPairs()
    {
        yield return Pair("Metre", "Centimetre");
        throw new InvalidOperationException("Deliberate source enumeration failure.");
    }

    private static void NullSource()
    {
        var context = new WorkspaceContext();
        var expected = Pair();
        context.RestoreConversionFavorites([expected]);
        var notifications = 0;
        context.ConversionFavoritesChanged += () => notifications++;
        var threw = false;
        try { context.RestoreConversionFavorites(null!); }
        catch (ArgumentNullException) { threw = true; }
        Require(threw && context.ConversionFavorites.SequenceEqual([expected]) && notifications == 0,
            "A null source discarded session data or notified persistence.");
    }

    private static void ValidateAndBound()
    {
        var category = UnitConverter.Categories.Single(item => item.Name == "Length");
        var valid = category.Units.SelectMany(from => category.Units.Select(to =>
            new ConversionFavorite(category.Name, from.Name, to.Name))).Take(25).ToArray();
        ConversionFavorite[] invalid = [null!, new(null!, "Metre", "Kilometre"), new("Missing", "Metre", "Kilometre"),
            new("Length", "Metre", "Kilogram"), new("Length", null!, "Metre")];
        var input = Enumerable.Repeat(invalid, 5).SelectMany(items => items)
            .Concat(valid.SelectMany(pair => new[] { pair, pair }));
        var context = new WorkspaceContext();
        var notifications = 0;
        context.ConversionFavoritesChanged += () => notifications++;
        context.RestoreConversionFavorites(input);
        Require(context.ConversionFavorites.SequenceEqual(valid.Take(20)) && notifications == 1,
            "Malformed/duplicate favorites consumed valid capacity or changed stable order.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
