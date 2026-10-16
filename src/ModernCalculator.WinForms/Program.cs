// Developer ::> Gehan Fernando
using ModernCalculator.Infrastructure.Persistence;
using ModernCalculator.WinForms.Forms;

namespace ModernCalculator.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Allow isolated profiles without changing the default local application data directory.
        var directory = Environment.GetEnvironmentVariable("ASTER_DATA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AsterCalculator");
        Application.Run(new MainForm(new StateStore(directory)));
    }
}
