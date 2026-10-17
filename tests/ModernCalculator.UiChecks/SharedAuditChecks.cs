using System.Globalization;
using System.Reflection;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Forms;
using ModernCalculator.WinForms.Themes;
using ModernCalculator.WinForms.Views;

namespace ModernCalculator.UiChecks;

public static class SharedAuditChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("Shared audit: persistent expression and exact Standard undo/redo", StandardEditing);
        run("Shared audit: consistent memory state and feedback", MemoryState);
        run("Shared audit: preference drafts, restore defaults and staged history clear", PreferencesDrafts);
        run("Shared audit: invalid display precision cannot commit", InvalidPrecision);
        run("Shared audit: measured palette text, state and boundary contrast", PaletteContrast);
        run("Shared audit: help topics and pages retain all guidance", HelpPages);
        run("Shared audit: storage failure remains visible and retryable", StorageFailure);
    }
    private static void StandardEditing()
    {
        var context = new WorkspaceContext(); using var view = new StandardView(context); using var host = Host(view, new(620, 680));
        foreach (var digit in "125*8") view.Character(digit);
        Require(Named<Label>(view, "Standard expression").Text == "125 × 8", "Pending expression must show both operands.");
        view.Command(Keys.Enter); Require(view.CopyText == "1000", "Standard result changed.");
        view.Command(Keys.Control | Keys.Z); Require(view.CopyText == "8", "Undo did not restore the pending operand.");
        view.Command(Keys.Control | Keys.Y); Require(view.CopyText == "1000", "Redo did not restore the exact result.");
        Require(context.History.Items.Count == 1, "Undo/redo duplicated or removed history.");
        view.Command(Keys.Enter); Require(view.CopyText == "8000", "Redo lost repeated-equals state.");
        view.Command(Keys.Escape); view.Character('9'); view.Character('/'); view.Character('0'); view.Command(Keys.Enter);
        Require(Descendants(view).OfType<ResultDisplay>().Single().FullValue.Contains("zero", StringComparison.OrdinalIgnoreCase), "Division by zero lacks an explicit error.");
        view.Character('4'); Require(view.CopyText == "4", "A new digit did not recover from error.");
    }
    private static void MemoryState()
    {
        var context = new WorkspaceContext(); var messages = new List<string>(); context.StatusChanged += messages.Add;
        using var view = new StandardView(context); using var host = Host(view, new(620, 680));
        var actions = Descendants(view).OfType<CalcButton>().Where(button => button.Parent?.AccessibleName == "Calculator memory").ToArray();
        Require(actions.Length == 6 && actions.All(button => button.Tone == ButtonTone.Normal), "Shared memory actions have inconsistent styling.");
        Require(!Named<CalcButton>(view, "Recall newest memory").Enabled && !Named<CalcButton>(view, "Clear all memory").Enabled, "Empty memory exposes active recall/clear.");
        view.Reuse("123456789012345678901234567890"); Activate(Named<CalcButton>(view, "Store in new memory slot"));
        Require(Named<CalcButton>(view, "Open memory list").Text.Contains('1'), "No visible populated-memory indicator.");
        Require(Named<CalcButton>(view, "Recall newest memory").Enabled && messages.Any(text => text.Contains("memory", StringComparison.OrdinalIgnoreCase)), "Memory changes have no state/feedback.");
        view.Command(Keys.Escape); Activate(Named<CalcButton>(view, "Recall newest memory")); Require(view.CopyText == "123456789012345678901234567890", "Memory recall narrowed the value.");
        context.Memory.Clear(); context.NotifyMemory(); view.Reuse("5"); Activate(Named<CalcButton>(view, "Add to newest memory"));
        Require(context.Memory.RecallExact().ToString() == "5", "M+ no longer initializes an empty memory slot.");
        context.Memory.Clear(); context.NotifyMemory(); Activate(Named<CalcButton>(view, "Subtract from newest memory"));
        Require(context.Memory.RecallExact().ToString() == "-5", "M− no longer initializes a negative empty memory slot.");
    }
    private static void PreferencesDrafts()
    {
        var initial = new AppSettings { Theme = ThemePreference.Dark, Precision = 11, PersistHistory = true };
        using (var form = new SettingsForm(initial, Palette.Resolve(initial.Theme)))
        {
            using var host = Host(form, new(570, 500), true);
            Named<SegmentedControl>(form, "Theme preference").SelectedIndex = (int)ThemePreference.Light;
            Named<SegmentedControl>(form, "Preferences section").SelectedIndex = 2;
            var clear = Named<CalcButton>(form, "Clear saved history"); Activate(clear); Require(!form.ClearHistoryRequested, "History clear bypassed its confirmation step.");
            Activate(clear); Require(form.ClearHistoryRequested, "Confirmed history clear was not staged.");
            Activate(Named<CalcButton>(form, "Cancel")); Require(form.DialogResult == DialogResult.Cancel && form.Result == initial, "Cancel committed a settings draft.");
        }
        using var saved = new SettingsForm(initial, Palette.Resolve(initial.Theme)); using var savedHost = Host(saved, new(570, 500), true);
        Activate(Named<CalcButton>(saved, "Restore defaults")); Require(saved.Result == initial, "Restore defaults changed committed preferences before Save.");
        Activate(Named<CalcButton>(saved, "Save preferences")); Require(saved.Result.Theme == ThemePreference.Light && saved.Result.Precision == 12 && !saved.Result.PersistHistory, "Saving default draft failed.");
    }
    private static void InvalidPrecision()
    {
        var initial = new AppSettings(); using var form = new SettingsForm(initial, Palette.Resolve(initial.Theme)); using var host = Host(form, new(570, 500), true);
        var sections = Named<SegmentedControl>(form, "Preferences section"); sections.SelectedIndex = 2;
        var input = Named<FlatNumberInput>(form, "Scientific significant display digits"); input.Editor.Text = "16";
        Activate(Named<CalcButton>(form, "Save preferences")); Require(form.DialogResult != DialogResult.OK && form.Result == initial, "Invalid precision committed settings.");
        var feedback = Named<Label>(form, "Display precision feedback");
        Require(sections.SelectedIndex == 1 && feedback.Parent == input.Parent && feedback.Text.Contains("3 to 15", StringComparison.Ordinal),
            "Invalid precision feedback is not on the selected Numbers page.");
        Require(feedback.ForeColor == Palette.Resolve(initial.Theme).Error && input.Editor.AccessibleDescription?.Contains("3 to 15", StringComparison.Ordinal) == true,
            "Invalid precision feedback lacks error styling or an accessible explanation.");
        input.Editor.Text = "7"; Require(feedback.Text.StartsWith("Example:", StringComparison.Ordinal) && feedback.ForeColor == Palette.Resolve(initial.Theme).Muted,
            "Corrected precision retained its error feedback.");
        Activate(Named<CalcButton>(form, "Save preferences")); Require(form.Result.Precision == 7, "Valid corrected precision was not saved.");
    }
    private static void PaletteContrast()
    {
        if (SystemInformation.HighContrast) return; // Custom OS palettes are user-defined; validate their source, not arbitrary ratios.
        foreach (var preference in new[] { ThemePreference.Light, ThemePreference.Dark })
        {
            var palette = Palette.Resolve(preference); var selected = Mix(palette.Surface, palette.Accent, .2);
            foreach (var surface in new[] { palette.Window, palette.Surface, palette.Key, palette.NumberKey })
            { Require(Contrast(palette.Text, surface) >= 4.5 && Contrast(palette.Muted, surface) >= 4.5, "Normal/muted text contrast is below 4.5:1 in " + preference); }
            Require(Contrast(palette.AccentText, palette.Accent) >= 4.5, "Accent button text contrast is below 4.5:1.");
            Require(Contrast(palette.Text, selected) >= 4.5 && Contrast(palette.Accent, selected) >= 3, "Selected label/underline contrast is insufficient.");
            Require(Contrast(palette.Border, palette.Surface) >= 3 && Contrast(palette.Muted, palette.Key) >= 3, "Required control boundary/icon contrast is insufficient.");
        }
    }
    private static Color Mix(Color a, Color b, double t) => Color.FromArgb((int)(a.R * (1-t) + b.R*t), (int)(a.G * (1-t)+b.G*t), (int)(a.B*(1-t)+b.B*t));
    private static double Contrast(Color a, Color b)
    {
        double L(Color c) { double V(byte channel) { var v = channel / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v+.055)/1.055,2.4); } return .2126*V(c.R)+.7152*V(c.G)+.0722*V(c.B); }
        var x=L(a); var y=L(b); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
    private static void HelpPages()
    {
        foreach (var theme in new[] { ThemePreference.Light, ThemePreference.Dark })
        using (var help = new HelpForm(new AppSettings(), Palette.Resolve(theme)))
        {
            using var host = Host(help, new(620, 400), true); var topics = Named<SegmentedControl>(help, "Help topic"); var found = new HashSet<string>();
            for (var index = 0; index < 4; index++)
            {
                topics.SelectedIndex = index;
                for (var page=0;page<12;page++)
                {
                    Layout(help); foreach(var label in Descendants(help).OfType<Label>()) found.Add(label.Text);
                    var next=Named<CalcButton>(help,"Next help page"); if (!next.Enabled) break; Activate(next);
                }
            }
            Require(found.Any(text=>text.Contains("Ctrl+Z",StringComparison.Ordinal)) && found.Any(text=>text.Contains("root(x,n)",StringComparison.Ordinal)) && found.Any(text=>text.Contains("Memory",StringComparison.OrdinalIgnoreCase)), "Help paging lost required guidance.");
            Require(!Descendants(help).Any(control=>control is ComboBox || control is ScrollableControl {AutoScroll:true}),"Help uses a dropdown or scrolling overflow.");
        }
    }
    private static void StorageFailure()
    {
        var blocker=Path.Combine(Path.GetTempPath(),"AsterBlocked-"+Guid.NewGuid().ToString("N")); File.WriteAllText(blocker,"test");
        try
        {
            using var form=new MainForm(new StateStore(blocker));
            var method=typeof(MainForm).GetMethod("TrySaveState",BindingFlags.Instance|BindingFlags.NonPublic)!;
            Require(method.Invoke(form,null) is false,"Blocked storage incorrectly reports success.");
            Require(Descendants(form).OfType<Label>().Any(label=>label.Text.Contains("may still be on disk",StringComparison.Ordinal)),"Storage failure/clear-data risk was hidden.");
        }
        finally {File.Delete(blocker);}
    }
    private static Form Host(Control control, Size size, bool existingForm=false)
    {
        var form=existingForm ? (Form)control : new Form {ClientSize=size,AutoScaleMode=AutoScaleMode.None};
        if(!existingForm)form.Controls.Add(control); else form.ClientSize=size;
        _=form.Handle; Layout(form); Handles(form); Layout(form); return form;
    }
    private static void Handles(Control control){_=control.Handle;foreach(Control child in control.Controls)Handles(child);}
    private static void Layout(Control control){control.PerformLayout();foreach(Control child in control.Controls)Layout(child);}
    private static void Activate(CalcButton button){Require(button.Enabled,"Disabled button: "+button.AccessibleName); typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(button,[EventArgs.Empty]);}
    private static T Named<T>(Control root,string name) where T:Control=>Descendants(root).OfType<T>().Single(item=>item.AccessibleName==name);
    private static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
