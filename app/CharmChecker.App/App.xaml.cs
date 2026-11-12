using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace CharmChecker.App;

public partial class App : Application
{
    private Mutex? _instanceLock;
    private bool _ownsInstanceLock;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 下のDispatcherUnhandledExceptionで捕まえない未処理例外(UIスレッド以外の例外と、メインウィンドウを表示するまでの
        // 起動中の例外)は、プロセスが終了するのを止められないため、記録だけ残す(起動中のUIスレッドの例外でもここが
        // 呼ばれることを2026-11-12に最小のWPFアプリで確認)
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ErrorLogger.Log("Unhandled", "未処理の例外でアプリを終了します(UIスレッド以外、または起動中の例外)。",
                args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));

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

        // テーマ自体は設定(settings.json)を読むMainWindowのコンストラクタで適用する
        ThemeService.Initialize();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        // 画面の操作中に捕まえていない例外が出てもアプリを終了させず、error.logに記録して知らせる。
        // 護石データは変更のたびに保存しているため、続けても失われるものは少ない(2026-11-08)。
        // 登録はメインウィンドウを表示した後にする。起動中(MainWindowの生成・表示)の例外まで続行させると、
        // ウィンドウの無いプロセスが多重起動防止のMutexを持ったまま残り、次の起動が拒否されるため
        // (2026-11-12、最小のWPFアプリで、登録後にウィンドウの生成で例外を投げるとプロセスが残ることを確認)
        DispatcherUnhandledException += OnDispatcherUnhandledException;
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

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLogger.Log("Unhandled", "画面の操作中に未処理の例外が発生しました。", e.Exception);
        MessageBox.Show(
            $"予期しないエラーが発生しました。操作は完了していない可能性があります。\n詳細は error.log に記録しました。\n\n{e.Exception.Message}",
            "エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static string HashDataDir(string dataDir)
    {
        var normalized = Path.GetFullPath(dataDir).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }
}

