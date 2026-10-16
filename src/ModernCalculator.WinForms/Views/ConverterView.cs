// Developer ::> Gehan Fernando
using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Conversions;
using ModernCalculator.Core.History;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms.Controls;

namespace ModernCalculator.WinForms.Views;

public sealed class ConverterView : ModeView
{
    private readonly SearchableUnitSelector from = new("Source unit");
    private readonly SearchableUnitSelector to = new("Destination unit");
    private readonly Label fromCaption = Ui.Label("From", 9, true);
    private readonly Label toCaption = Ui.Label("To", 9, true);
    private readonly TextBox input = Ui.Input("Value to convert", "1");
    private readonly ResultDisplay display = new();
    private readonly Label explanation = Ui.Label("", 9);
    private readonly TableLayoutPanel layout = Ui.Table(1, -14, -9, -57, -10, -10);
    private readonly TableLayoutPanel pairs = Ui.Table(2, -100);
    private readonly Panel content = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly SegmentedControl collection = new("Converter workspace", ["Convert", "Favorites", "Recent"]);
    private readonly ConverterChoiceBrowser favorites = new("Favorite conversion pairs", "Search saved category or unit pair", "Use Save pair to add a favorite");
    private readonly ConverterChoiceBrowser recent = new("Recent conversions", "Search recent conversions", "Use Save conversion or Enter");
    private readonly CalcButton[] categoryButtons;
    private readonly CalcButton swap;
    private readonly CalcButton savePair;
    private readonly CalcButton saveConversion;
    private readonly CalcButton history;
    private readonly ToolTip tooltips = new();
    private bool? compactLayout;
    private bool updating;
    private int categoryIndex;
    private string copy = "";
    private string conversionContext = "";
    private string? inputError;

    public ConverterView(WorkspaceContext context) : base(context)
    {
        AccessibleName = "Unit converter";
        AutoScroll = false;
        layout.Padding = new(2);
        var categories = Ui.Table(6, -50, -50);
        categories.AccessibleName = "Conversion categories";
        categoryButtons = UnitConverter.Categories.Select((category, index) =>
        {
            var button = Ui.Button(category.Name, () => SelectCategory(index), ButtonTone.Quiet);
            button.Margin = new(1);
            Ui.SetFont(button, 9);
            categories.Controls.Add(button, index % 6, index / 6);
            return button;
        }).ToArray();
        layout.Controls.Add(categories, 0, 0);
        collection.Margin = Padding.Empty;
        collection.Padding = new(1);
        layout.Controls.Add(collection, 0, 1);

        var sourcePanel = Ui.Table(1, -15, -30, -55);
        var targetPanel = Ui.Table(1, -15, -30, -55);
        fromCaption.AutoEllipsis = toCaption.AutoEllipsis = true;
        input.BorderStyle = BorderStyle.None;
        input.Margin = new(5, 3, 5, 2);
        sourcePanel.Controls.Add(fromCaption, 0, 0);
        sourcePanel.Controls.Add(input, 0, 1);
        sourcePanel.Controls.Add(from, 0, 2);
        targetPanel.Controls.Add(toCaption, 0, 0);
        targetPanel.Controls.Add(display, 0, 1);
        targetPanel.Controls.Add(to, 0, 2);
        pairs.Controls.Add(sourcePanel, 0, 0);
        pairs.Controls.Add(targetPanel, 1, 0);
        content.Controls.Add(pairs);
        layout.Controls.Add(content, 0, 2);

        var actions = Ui.Table(4, -100);
        swap = Ui.Button("Swap units", Swap, ButtonTone.Quiet, name: "Swap units");
        savePair = Ui.Button("Save pair", ToggleFavorite, ButtonTone.Operator, name: "Save current unit pair to Favorites");
        saveConversion = Ui.Button("Save conversion", () => Convert(true), ButtonTone.Operator, name: "Save this conversion to history");
        history = Ui.Button("History", () => { Context.ShowHistory(); Context.Status("Conversion entries are in History. Select an entry to reuse its result."); },
            ButtonTone.Quiet, name: "Open conversion history");
        actions.Controls.Add(swap, 0, 0); actions.Controls.Add(savePair, 1, 0);
        actions.Controls.Add(saveConversion, 2, 0); actions.Controls.Add(history, 3, 0);
        layout.Controls.Add(actions, 0, 3);
        explanation.AccessibleName = "Conversion precision and factor";
        layout.Controls.Add(explanation, 0, 4);
        Controls.Add(layout);

        tooltips.SetToolTip(swap, "Exchange the source and target units. The entered value stays the same.");
        tooltips.SetToolTip(savePair, "Save or remove this unit pair in Favorites. Favorites are stored on this device.");
        tooltips.SetToolTip(saveConversion, "Add this value and conversion to Recent and History. Enter in the From value does the same.");
        tooltips.SetToolTip(history, "Open the shared History panel to browse, reuse, copy, or delete saved calculations.");
        tooltips.SetToolTip(input, "Enter a regional decimal value or scientific notation. Grouping separators are not accepted.");
        from.SelectedIndexChanged += (_, _) => Convert(false);
        to.SelectedIndexChanged += (_, _) => Convert(false);
        input.TextChanged += (_, _) => Convert(false);
        collection.SelectedIndexChanged += (_, _) => ShowCollection();
        Context.ConversionFavoritesChanged += RefreshFavorites;
        Context.JournalChanged += RefreshRecent;
        SizeChanged += (_, _) => AdaptCompactLayout();
        SelectCategory(0);
        RefreshFavorites(); RefreshRecent();
        FinishLayout();
        AdaptCompactLayout();
    }

