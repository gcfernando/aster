using System.Globalization;
using System.Reflection;
using ModernCalculator.Core.Conversions;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

public static class ConverterViewAuditChecks
{
    private static readonly MethodInfo ClickEvent = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Control.OnClick is unavailable.");
    private static readonly MethodInfo KeyDownEvent = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Control.OnKeyDown is unavailable.");

    public static void Run(Action<string, Action> run)
    {
        run("Converter audit: names, symbols, aliases and empty matches", SearchMatches);
        run("Converter audit: every unit is directly reachable by symbol", EveryUnit);
        run("Converter audit: favorites and recent restore value and units", FavoritesAndRecent);
        run("Converter audit: invalid input cannot save a conversion", InvalidInput);
        run("Converter audit: culture and compact controls remain accessible", CultureAndCompact);
        run("Converter review: paged search Enter selects the visible first match", PagedEnter);
        run("Converter review: collection browsing disables hidden conversion actions", CollectionActions);
        run("Converter review: favorite capacity replacement is explained", FavoriteCapacity);
    }

    private static void SearchMatches()
    {
        using var host = Host(new WorkspaceContext(), new Size(600, 480));
        var view = Descendants(host).OfType<ConverterView>().Single();
        var source = Named<SearchableUnitSelector>(view, "Source unit");
        foreach (var (query, expected) in new[] { ("Kilometre", "km"), ("km", "km"), ("kilometers", "km"), ("feet", "ft"), ("inches", "in") })
        {
            SearchAndChoose(source, query);
            Require(source.SelectedItem?.Symbol == expected, "Source search did not match " + query);
        }
        var selected = source.SelectedIndex;
        Named<TextBox>(source, "Source unit choices search").Text = "no such unit exists";
        Require(!Descendants(source).OfType<CalcButton>().Any(button => button.Enabled && button.AccessibleName?.StartsWith("Source unit choices: ", StringComparison.Ordinal) == true), "Empty search left selectable results.");
        Require(source.SelectedIndex == selected, "An empty search changed the selected unit.");
        Require(Descendants(source).OfType<Label>().Any(label => label.Text == "No matches"), "Empty search does not explain the state.");
        Activate(Named<CalcButton>(view, "Temperature"));
        SearchAndChoose(source, "centigrade");
        Require(source.SelectedItem?.Symbol == "°C", "Centigrade alias did not select Celsius.");
        Activate(Named<CalcButton>(view, "Data storage"));
        SearchAndChoose(source, "octets");
        Require(source.SelectedItem?.Symbol == "B", "Octet alias did not select bytes.");
        Activate(Named<CalcButton>(view, "Pressure"));
        SearchAndChoose(source, "pounds per square inch");
        Require(source.SelectedItem?.Symbol == "psi", "PSI alias did not match.");
    }

    private static void EveryUnit()
    {
        using var host = Host(new WorkspaceContext(), new Size(550, 250));
        var view = Descendants(host).OfType<ConverterView>().Single();
        foreach (var category in UnitConverter.Categories)
        {
            Activate(Named<CalcButton>(view, category.Name));
            foreach (var name in new[] { "Source unit", "Destination unit" })
            {
                var selector = Named<SearchableUnitSelector>(view, name);
                Require(selector.Items.Count == category.Units.Count, "Category lost catalog units.");
                foreach (var unit in category.Units)
                {
                    SearchAndChoose(selector, unit.Symbol);
                    Require(selector.SelectedItem == unit, $"Symbol {unit.Symbol} did not select {unit.Name} in {category.Name}.");
                }
            }
        }
    }

