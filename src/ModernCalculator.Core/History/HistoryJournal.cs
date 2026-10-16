// Developer ::> Gehan Fernando
using System.Globalization;
using ModernCalculator.Core.Calculations;

namespace ModernCalculator.Core.History;

public sealed record HistoryItem(Guid Id, DateTimeOffset Timestamp, string Mode, string Expression, string Result)
{
    public string LocalizedResult(CultureInfo culture)
    {
        if (Mode == "Dates") return Result;
        if (Mode is "Standard" or "Programmer" && BigDecimal.TryParse(Result, out var number))
            return number.ToString(culture);
        return double.TryParse(Result, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real)
            ? real.ToString("G17", culture) : Result;
    }
}

public sealed class HistoryJournal
{
    public const int Capacity = 200;
    private readonly List<HistoryItem> items = [];
    public IReadOnlyList<HistoryItem> Items => items.AsReadOnly();
    public void Record(string mode, string expression, string result)
    {
        items.Insert(0, new(Guid.NewGuid(), DateTimeOffset.Now, mode, expression, result));
        if (items.Count > Capacity) items.RemoveAt(items.Count - 1);
    }
    public IEnumerable<HistoryItem> Search(string query) => items.Where(item =>
        item.Expression.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        item.Result.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        item.LocalizedResult(CultureInfo.CurrentCulture).Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        item.Mode.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    // Discard malformed or duplicate saved entries before applying the journal limit.
    public void Restore(IEnumerable<HistoryItem> saved)
    {
        items.Clear();
        items.AddRange(saved.Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Expression) &&
            !string.IsNullOrWhiteSpace(item.Result) && item.Expression.Length <= 4096 && item.Result.Length <= 4096 &&
            !string.IsNullOrWhiteSpace(item.Mode)).DistinctBy(item => item.Id).Take(Capacity));
    }
    public void Delete(Guid id) => items.RemoveAll(item => item.Id == id);
    public void Clear() => items.Clear();
}