    private UnitCategory Category => UnitConverter.Categories[categoryIndex];
    private string PairKey(string category, string source, string target) => category + "|" + source + "|" + target;

    private void AdaptCompactLayout()
    {
        var compact = Context.Settings.Density == DensityPreference.Compact || ClientSize.Height * 96f / DeviceDpi < 360;
        if (compactLayout == compact) return;
        compactLayout = compact;
        var weights = compact ? new float[] { 18, 9, 53, 11, 9 } : [14, 9, 57, 10, 10];
        for (var index = 0; index < weights.Length; index++) layout.RowStyles[index].Height = weights[index];
        foreach (var button in categoryButtons) Ui.SetFont(button, compact ? 8 : 9);
        SetSegmentFonts(collection, compact ? 8 : 10);
        Ui.SetFont(input, compact ? 11 : 22);
        input.Margin = new(4, 2 * DeviceDpi / 96, 4, 2 * DeviceDpi / 96);
        foreach (var caption in new[] { fromCaption, toCaption }) Ui.SetFont(caption, compact ? 8 : 9, FontStyle.Bold);
        foreach (var button in new[] { swap, savePair, saveConversion, history })
        {
            Ui.SetFont(button, compact ? 8 : 9);
            button.Margin = new(1 * DeviceDpi / 96);
        }
        Ui.SetFont(explanation, compact ? 8 : 9);
        from.AdaptCompact(compact); to.AdaptCompact(compact);
        favorites.AdaptCompact(compact); recent.AdaptCompact(compact);
        layout.PerformLayout();
        UpdateExplanation();
    }

    private void SelectCategory(int index)
    {
        categoryIndex = index;
        for (var i = 0; i < categoryButtons.Length; i++)
        {
            categoryButtons[i].Selected = i == index;
            categoryButtons[i].AccessibleDescription = i == index ? "Selected conversion category" : "Conversion category";
            categoryButtons[i].Invalidate();
        }
        updating = true;
        try
        {
            from.SetUnits(Category.Units, 0);
            to.SetUnits(Category.Units, Math.Min(1, Category.Units.Count - 1));
        }
        finally { updating = false; }
        collection.SelectedIndex = 0;
        Convert(false);
    }

    private void Swap()
    {
        updating = true;
        try { (from.SelectedIndex, to.SelectedIndex) = (to.SelectedIndex, from.SelectedIndex); }
        finally { updating = false; }
        from.RevealSelection(); to.RevealSelection();
        collection.SelectedIndex = 0;
        Convert(false);
        Context.Status("Units swapped. The entered value is unchanged.");
    }

