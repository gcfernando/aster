// Developer ::> Gehan Fernando
using ModernCalculator.Core.History;
using ModernCalculator.Core.Memory;
using ModernCalculator.Core.Conversions;
using ModernCalculator.Infrastructure.Settings;

namespace ModernCalculator.WinForms;

public sealed class WorkspaceContext
{
    public AppSettings Settings { get; set; } = new();
    public MemoryBank Memory { get; } = new();
    public HistoryJournal History { get; } = new();
    public event Action? JournalChanged;
    public event Action? MemoryChanged;
    public event Action<string>? StatusChanged;
    public event Action? ShowMemoryRequested;
    public event Action? ShowHistoryRequested;
    public event Action? ConversionFavoritesChanged;
    private readonly List<ConversionFavorite> favorites = [];
    public IReadOnlyList<ConversionFavorite> ConversionFavorites => favorites.AsReadOnly();
    public void RestoreConversionFavorites(IEnumerable<ConversionFavorite> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        // Restore can enumerate this collection, a lazy query, or a failing source.
        // Finish validating before replacing session data or notifying persistence.
        var replacement = pairs.Where(pair => pair is not null && UnitConverter.Categories.Any(category =>
            category.Name == pair.Category && category.Units.Any(unit => unit.Name == pair.FromUnit) &&
            category.Units.Any(unit => unit.Name == pair.ToUnit))).Distinct().Take(20).ToArray();
        favorites.Clear();
        favorites.AddRange(replacement);
        ConversionFavoritesChanged?.Invoke();
    }
    public void ToggleConversionFavorite(string category, string fromUnit, string toUnit)
    {
        // Identify a favorite by its category and ordered source/destination pair.
        var pair = new ConversionFavorite(category, fromUnit, toUnit);
        if (!favorites.Remove(pair)) { favorites.Insert(0, pair); if (favorites.Count > 20) favorites.RemoveAt(20); }
        ConversionFavoritesChanged?.Invoke();
    }
    public void Record(string mode, string expression, string result)
    {
        History.Record(mode, expression, result); JournalChanged?.Invoke();
    }
    public void NotifyHistory() => JournalChanged?.Invoke();
    public void NotifyMemory() => MemoryChanged?.Invoke();
    public void Status(string text) => StatusChanged?.Invoke(text);
    public void ShowMemory() => ShowMemoryRequested?.Invoke();
    public void ShowHistory() => ShowHistoryRequested?.Invoke();
}
