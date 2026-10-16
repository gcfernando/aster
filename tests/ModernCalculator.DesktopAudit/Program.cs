using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.Infrastructure.Settings;
using ModernCalculator.WinForms.Forms;

namespace ModernCalculator.DesktopAudit;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // This is the production MainForm and engines with an isolated store, not a UI mock.
        // The host sends no input and runs no automated actions.
        var session = Path.Combine(AppContext.BaseDirectory, "sessions", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(session);
        var store = new StateStore(session);
        var warning = store.Save(new AppSettings { Theme = ThemePreference.Dark, Mode = "Standard", PersistHistory = false }, []);
        if (warning is not null) throw new IOException(warning);
        using var form = new MainForm(store) { Text = "Aster · Independent test session" };
        Application.Run(form);
    }
}