    // Refresh live results freely; only explicit saves append a history entry.
    private void Convert(bool record)
    {
        if (updating || from.SelectedItem is not UnitDefinition source || to.SelectedItem is not UnitDefinition target) return;
        fromCaption.Text = "From · " + source;
        toCaption.Text = "To · " + target;
        fromCaption.AccessibleName = "From unit: " + source;
        toCaption.AccessibleName = "To unit: " + target;
        tooltips.SetToolTip(fromCaption, fromCaption.Text); tooltips.SetToolTip(toCaption, toCaption.Text);
        try
        {
            if (!NumberText.TryDouble(input.Text, CultureInfo.CurrentCulture, out var value, allowGrouping: false))
                throw new CalculationException("Enter a number in floating-point range without grouping separators.");
            var result = UnitConverter.Convert(value, Category, source, target);
            copy = result.ToString("G17", CultureInfo.CurrentCulture);
            display.Set("Converted value · " + target.Symbol,
                NumberText.Format(result, CultureInfo.CurrentCulture, Context.Settings.Precision, Context.Settings.GroupDigits));
            conversionContext = ExplainFactor(source, target);
            inputError = null;
            if (record)
            {
                var label = $"{Category.Name}: {input.Text} {source.Symbol} → {target.Symbol}";
                Context.Record("Converter", label, result.ToString("G17", CultureInfo.InvariantCulture));
                Context.Status("Conversion saved to Recent and History. Open Recent or History to reuse it.");
            }
        }
        catch (CalculationException ex)
        {
            inputError = ex.Message;
            display.Set("Check your From value", ex.Message, true);
            copy = "";
            if (record) Context.Status(ex.Message);
        }
        UpdateFavoriteState();
        UpdateActionState();
        UpdateExplanation();
    }

    private void UpdateActionState()
    {
        var active = collection.SelectedIndex == 0 && from.SelectedItem is not null && to.SelectedItem is not null;
        swap.Enabled = savePair.Enabled = active;
        saveConversion.Enabled = active && copy.Length > 0;
    }

    private string ExplainFactor(UnitDefinition source, UnitDefinition target)
    {
        var multiplier = source.Scale / target.Scale;
        var offset = (source.Offset - target.Offset) / target.Scale;
        string F(double value) => NumberText.Format(value, CultureInfo.CurrentCulture, Math.Min(Context.Settings.Precision, 8), false);
        return offset == 0 ? $"1 {source.Symbol} = {F(multiplier)} {target.Symbol}"
            : $"{target.Symbol} = {source.Symbol} × {F(multiplier)} {(offset < 0 ? "−" : "+")} {F(Math.Abs(offset))}";
    }

    private void UpdateExplanation()
    {
        var precision = $"{Context.Settings.Precision} significant display digits · approx. 15-digit calculation precision";
        var detail = collection.SelectedIndex switch
        {
            1 => "Favorites stay on this device. Select a pair to return to Convert; Save pair adds or removes it there.",
            2 => "Recent conversions: select to restore value and units. History opens all calculations.",
            _ => inputError ?? conversionContext
        };
        var compactDetail = collection.SelectedIndex switch
        {
            1 => "Favorites · Select a pair to return to Convert",
            2 => "Recent · Select to restore · History shows all calculations",
            _ => detail
        };
        explanation.Text = compactLayout == true ? $"{Context.Settings.Precision} sig. digits · {compactDetail}"
            : precision + Environment.NewLine + detail;
        explanation.AccessibleDescription = precision + ". " + detail;
        tooltips.SetToolTip(explanation, explanation.AccessibleDescription);
    }

