using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Wpf.Ui.Appearance;

namespace CharmChecker.App;

public partial class App : Application
{
    private Mutex? _instanceLock;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mutexName = "MHWildsCharmChecker_" + HashDataDir(AppDomain.CurrentDomain.BaseDirectory);
        _instanceLock = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "このフォルダのMHWilds護石チェッカーは既に起動しています。\n護石データ（charms.json）の破損を防ぐため、同じフォルダから複数起動することはできません。",
                "多重起動",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceLock?.ReleaseMutex();
        _instanceLock?.Dispose();
        base.OnExit(e);
    }

    private static string HashDataDir(string dataDir)
    {
        var normalized = Path.GetFullPath(dataDir).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }
}

