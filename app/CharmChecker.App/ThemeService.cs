using System.Windows;
using Wpf.Ui.Appearance;

namespace CharmChecker.App;

/// <summary>設定画面で選べるテーマ。</summary>
public enum AppThemeMode
{
    /// <summary>Windowsの設定(ダーク/ライト)に合わせる。既定値。</summary>
    System,
    Light,
    Dark,
}

/// <summary>
/// WPF-UIのテーマ(ApplicationThemeManager)と、アプリ独自の色定義(Palette/Dark.xaml・Light.xaml)を
/// 揃えて切り替える。独自の色定義はApplicationThemeManager.Changedを受けて差し替えるため、
/// 設定画面からの切り替えでも、SystemThemeWatcherによるWindowsの設定変更への追従でも同じ経路で揃う。
/// </summary>
public static class ThemeService
{
    private static readonly Uri DarkPaletteUri = new("pack://application:,,,/MHWildsCharmChecker;component/Palette/Dark.xaml");
    private static readonly Uri LightPaletteUri = new("pack://application:,,,/MHWildsCharmChecker;component/Palette/Light.xaml");

    private static ResourceDictionary? _palette;
    private static bool _watching;

    /// <summary>
    /// App.xamlに登録済みの独自色定義を掴み、WPF-UIのテーマ変更通知を購読する。
    /// 最初のApplyより前に呼ぶこと(App.xamlの初期テーマはChangedを発行しないため、ここで現在の
    /// テーマに合わせて独自色定義も一度揃える)。
    /// </summary>
    public static void Initialize()
    {
        _palette = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("/Palette/", StringComparison.OrdinalIgnoreCase));
        ApplicationThemeManager.Changed += (theme, _) => SetPalette(theme);
        SetPalette(ApplicationThemeManager.GetAppTheme());
    }

    /// <summary>
    /// テーマを適用する。System ではWindowsの設定を適用したうえで、以後のWindowsの設定変更に追従するよう
    /// メインウィンドウを監視する。Light/Dark では監視を外して明示したテーマを適用する。
    /// </summary>
    public static void Apply(AppThemeMode mode, Window mainWindow)
    {
        if (mode == AppThemeMode.System)
        {
            // SystemThemeWatcher.Watchは同じウィンドウを重ねて登録すると多重にフックするため、自前で状態を持つ
            if (!_watching)
            {
                SystemThemeWatcher.Watch(mainWindow);
                _watching = true;
            }
            ApplicationThemeManager.ApplySystemTheme();
            return;
        }

        // UnWatchは表示済み(Loaded)のウィンドウにしか使えない。未表示なら監視はまだ始まっていない
        // (Watchは未表示のウィンドウに対してはLoaded時に登録を予約する)ため、表示後に外す
        if (_watching)
        {
            if (mainWindow.IsLoaded)
                SystemThemeWatcher.UnWatch(mainWindow);
            else
                mainWindow.Loaded += (_, _) => SystemThemeWatcher.UnWatch(mainWindow);
            _watching = false;
        }
        ApplicationThemeManager.Apply(mode == AppThemeMode.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    /// <summary>設定ファイル上の文字列表現("system"/"light"/"dark")との相互変換。不明な値はSystem。</summary>
    public static AppThemeMode Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "light" => AppThemeMode.Light,
        "dark" => AppThemeMode.Dark,
        _ => AppThemeMode.System,
    };

    public static string ToSettingString(AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => "light",
        AppThemeMode.Dark => "dark",
        _ => "system",
    };

    private static void SetPalette(ApplicationTheme theme)
    {
        if (_palette is null) return;
        // ハイコントラスト(Windowsの設定に合わせる場合のみ起こりうる)はライトの色定義で代用する(未検証)
        var uri = theme == ApplicationTheme.Dark ? DarkPaletteUri : LightPaletteUri;
        if (_palette.Source == uri) return;

        // Sourceの書き換えではなくMergedDictionaries内の要素ごと置き換える(WPF-UIのResourceDictionaryManagerと
        // 同じ方式)。コレクションの変更としてDynamicResourceの参照先に確実に反映させるため
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        int index = dictionaries.IndexOf(_palette);
        if (index < 0) return;
        var next = new ResourceDictionary { Source = uri };
        dictionaries[index] = next;
        _palette = next;
    }
}