    private static void SetSegmentFonts(Control parent, float size)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is RadioButton) Ui.SetFont(child, size);
            SetSegmentFonts(child, size);
        }
    }

    private void ShowCollection()
    {
        content.Controls.Clear();
        content.Controls.Add(collection.SelectedIndex switch { 1 => favorites, 2 => recent, _ => pairs });
        Ui.Theme(content, Palette);
        Ui.AttachTooltips(content);
        content.PerformLayout();
        UpdateActionState();
        UpdateExplanation();
    }

    private void ToggleFavorite()
    {
        if (from.SelectedItem is not UnitDefinition source || to.SelectedItem is not UnitDefinition target) return;
        var replacesOldest = Context.ConversionFavorites.Count >= 20 && !Context.ConversionFavorites.Any(item =>
            item.Category == Category.Name && item.FromUnit == source.Name && item.ToUnit == target.Name);
        Context.ToggleConversionFavorite(Category.Name, source.Name, target.Name);
        UpdateFavoriteState();
        Context.Status(savePair.Selected ? replacesOldest
            ? "Unit pair saved in Favorites. The oldest pair was replaced (20-pair limit)."
            : "Unit pair saved in Favorites. Open Favorites to select it again."
            : "Unit pair removed from Favorites.");
    }

    private void UpdateFavoriteState()
    {
        if (from.SelectedItem is not UnitDefinition source || to.SelectedItem is not UnitDefinition target) return;
        savePair.Selected = Context.ConversionFavorites.Any(item => item.Category == Category.Name && item.FromUnit == source.Name && item.ToUnit == target.Name);
        savePair.Text = savePair.Selected ? "Saved pair ✓" : "Save pair";
        savePair.AccessibleDescription = savePair.Selected ? "Saved in Favorites. Activate to remove the saved pair." : "Activate to save this unit pair in Favorites.";
        savePair.Invalidate();
        favorites.MarkSelected(PairKey(Category.Name, source.Name, target.Name));
    }

    private void RefreshFavorites()
    {
        favorites.SetEntries(Context.ConversionFavorites.Select(item =>
        {
            var category = UnitConverter.Categories.FirstOrDefault(value => value.Name == item.Category);
            var source = category?.Units.FirstOrDefault(value => value.Name == item.FromUnit);
            var target = category?.Units.FirstOrDefault(value => value.Name == item.ToUnit);
            if (category is null || source is null || target is null) return null;
            return new ConverterChoice(PairKey(item.Category, item.FromUnit, item.ToUnit), $"{item.Category} · {source.Symbol} → {target.Symbol}",
                item.Category + " " + UnitSearch.Terms(source) + " " + UnitSearch.Terms(target), () => RestorePair(category, source, target));
        }).OfType<ConverterChoice>());
        UpdateFavoriteState();
    }

    private void RestorePair(UnitCategory category, UnitDefinition source, UnitDefinition target)
    {
        SelectCategory(UnitConverter.Categories.ToList().IndexOf(category));
        updating = true;
        try { from.SelectedIndex = category.Units.ToList().IndexOf(source); to.SelectedIndex = category.Units.ToList().IndexOf(target); }
        finally { updating = false; }
        from.RevealSelection(); to.RevealSelection();
        collection.SelectedIndex = 0;
        Convert(false);
        Context.Status("Favorite pair restored. Enter a value to convert.");
        input.Focus(); input.SelectAll();
    }

    private void RefreshRecent()
    {
        recent.SetEntries(Context.History.Items.Where(item => item.Mode == "Converter").Select(item => new ConverterChoice(item.Id.ToString(),
            item.Expression + " = " + item.LocalizedResult(CultureInfo.CurrentCulture), item.Expression + " " + item.Result, () => RestoreRecent(item))));
    }

    private void RestoreRecent(HistoryItem item)
    {
        foreach (var category in UnitConverter.Categories)
        foreach (var source in category.Units)
        foreach (var target in category.Units)
        {
            var suffix = $" {source.Symbol} → {target.Symbol}";
            if (!item.Expression.EndsWith(suffix, StringComparison.Ordinal)) continue;
            var raw = item.Expression[..^suffix.Length];
            if (raw.StartsWith(category.Name + ": ", StringComparison.Ordinal)) raw = raw[(category.Name.Length + 2)..];
            else if (raw.Contains(": ", StringComparison.Ordinal)) continue;
            var invariantInput = new string(raw.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format).ToArray())
                .Replace('−', '-').Replace(',', '.').Replace('٫', '.');
            if (!NumberText.TryDouble(raw, CultureInfo.CurrentCulture, out var value, allowGrouping: false) &&
                !double.TryParse(invariantInput, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
            if (!double.IsFinite(value)) continue;
            RestorePair(category, source, target);
            input.Text = value.ToString("G17", CultureInfo.CurrentCulture);
            Context.Status("Recent conversion restored with its value and units.");
            return;
        }
        Context.ShowHistory();
        Context.Status("This older entry cannot restore unit metadata. Its full result is available in History.");
    }

    public override string CopyText => copy;
    public override void Reuse(string value) { collection.SelectedIndex = 0; input.Text = value; input.Focus(); input.SelectAll(); }
    public override void FocusInput() => input.Focus();
    public override void RefreshFormatting() { AdaptCompactLayout(); Convert(false); RefreshRecent(); }
    public override bool Command(Keys key)
    {
        if (key != Keys.Enter || !input.Focused) return false;
        Convert(true); return true;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Context.ConversionFavoritesChanged -= RefreshFavorites;
            Context.JournalChanged -= RefreshRecent;
            tooltips.Dispose();
            // Detached collections still belong to this view.
            favorites.Dispose(); recent.Dispose(); pairs.Dispose();
        }
        base.Dispose(disposing);
    }
}
