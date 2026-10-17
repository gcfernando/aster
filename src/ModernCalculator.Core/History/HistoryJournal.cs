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
    private readonly Dictionary<Guid, string> localizedResults = [];
    private FormattingKey? cachedFormatting;
    public IReadOnlyList<HistoryItem> Items => items.AsReadOnly();
    public void Record(string mode, string expression, string result)
    {
        items.Insert(0, new(Guid.NewGuid(), DateTimeOffset.Now, mode, expression, result));
        if (items.Count > Capacity)
        {
            localizedResults.Remove(items[^1].Id);
            items.RemoveAt(items.Count - 1);
        }
    }
    public IEnumerable<HistoryItem> Search(string query) => items.Where(item =>
        item.Expression.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        item.Result.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        item.Mode.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        LocalizedResult(item).Contains(query, StringComparison.CurrentCultureIgnoreCase));
    private string LocalizedResult(HistoryItem item)
    {
        var culture = CultureInfo.CurrentCulture;
        var format = culture.NumberFormat;
        var key = new FormattingKey(format.NumberDecimalSeparator, format.NegativeSign, format.PositiveSign);
        if (cachedFormatting != key) { localizedResults.Clear(); cachedFormatting = key; }
        if (!localizedResults.TryGetValue(item.Id, out var result))
            localizedResults.Add(item.Id, result = item.LocalizedResult(culture));
        return result;
    }
    // Keep presentation caching outside the immutable record's equality/hash state.
    private readonly record struct FormattingKey(string DecimalSeparator, string NegativeSign, string PositiveSign);
    // Discard malformed or duplicate saved entries before applying the journal limit.
    public void Restore(IEnumerable<HistoryItem> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        // Enumerate before mutating: the source can be this journal or a query
        // over it, and a failed enumeration must leave current history intact.
        var replacement = saved.Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Expression) &&
            !string.IsNullOrWhiteSpace(item.Result) && item.Expression.Length <= 4096 && item.Result.Length <= 4096 &&
            !string.IsNullOrWhiteSpace(item.Mode)).DistinctBy(item => item.Id).Take(Capacity).ToArray();
        items.Clear();
        items.AddRange(replacement);
        localizedResults.Clear();
    }
    public void Delete(Guid id) { items.RemoveAll(item => item.Id == id); localizedResults.Remove(id); }
    public void Clear() { items.Clear(); localizedResults.Clear(); }
}
