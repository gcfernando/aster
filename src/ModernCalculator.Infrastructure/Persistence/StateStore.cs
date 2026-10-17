// Developer ::> Gehan Fernando
using System.Text.Json;
using System.Text;
using ModernCalculator.Core.History;
using ModernCalculator.Core.Conversions;
using ModernCalculator.Infrastructure.Settings;

namespace ModernCalculator.Infrastructure.Persistence;

public sealed record SavedState(AppSettings Settings, IReadOnlyList<HistoryItem> History, IReadOnlyList<ConversionFavorite>? ConversionFavorites = null, int SchemaVersion = 1);
public sealed record LoadResult(SavedState State, string? Warning);

public sealed class StateStore(string directory)
{
    private const int MaximumFileBytes = 2 * 1024 * 1024;
    private const int CurrentSchemaVersion = 1;
    private const string UnsupportedSchemaWarning = "Saved preferences use an unsupported format and were kept. Use a compatible Aster version or choose another data directory. Changes apply only for this session.";
    private readonly string filePath = Path.Combine(directory, "state.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, MaxDepth = 16 };

    public LoadResult Load()
    {
        try
        {
            using var document = ReadProfile();
            if (document is null) return Defaults("Saved preferences were too large; defaults are in use.");
            if (!SupportedSchema(document.RootElement)) return Defaults(UnsupportedSchemaWarning);
            var state = document.RootElement.Deserialize<SavedState>(Options);
            if (state?.Settings is null || state.History is null) return Defaults("Saved preferences were incomplete; defaults are in use.");
            var settings = state.Settings.Validated();
            var journal = new HistoryJournal();
            // Restore saved history only when validated preferences enable persistence.
            if (settings.PersistHistory) journal.Restore(state.History);
            return new(new(settings, journal.Items.ToArray(), ValidFavorites(state.ConversionFavorites)), null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Defaults();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return Defaults("Saved preferences could not be read; defaults are in use.");
        }
    }

    private JsonDocument? ReadProfile()
    {
        var text = ReadProfileText();
        return text is null ? null : JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = Options.MaxDepth });
    }

    private string? ReadProfileText()
    {
        // Keep one snapshot open. Allow atomic replacement, but not in-place writes.
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var bytes = ReadBounded(stream);
        if (bytes is null) return null;
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static bool SupportedSchema(JsonElement root) => root.ValueKind != JsonValueKind.Object ||
        !root.TryGetProperty(nameof(SavedState.SchemaVersion), out var version) ||
        version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number == CurrentSchemaVersion;

    private bool ExistingSchemaIsUnsupported()
    {
        var unsupported = false;
        var tooDeep = false;
        try
        {
            var text = ReadProfileText();
            // An oversized profile cannot be inspected safely; do not overwrite unknown data.
            if (text is null) return true;
            // Iterative token inspection can identify future metadata even when its payload
            // exceeds our deserialization depth. Input bytes remain bounded; no deep DOM is built.
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), new JsonReaderOptions { MaxDepth = int.MaxValue });
            var rootObject = false;
            var firstToken = true;
            while (reader.Read())
            {
                if (firstToken) { rootObject = reader.TokenType == JsonTokenType.StartObject; firstToken = false; }
                var nesting = reader.CurrentDepth + (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray ? 1 : 0);
                tooDeep |= nesting > Options.MaxDepth;
                if (rootObject && reader.CurrentDepth == 1 && reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(nameof(SavedState.SchemaVersion)))
                {
                    if (!reader.Read()) return true;
                    unsupported = reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var version) || version != CurrentSchemaVersion;
                }
            }
            return unsupported || tooDeep;
        }
        // Missing and corrupt profiles retain the documented first-run/recovery behavior.
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
        catch (JsonException) { return unsupported || tooDeep; }
    }

    private static byte[]? ReadBounded(Stream stream)
    {
        if (stream.CanSeek && stream.Length > MaximumFileBytes) return null;
        using var contents = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            // Read one extra byte to detect growth or streams whose length is unavailable.
            var count = stream.Read(buffer, 0, Math.Min(buffer.Length, MaximumFileBytes - (int)contents.Length + 1));
            if (count == 0) return contents.ToArray();
            if (contents.Length + count > MaximumFileBytes) return null;
            contents.Write(buffer, 0, count);
        }
    }

    public string? Save(AppSettings settings, IEnumerable<HistoryItem> history, IEnumerable<ConversionFavorite>? conversionFavorites = null)
    {
        string? temporary = null;
        try
        {
            // Inspect the current file even when Save is called without an earlier Load,
            // or a newer application replaced the profile since this instance started.
            if (ExistingSchemaIsUnsupported()) return UnsupportedSchemaWarning;
            var state = new SavedState(settings.Validated(), settings.PersistHistory ? history.Take(HistoryJournal.Capacity).ToArray() : [], ValidFavorites(conversionFavorites));
            var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options);
            // Never replace a readable profile with a file that Load would reject.
            if (bytes.Length > MaximumFileBytes)
                return "Preferences and history exceed the saved-data limit. Clear some history and retry; the previous saved data was kept.";
            Directory.CreateDirectory(directory);
            // Write beside the state file so replacement stays on the same filesystem.
            temporary = Path.Combine(directory, $"state.{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(temporary, bytes);
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
