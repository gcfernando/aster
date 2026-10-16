// Developer ::> Gehan Fernando
using System.ComponentModel;
using System.Globalization;
using System.Text;
using ModernCalculator.Core.Conversions;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Controls;

public sealed record ConverterChoice(string Key, string Text, string SearchTerms, Action Activate, string? Symbol = null);

/// <summary>Always-visible search and flat choices. Search narrows directly; labelled navigation is a fallback.</summary>
public sealed class ConverterChoiceBrowser : UserControl, IThemedControl
{
    private readonly TableLayoutPanel layout = Ui.Table(1, 28, -100, 26);
    private readonly TableLayoutPanel matches = Ui.Table(1, -1, -1, -1);
    private readonly TextBox search;
    private readonly CalcButton[] buttons;
    private readonly CalcButton previous;
    private readonly CalcButton next;
    private readonly Label range = Ui.Label("", 8);
    private readonly ToolTip tooltips = new();
    private readonly string emptyMessage;
    private IReadOnlyList<ConverterChoice> entries = [];
    private ConverterChoice[] filtered = [];
    private int offset;
    private int visibleCount = 3;
    private string selectedKey = "";
    public event Action? FilterChanged;

    public ConverterChoiceBrowser(string name, string placeholder, string emptyMessage = "No matches")
    {
        this.emptyMessage = emptyMessage;
        AccessibleName = name;
        AccessibleRole = AccessibleRole.Grouping;
        AutoScaleMode = AutoScaleMode.None;
        Dock = DockStyle.Fill;
        Margin = new(2, 0, 2, 0);
        search = Ui.Input(name + " search");
        search.BorderStyle = BorderStyle.None;
        search.PlaceholderText = placeholder;
        search.Margin = new(3, 3, 3, 2);
        Ui.SetFont(search, 9);
        search.TextChanged += (_, _) => Filter();
        search.KeyDown += HandleSearchKeyDown;
        buttons = Enumerable.Range(0, 3).Select(slot =>
        {
            var button = Ui.Button("", () => { var index = offset + slot; if (index < filtered.Length) filtered[index].Activate(); }, ButtonTone.Quiet,
                name: name + " match " + (slot + 1));
            button.Margin = new(1);
            Ui.SetFont(button, 9);
            button.PreviewKeyDown += (_, e) => { if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown) e.IsInputKey = true; };
            button.KeyDown += (_, e) => HandleMatchKeyDown(slot, e);
            matches.Controls.Add(button, 0, slot);
            return button;
        }).ToArray();
        var navigation = Ui.Table(3, -100);
        navigation.ColumnStyles.Clear();
        navigation.ColumnStyles.Add(new(SizeType.Percent, 30));
        navigation.ColumnStyles.Add(new(SizeType.Percent, 40));
        navigation.ColumnStyles.Add(new(SizeType.Percent, 30));
        previous = Ui.Button("Back", () => MovePage(-1), ButtonTone.Quiet, name: name + " previous matches");
        next = Ui.Button("More", () => MovePage(1), ButtonTone.Quiet, name: name + " more matches");
        previous.Margin = next.Margin = new(1);
        Ui.SetFont(previous, 8); Ui.SetFont(next, 8);
        range.TextAlign = ContentAlignment.MiddleCenter;
        navigation.Controls.Add(previous, 0, 0); navigation.Controls.Add(range, 1, 0); navigation.Controls.Add(next, 2, 0);
        layout.Controls.Add(search, 0, 0); layout.Controls.Add(matches, 0, 1); layout.Controls.Add(navigation, 0, 2);
        Controls.Add(layout);
    }

    private void HandleSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && search.TextLength > 0)
        { search.Clear(); e.Handled = e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Enter && filtered.Length > 0)
        { filtered[offset].Activate(); e.Handled = e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Down && filtered.Length > 0)
        { buttons[0].Focus(); e.Handled = e.SuppressKeyPress = true; }
    }

    private void HandleMatchKeyDown(int slot, KeyEventArgs e)
    {
        var index = e.KeyCode switch
        {
            Keys.Up => Math.Max(0, offset + slot - 1), Keys.Down => Math.Min(filtered.Length - 1, offset + slot + 1),
            Keys.Home => 0, Keys.End => filtered.Length - 1,
            Keys.PageUp => Math.Max(0, offset + slot - visibleCount), Keys.PageDown => Math.Min(filtered.Length - 1, offset + slot + visibleCount), _ => -1
        };
        if (index < 0) return;
        offset = index / visibleCount * visibleCount;
        RenderChoices(); buttons[index - offset].Focus();
        e.Handled = e.SuppressKeyPress = true;
    }

    public void SetEntries(IEnumerable<ConverterChoice> values, bool clearSearch = false)
    {
        entries = values.ToArray();
        if (clearSearch && search.TextLength > 0) search.Clear();
        Filter();
    }

    public void MarkSelected(string key) { selectedKey = key; RenderChoices(); }
    public void Reveal(string key)
    {
        search.Clear();
        Filter();
        var index = Array.FindIndex(filtered, entry => entry.Key == key);
        if (index >= 0) offset = index / visibleCount * visibleCount;
        RenderChoices();
    }
    public void ClearSearch() { search.Clear(); Filter(); }
    public void FocusSearch() { search.Focus(); search.SelectAll(); }

    public void AdaptCompact(bool compact)
    {
        visibleCount = compact ? 1 : 3;
        layout.RowStyles[0].Height = (compact ? 22 : 28) * DeviceDpi / 96f;
        layout.RowStyles[2].Height = (compact ? 22 : 26) * DeviceDpi / 96f;
        Ui.SetFont(search, compact ? 8 : 9);
        for (var index = 0; index < buttons.Length; index++)
        {
            matches.RowStyles[index].Height = index < visibleCount ? 100f / visibleCount : 0;
            Ui.SetFont(buttons[index], compact ? 8 : 9);
        }
        offset = 0;
        RenderChoices();
    }

    // Rank exact symbol matches ahead of names while requiring every search token.
    private void Filter()
    {
        var query = UnitSearch.Normalize(search.Text);
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        filtered = entries.Where(entry => tokens.All(token => UnitSearch.Normalize(entry.SearchTerms).Contains(token, StringComparison.Ordinal)))
            .OrderBy(entry => query.Length == 0 ? 2 : entry.Symbol is not null && UnitSearch.Normalize(entry.Symbol) == query ? 0
                : UnitSearch.Normalize(entry.Key) == query ? 1 : 2).ToArray();
        offset = 0;
        RenderChoices();
        FilterChanged?.Invoke();
    }

    private void MovePage(int direction)
    {
        offset = Math.Clamp(offset + direction * visibleCount, 0, Math.Max(0, (filtered.Length - 1) / visibleCount * visibleCount));
        RenderChoices();
    }

    private void RenderChoices()
    {
        for (var slot = 0; slot < buttons.Length; slot++)
        {
            var index = offset + slot;
            var available = slot < visibleCount && index < filtered.Length;
            var button = buttons[slot];
            button.Visible = button.Enabled = available;
            button.Text = available ? filtered[index].Text : "";
            button.AccessibleName = available ? AccessibleName + ": " + filtered[index].Text : AccessibleName + " unused match";
            button.AccessibleDescription = available && filtered[index].Key == selectedKey ? "Selected choice" : "Activate this choice";
            button.Selected = available && filtered[index].Key == selectedKey;
            tooltips.SetToolTip(button, button.Text);
            button.Invalidate();
        }
        range.Text = filtered.Length == 0 ? entries.Count == 0 && search.TextLength == 0 ? emptyMessage : "No matches"
            : $"{offset + 1}–{Math.Min(offset + visibleCount, filtered.Length)} of {filtered.Length}";
        range.AccessibleName = AccessibleName + " " + range.Text;
        previous.Enabled = offset > 0;
        next.Enabled = offset + visibleCount < filtered.Length;
        AccessibleDescription = "Search by name, symbol, or alias. " + range.Text + ". Back and More browse remaining matches without scrolling.";
        Ui.AttachTooltips(this);
    }

    public void ApplyTheme(Palette palette) { BackColor = palette.Surface; range.ForeColor = palette.Muted; }
    protected override void Dispose(bool disposing) { if (disposing) tooltips.Dispose(); base.Dispose(disposing); }
}