    private static void FavoritesAndRecent()
    {
        var context = new WorkspaceContext();
        using var host = Host(context, new Size(600, 480));
        var view = Descendants(host).OfType<ConverterView>().Single();
        var source = Named<SearchableUnitSelector>(view, "Source unit");
        var target = Named<SearchableUnitSelector>(view, "Destination unit");
        var value = Named<TextBox>(view, "Value to convert");
        var workspace = Named<SegmentedControl>(view, "Converter workspace");
        value.Text = "1000";
        source.SelectedIndex = 0; target.SelectedIndex = 1;
        Activate(Named<CalcButton>(view, "Save current unit pair to Favorites"));
        Require(context.ConversionFavorites.Count == 1 && context.History.Items.Count == 0, "Save pair must add Favorites without adding History.");
        workspace.SelectedIndex = 1;
        Require(Descendants(view).OfType<ConverterChoiceBrowser>().Any(browser => browser.AccessibleName == "Favorite conversion pairs"), "Favorites destination is missing.");
        Activate(Choice(view, "Favorite conversion pairs: "));
        Require(workspace.SelectedIndex == 0 && source.SelectedItem?.Symbol == "m" && target.SelectedItem?.Symbol == "km", "Favorite did not reopen its pair.");
        Activate(Named<CalcButton>(view, "Save this conversion to history"));
        Require(context.History.Items.Count == 1 && context.History.Items[0].Mode == "Converter", "Save conversion did not add History.");
        Activate(Named<CalcButton>(view, "Mass")); value.Text = "2";
        workspace.SelectedIndex = 2;
        Activate(Choice(view, "Recent conversions: "));
        Require(workspace.SelectedIndex == 0 && value.Text == "1000", "Recent did not restore the entered value.");
        Require(source.SelectedItem?.Symbol == "m" && target.SelectedItem?.Symbol == "km", "Recent did not restore source/target units.");
        Require(double.Parse(view.CopyText, CultureInfo.CurrentCulture) == 1, "Restored recent conversion result changed.");
        var historyRequests = 0; context.ShowHistoryRequested += () => historyRequests++;
        Activate(Named<CalcButton>(view, "Open conversion history"));
        Require(historyRequests == 1, "History entry did not request the shared History panel.");
        Activate(Named<CalcButton>(view, "Save current unit pair to Favorites"));
        Require(context.ConversionFavorites.Count == 0, "Saved pair could not be removed by toggling.");
    }

    private static void InvalidInput()
    {
        var context = new WorkspaceContext();
        using var host = Host(context, new Size(600, 480));
        var view = Descendants(host).OfType<ConverterView>().Single();
        var input = Named<TextBox>(view, "Value to convert");
        foreach (var invalid in new[] { "", "not a number", "1e999", "1e-999" })
        {
            input.Text = invalid;
            Require(view.CopyText.Length == 0, "Invalid input left a copyable numeric value.");
            Require(!Named<CalcButton>(view, "Save this conversion to history").Enabled, "Invalid conversion can still be saved.");
            Require(context.History.Items.Count == 0, "Invalid conversion entered History.");
            var output = Descendants(view).OfType<ResultDisplay>().Single();
            Require(output.FullValue.Length > 0 && !double.TryParse(output.FullValue, out _), "Invalid input has no explanatory result error.");
        }
        Activate(Named<CalcButton>(view, "Temperature")); input.Text = "-274";
        Require(view.CopyText.Length == 0, "Temperature below absolute zero was accepted.");
        input.Text = "0";
        Require(view.CopyText.Length > 0 && Named<CalcButton>(view, "Save this conversion to history").Enabled, "Correcting input did not recover conversion.");
    }

    private static void PagedEnter()
    {
        using var host = Host(new WorkspaceContext(), new Size(600, 480));
        var selector = Named<SearchableUnitSelector>(host, "Source unit");
        Activate(Named<CalcButton>(selector, "Source unit choices more matches"));
        var firstVisible = Choice(selector, "Source unit choices: ").Text;
        KeyDownEvent.Invoke(Named<TextBox>(selector, "Source unit choices search"), [new KeyEventArgs(Keys.Enter)]);
        Require(selector.SelectedItem?.ToString() == firstVisible, "Search Enter selected a hidden earlier page instead of its visible first match.");
    }

    private static void CollectionActions()
    {
        var context = new WorkspaceContext(); using var host = Host(context, new Size(600, 480));
        var view = Descendants(host).OfType<ConverterView>().Single();
        var workspace = Named<SegmentedControl>(view, "Converter workspace");
        foreach (var index in new[] { 1, 2 })
        {
            workspace.SelectedIndex = index;
            foreach (var name in new[] { "Swap units", "Save current unit pair to Favorites", "Save this conversion to history" })
                Require(!Named<CalcButton>(view, name).Enabled, "Browsing a collection leaves a hidden conversion action enabled: " + name);
            Require(Named<CalcButton>(view, "Open conversion history").Enabled, "Collection browsing disabled History navigation.");
            view.RefreshFormatting();
            Require(!Named<CalcButton>(view, "Save this conversion to history").Enabled, "Formatting re-enabled a hidden save action.");
        }
        workspace.SelectedIndex = 0;
        Require(Named<CalcButton>(view, "Save this conversion to history").Enabled, "Returning to the valid paired workspace did not restore saving.");
        Require(context.History.Items.Count == 0 && context.ConversionFavorites.Count == 0, "Collection navigation mutated saved data.");
    }

