// Developer ::> Gehan Fernando
using ModernCalculator.Core.Calculations;

namespace ModernCalculator.Core.Memory;

public sealed record MemoryItem(Guid Id, BigDecimal ExactValue)
{
    public MemoryItem(Guid id, decimal value) : this(id, BigDecimal.FromDecimal(value)) { }
    public decimal Value => ExactValue.TryToDecimal(out var value) ? value : throw new CalculationException("Use ExactValue to access this memory value without losing precision.");
}

public sealed class MemoryBank
{
    public const int Capacity = 20;
    private readonly List<MemoryItem> items = [];
    public IReadOnlyList<MemoryItem> Items => items.AsReadOnly();
    public decimal Recall() => items.Count == 0 ? 0 : items[0].Value;
    public BigDecimal RecallExact() => items.Count == 0 ? BigDecimal.Zero : items[0].ExactValue;
    public void Store(decimal value) => Store(BigDecimal.FromDecimal(value));
    public void Store(BigDecimal value)
    {
        items.Insert(0, new(Guid.NewGuid(), value));
        if (items.Count > Capacity) items.RemoveAt(items.Count - 1);
    }
    public void Add(decimal value) => Add(BigDecimal.FromDecimal(value));
    // Calculate the replacement first so a failed addition leaves the stored slot intact.
    public void Add(BigDecimal value)
    {
        if (items.Count == 0) Store(value);
        else items[0] = items[0] with { ExactValue = items[0].ExactValue + value };
    }
    public void Remove(Guid id) => items.RemoveAll(item => item.Id == id);
    public void Clear() => items.Clear();
}
