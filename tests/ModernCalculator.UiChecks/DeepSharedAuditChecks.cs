using System.Reflection;
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Forms;

namespace ModernCalculator.UiChecks;

/// <summary>Hidden workspace checks for unnecessary I/O, lazy drawer updates and action labels.</summary>
public static class DeepSharedAuditChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("Deep shared: disabled history persistence avoids per-calculation profile writes", HistoryOptOutWrites);
        check("Deep shared: opted-in history still saves each completed result", HistoryOptInWrites);
        check("Deep shared: closed drawer keeps its card until reopened", HiddenDrawerUpdates);
        check("Deep shared: drawer actions and page descriptions follow the active collection", DrawerActionNames);
        check("Deep shared: caption action name follows maximize and restore", CaptionActionName);
    }

    private static void HistoryOptOutWrites() => WithForm(false, (form, context, directory) =>
    {
        var path = Path.Combine(directory, "state.json");
        var unchanged = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, unchanged);
        var before = File.ReadAllBytes(path);
        for (var index = 0; index < 20; index++) context.Record("Standard", $"{index} + 1 =", (index + 1).ToString());
        Require(context.History.Items.Count == 20, "History opt-out discarded session history.");
        Require(File.GetLastWriteTimeUtc(path) == unchanged && before.SequenceEqual(File.ReadAllBytes(path)),
            "A calculation rewrote the profile while history persistence was disabled.");
    });

    private static void HistoryOptInWrites() => WithForm(true, (_, context, directory) =>
    {
        context.Record("Standard", "1 + 2 =", "3");
        var loaded = new StateStore(directory).Load();
        Require(loaded.Warning is null && loaded.State.History.Count == 1 && loaded.State.History[0].Result == "3",
            "Opted-in calculation history was not saved.");
    });

    private static void HiddenDrawerUpdates() => WithForm(false, (form, context, _) =>
    {
        Invoke(form, "SetDrawer", true);
        var list = Field<Panel>(form, "journalList");
        var original = list.Controls[0];
        Invoke(form, "SetDrawer", false);
        for (var index = 0; index < 20; index++) context.Record("Standard", $"{index} + 1 =", (index + 1).ToString());
        Require(!original.IsDisposed && ReferenceEquals(original, list.Controls[0]),
            "A closed drawer recreated/disposed its stored-result card.");
        Invoke(form, "SetDrawer", true);
        Require(original.IsDisposed && Descendants(list).OfType<ResultDisplay>().Single().FullValue == "20",
            "Reopening the drawer did not replace its stale card with the latest result.");
    });

    private static void DrawerActionNames() => WithForm(false, (form, context, _) =>
    {
        context.Record("Standard", "1 + 2 =", "3");
        context.Memory.Store(5m); context.NotifyMemory();
        Invoke(form, "SetDrawer", true);
        var clear = Field<CalcButton>(form, "clearJournal");
        var page = Field<Label>(form, "journalPage");
        var tabs = Field<SegmentedControl>(form, "drawerTabs");
        Require(clear.AccessibleName == "Clear history" && page.AccessibleDescription == "1 / 1",
            "History action/page accessibility does not describe the current collection.");
        tabs.SelectedIndex = 1;
        Require(clear.AccessibleName == "Clear memory" && clear.AccessibleDescription?.Contains("memory", StringComparison.Ordinal) == true,
            "The memory-clear action still announces that it clears history.");
        tabs.SelectedIndex = 0;
        Require(clear.AccessibleName == "Clear history", "Returning to History retained the memory action name.");
    });

    private static void CaptionActionName() => WithForm(false, (form, _, _) =>
    {
        var button = Field<CalcButton>(form, "maximizeButton");
        form.WindowState = FormWindowState.Maximized; Invoke(form, "UpdateResponsiveChrome");
        Require(button.AccessibleName == "Restore window", "The restore action still announces Maximize.");
        form.WindowState = FormWindowState.Normal; Invoke(form, "UpdateResponsiveChrome");
        Require(button.AccessibleName == "Maximize window", "The maximize action still announces Restore.");
    });

    private static void WithForm(bool persistHistory, Action<MainForm, WorkspaceContext, string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AsterDeepShared-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(directory);
            Require(store.Save(new AppSettings { PersistHistory = persistHistory }, []) is null, "Could not seed isolated profile.");
            using var form = new MainForm(store);
            _ = form.Handle;
            action(form, Field<WorkspaceContext>(form, "context"), directory);
            Require(!form.Visible, "Shared regression windows must remain hidden.");
        }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Require(string.Equals(Path.GetDirectoryName(resolved), temporaryRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("AsterDeepShared-", StringComparison.Ordinal), "Unexpected test profile path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }

    private static object? Invoke(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
