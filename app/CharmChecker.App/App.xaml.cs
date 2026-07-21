using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Wpf.Ui.Appearance;

namespace CharmChecker.App;

public partial class App : Application
{
    private Mutex? _instanceLock;
    private bool _ownsInstanceLock;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mutexName = "MHWildsCharmChecker_" + HashDataDir(AppDomain.CurrentDomain.BaseDirectory);
        _instanceLock = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
        _ownsInstanceLock = createdNew;
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
        // initiallyOwned:trueで所有権を得るのはMutexを新規作成した場合のみ。多重起動を検知した
        // 側(createdNew==false)は既存Mutexを開いただけで所有していないため、ReleaseMutex()を
        // 呼ぶとApplicationExceptionを投げ、後続のDispose()/base.OnExit(e)が実行されなくなる
        if (_ownsInstanceLock)
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