    private static void FavoriteCapacity()
    {
        var context = new WorkspaceContext(); var category = UnitConverter.Categories[0];
        var pairs = category.Units.SelectMany(source => category.Units.Select(target => new ConversionFavorite(category.Name, source.Name, target.Name)))
            .Where(pair => pair.FromUnit != category.Units[0].Name || pair.ToUnit != category.Units[1].Name).Take(20).ToArray();
        context.RestoreConversionFavorites(pairs);
        var status = ""; context.StatusChanged += message => status = message;
        using var host = Host(context, new Size(600, 480));
        Activate(Named<CalcButton>(host, "Save current unit pair to Favorites"));
        Require(context.ConversionFavorites.Count == 20 && !context.ConversionFavorites.Contains(pairs[^1]), "Favorite capacity replacement changed its existing bound/order.");
        Require(status.Contains("oldest", StringComparison.OrdinalIgnoreCase) && status.Contains("20", StringComparison.Ordinal), "Favorite replacement feedback does not disclose the capacity and replaced oldest pair.");
    }

    private static void CultureAndCompact()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            using var host = Host(new WorkspaceContext(), new Size(550, 250));
            var view = Descendants(host).OfType<ConverterView>().Single();
            Named<TextBox>(view, "Value to convert").Text = "1,25";
            var source = Named<SearchableUnitSelector>(view, "Source unit");
            var target = Named<SearchableUnitSelector>(view, "Destination unit");
            source.SelectedIndex = 0; target.SelectedIndex = 1;
            Require(Math.Abs(double.Parse(view.CopyText, CultureInfo.CurrentCulture) - .00125) < 1e-15, "Regional decimal conversion changed.");
            foreach (var control in Descendants(view).Prepend(view))
            {
                Require(control is not ComboBox && control.ContextMenuStrip is null, "Converter introduced a dropdown/menu.");
                if (control is ScrollableControl scrollable) Require(!scrollable.AutoScroll && !scrollable.VerticalScroll.Visible && !scrollable.HorizontalScroll.Visible, "Converter has a pane scrollbar.");
                if (control is TextBox text) Require(text.ScrollBars == ScrollBars.None, "Converter textbox has a scrollbar.");
                if (control is CalcButton or TextBox or SearchableUnitSelector or SegmentedControl)
                    Require(!string.IsNullOrWhiteSpace(control.AccessibleName), "Converter interactive control lacks a name.");
            }
            Require(Descendants(view).OfType<Label>().Any(label => label.AccessibleName == "Conversion precision and factor" && label.Text.Contains("sig. digits", StringComparison.Ordinal)), "Compact precision context is missing.");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static void SearchAndChoose(SearchableUnitSelector selector, string query)
    {
        var search = Descendants(selector).OfType<TextBox>().Single();
        search.Text = query;
        // Invoke the hidden component's Enter handler; no OS input is sent.
        KeyDownEvent.Invoke(search, [new KeyEventArgs(Keys.Enter)]);
    }

    private static CalcButton Choice(Control scope, string prefix) => Descendants(scope).OfType<CalcButton>()
        .First(button => button.Enabled && button.AccessibleName?.StartsWith(prefix, StringComparison.Ordinal) == true);
    private static T Named<T>(Control scope, string name) where T : Control => Descendants(scope).OfType<T>().Single(item => item.AccessibleName == name);
    private static void Activate(CalcButton button)
    {
        Require(button.Enabled, "Cannot activate disabled component " + button.AccessibleName);
        try { ClickEvent.Invoke(button, [EventArgs.Empty]); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); }
    }
    private static Form Host(WorkspaceContext context, Size logicalSize)
    {
        var host = new Form { AutoScaleMode = AutoScaleMode.None };
        _ = host.Handle;
        var scale = host.DeviceDpi / 96f;
        host.ClientSize = new Size((int)(logicalSize.Width * scale), (int)(logicalSize.Height * scale));
        host.Controls.Add(new ConverterView(context));
        Ui.Theme(host, Palette.Resolve(context.Settings.Theme));
        Prepare(host);
        return host;
    }
    private static void Prepare(Control control)
    {
        _ = control.Handle;
        control.PerformLayout();
        foreach (var child in control.Controls.Cast<Control>().ToArray()) Prepare(child);
        control.PerformLayout();
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
