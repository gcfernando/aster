// Developer ::> Gehan Fernando
using System.Text.Json;
using ModernCalculator.Core.History;
using ModernCalculator.Core.Conversions;
using ModernCalculator.Infrastructure.Settings;

namespace ModernCalculator.Infrastructure.Persistence;

public sealed record SavedState(AppSettings Settings, IReadOnlyList<HistoryItem> History, IReadOnlyList<ConversionFavorite>? ConversionFavorites = null);
public sealed record LoadResult(SavedState State, string? Warning);

public sealed class StateStore(string directory)
{
    private const int MaximumFileBytes = 2 * 1024 * 1024;
    private readonly string filePath = Path.Combine(directory, "state.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, MaxDepth = 16 };

    public LoadResult Load()
    {
        try
        {
            if (!File.Exists(filePath)) return Defaults();
            if (new FileInfo(filePath).Length > MaximumFileBytes) return Defaults("Saved preferences were too large; defaults are in use.");
            var state = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(filePath), Options);
            if (state?.Settings is null || state.History is null) return Defaults("Saved preferences were incomplete; defaults are in use.");
            var settings = state.Settings.Validated();
            var journal = new HistoryJournal();
            // Restore saved history only when validated preferences enable persistence.
            if (settings.PersistHistory) journal.Restore(state.History);
            return new(new(settings, journal.Items.ToArray(), ValidFavorites(state.ConversionFavorites)), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return Defaults("Saved preferences could not be read; defaults are in use.");
        }
    }

    public string? Save(AppSettings settings, IEnumerable<HistoryItem> history, IEnumerable<ConversionFavorite>? conversionFavorites = null)
    {
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(directory);
            var state = new SavedState(settings.Validated(), settings.PersistHistory ? history.Take(HistoryJournal.Capacity).ToArray() : [], ValidFavorites(conversionFavorites));
            // Write beside the state file so replacement stays on the same filesystem.
            temporary = Path.Combine(directory, $"state.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, Options));
            File.Move(temporary, filePath, true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return "Preferences could not be saved. This session will continue normally.";
        }
        finally
        {
            if (temporary is not null)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* A future save can still succeed. */ }
            }
        }
    }

    private static ConversionFavorite[] ValidFavorites(IEnumerable<ConversionFavorite>? pairs) => (pairs ?? [])
        .Where(pair => pair is not null && UnitConverter.Categories.Any(category => category.Name == pair.Category &&
            category.Units.Any(unit => unit.Name == pair.FromUnit) && category.Units.Any(unit => unit.Name == pair.ToUnit)))
        .Distinct().Take(20).ToArray();

    private static LoadResult Defaults(string? warning = null) => new(new(new(), []), warning);
}
