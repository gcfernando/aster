using System.Globalization;
using ModernCalculator.WinForms.Controls;

namespace ModernCalculator.WinForms.Forms;

public sealed partial class MainForm
{
    private readonly TableLayoutPanel drawer = Ui.Table(1, 36, 32, -100, 28, 30);
    private readonly TextBox historySearch = Ui.Input("Search calculation history");
    private readonly Panel journalList = new() { Dock = DockStyle.Fill, Margin = new(0), AccessibleName = "Stored result" };
    private readonly SegmentedControl drawerTabs = new("Secondary panel", ["History", "Memory"]);
    private readonly Label journalPage = Ui.Label("", 8);
    private CalcButton clearJournal = null!;
    private CalcButton previousJournal = null!;
    private CalcButton nextJournal = null!;
    private bool memoryTab;
    private int journalIndex;
    private void BuildDrawer()
    {
        drawer.Padding = new(8, 0, 0, 0);
        drawerTabs.SelectedIndexChanged += (_, _) => { memoryTab = drawerTabs.SelectedIndex == 1; journalIndex = 0; RefreshDrawer(); };
        drawer.Controls.Add(drawerTabs, 0, 0);
        Ui.SetFont(historySearch, 9); historySearch.BorderStyle = BorderStyle.None; historySearch.Margin = new(4, 4, 4, 2); historySearch.PlaceholderText = "Search calculations";
        historySearch.TextChanged += (_, _) => { journalIndex = 0; RefreshDrawer(); }; drawer.Controls.Add(historySearch, 0, 1); drawer.Controls.Add(journalList, 0, 2);
        var pages = Ui.Table(3, -100); pages.ColumnStyles.Clear(); pages.ColumnStyles.Add(new(SizeType.Absolute, 32)); pages.ColumnStyles.Add(new(SizeType.Percent, 100)); pages.ColumnStyles.Add(new(SizeType.Absolute, 32));
        previousJournal = Ui.Button("‹", () => { journalIndex--; RefreshDrawer(); }, ButtonTone.Quiet, name: "Previous stored results");
        nextJournal = Ui.Button("›", () => { journalIndex++; RefreshDrawer(); }, ButtonTone.Quiet, name: "Next stored results");
        journalPage.AccessibleName = "Stored results page"; journalPage.TextAlign = ContentAlignment.MiddleCenter;
        pages.Controls.Add(previousJournal, 0, 0); pages.Controls.Add(journalPage, 1, 0); pages.Controls.Add(nextJournal, 2, 0); drawer.Controls.Add(pages, 0, 3);
        clearJournal = Ui.Button("Clear history", () =>
        {
            if (memoryTab) { context.Memory.Clear(); context.NotifyMemory(); }
            else { context.History.Clear(); context.NotifyHistory(); }
        }, ButtonTone.Quiet, IconKind.Trash);
        Ui.SetFont(clearJournal, 9); drawer.Controls.Add(clearJournal, 0, 4);
    }
    private void RefreshDrawer()
    {
        if (clearJournal is null) return;
        journalList.SuspendLayout();
        while (journalList.Controls.Count > 0) { var child = journalList.Controls[0]; journalList.Controls.RemoveAt(0); child.Dispose(); }
        historySearch.Visible = !memoryTab; drawer.RowStyles[1].Height = memoryTab ? 0 : 32 * DeviceDpi / 96f;
        clearJournal.Text = memoryTab ? "Clear memory" : "Clear history";
        var count = 0;
        if (memoryTab)
        {
            var items = context.Memory.Items; count = items.Count; journalIndex = Math.Clamp(journalIndex, 0, Math.Max(0, count - 1));
            if (count > 0)
            {
                var item = items[journalIndex]; var text = item.ExactValue.ToString(CultureInfo.CurrentCulture);
                AddJournalRow("Stored number", text, () => ReuseMemory(item.ExactValue), () => CopyText(text),
                    () => { context.Memory.Remove(item.Id); context.NotifyMemory(); });
            }
        }
        else
        {
            var items = context.History.Search(historySearch.Text).ToArray(); count = items.Length; journalIndex = Math.Clamp(journalIndex, 0, Math.Max(0, count - 1));
            if (count > 0)
            {
                var item = items[journalIndex]; var local = LocalResult(item);
                AddJournalRow(item.Expression, local, () => ReuseHistory(item), () => CopyText(local),
                    () => { context.History.Delete(item.Id); context.NotifyHistory(); }, item.Mode + " · " + item.Timestamp.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture));
            }
        }
        clearJournal.Enabled = memoryTab ? context.Memory.Items.Count > 0 : context.History.Items.Count > 0;
        previousJournal.Enabled = journalIndex > 0; nextJournal.Enabled = journalIndex + 1 < count;
        journalPage.Text = count == 0 ? "No results" : $"{journalIndex + 1} / {count}";
        if (count == 0)
        {
            var empty = Ui.Label(memoryTab ? "No stored numbers\r\nUse MS to store a value." : historySearch.Text.Length > 0 ? "No matching calculations." : "No calculations yet.", 9);
            empty.TextAlign = ContentAlignment.MiddleCenter; empty.ForeColor = palette.Muted; journalList.Controls.Add(empty);
        }
        Ui.Theme(drawer, palette); journalList.ResumeLayout(); SetTooltips(drawer);
    }
    private void AddJournalRow(string expression, string result, Action reuse, Action copy, Action delete, string? caption = null)
    {
        var scale = DeviceDpi / 96f;
        var card = Ui.Table(1, 24 * scale, -100, 32 * scale);
        var metadata = Ui.Label(caption ?? "Session memory", 8); metadata.ForeColor = palette.Muted; card.Controls.Add(metadata, 0, 0);
        var value = new ResultDisplay { AccessibleName = "Full stored result" }; value.Set(expression, result); card.Controls.Add(value, 0, 1);
        var actions = Ui.Table(3, -100); actions.ColumnStyles.Clear(); actions.ColumnStyles.Add(new(SizeType.Percent, 100)); actions.ColumnStyles.Add(new(SizeType.Absolute, 32 * scale)); actions.ColumnStyles.Add(new(SizeType.Absolute, 32 * scale));
        var reuseButton = Ui.Button("Use result", reuse, ButtonTone.Normal, name: "Reuse result " + result); Ui.SetFont(reuseButton, 9);
        actions.Controls.Add(reuseButton, 0, 0); actions.Controls.Add(Ui.Button("", copy, ButtonTone.Quiet, IconKind.Copy, "Copy this result"), 1, 0); actions.Controls.Add(Ui.Button("", delete, ButtonTone.Quiet, IconKind.Trash, "Delete this item"), 2, 0);
        card.Controls.Add(actions, 0, 2); journalList.Controls.Add(card);
    }
}
