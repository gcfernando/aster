// Developer ::> Gehan Fernando
using ModernCalculator.Core.History;
using ModernCalculator.Core.Memory;
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
    { favorites.Clear(); favorites.AddRange(pairs.Distinct().Take(20)); ConversionFavoritesChanged?.Invoke(); }
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