public sealed class SearchableUnitSelector : UserControl
{
    private readonly ConverterChoiceBrowser browser;
    private IReadOnlyList<UnitDefinition> items = [];
    private int selectedIndex = -1;
    public IReadOnlyList<UnitDefinition> Items => items;
    public UnitDefinition? SelectedItem => selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
    public event EventHandler? SelectedIndexChanged;
    [DefaultValue(-1)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < 0 || value >= items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            var changed = selectedIndex != value;
            selectedIndex = value;
            browser.MarkSelected(items[value].Name);
            AccessibleDescription = "Selected unit: " + items[value];
            if (changed) SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SearchableUnitSelector(string name)
    {
        AccessibleName = name;
        AccessibleRole = AccessibleRole.Grouping;
        AutoScaleMode = AutoScaleMode.None;
        Dock = DockStyle.Fill; Margin = Padding.Empty;
        browser = new ConverterChoiceBrowser(name + " choices", "Search name, symbol or alias");
        Controls.Add(browser);
    }

    public void SetUnits(IReadOnlyList<UnitDefinition> units, int index)
    {
        items = units;
        selectedIndex = -1;
        browser.SetEntries(units.Select((unit, unitIndex) => new ConverterChoice(unit.Name, unit.ToString(), UnitSearch.Terms(unit), () => SelectedIndex = unitIndex, unit.Symbol)), true);
        SelectedIndex = index;
    }
    public void AdaptCompact(bool compact) => browser.AdaptCompact(compact);
    public void RevealSelection() { if (SelectedItem is not null) browser.Reveal(SelectedItem.Name); }
}

/// <summary>English catalog names, symbols, spelling variants, plurals, and unambiguous conventional aliases.</summary>
public static class UnitSearch
{
    public static string Normalize(string text)
    {
        var expanded = text.Replace("²", "2", StringComparison.Ordinal).Replace("³", "3", StringComparison.Ordinal)
            .Replace("°", "", StringComparison.Ordinal).ToLowerInvariant()
            .Replace("meter", "metre", StringComparison.Ordinal).Replace("liter", "litre", StringComparison.Ordinal);
        var result = new StringBuilder();
        foreach (var character in expanded.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(char.IsLetterOrDigit(character) ? character : ' ');
        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Terms(UnitDefinition unit)
    {
        var aliases = unit.Symbol switch
        {
            "ft" => "feet foot", "in" => "inches inch", "kg" => "kilograms kilos kilogramme kilogrammes", "lb" => "lbs pounds", "oz" => "ounces",
            "nmi" => "nautical miles nauticalmile", "t" => "metric ton tonnes tons",
            "°C" => "celsius centigrade degc degrees c", "°F" => "fahrenheit degf degrees f", "K" => "kelvin kelvins",
            "m²" => "square meters square metres sqm m^2", "m³" => "cubic meters cubic metres cum m^3",
            "ft²" => "square feet sqft", "in²" => "square inches sqin",
            "US gal" => "us gallons american gallons", "imp gal" => "imperial gallons british gallons uk gallons",
            "US fl oz" => "us fluid ounces american fluid ounces", "US cup" => "us cups american cups",
            "s" => "sec secs seconds", "min" => "mins minutes", "h" => "hr hrs hours", "wk" => "weeks", "a" => "julian years yr yrs 365.25 days",
            "km/h" => "kph kmph kilometers per hour kilometres per hour", "m/s" => "meters per second metres per second mps",
            "mph" => "miles per hour", "ft/s" => "feet per second fps", "kn" => "knots kts",
            "B" => "bytes octets", "bit" => "bits binary digits", "kB" => "kilobytes kb decimal", "MB" => "megabytes decimal",
            "GB" => "gigabytes decimal", "TB" => "terabytes decimal", "KiB" => "kibibytes binary", "MiB" => "mebibytes binary", "GiB" => "gibibytes binary", "TiB" => "tebibytes binary",
            "cal" => "thermochemical calories", "kcal" => "kilocalories food calories", "Wh" => "watt hours", "kWh" => "kilowatt hours",
            "BTU" => "british thermal unit international table", "hp" => "mechanical horsepower imperial horsepower", "PS" => "metric horsepower pferdestarke",
            "Pa" => "pascals", "atm" => "atmospheres", "psi" => "pounds per square inch", "mmHg" => "millimeters millimetres of mercury",
            "°" => "degrees deg", "rad" => "radians", "gon" => "gradian gradians grads grad", "turn" => "turns revolutions rev",
            _ => ""
        };
        return unit.Name + " " + unit.Name + "s " + unit.Symbol + " " + aliases;
    }
}
