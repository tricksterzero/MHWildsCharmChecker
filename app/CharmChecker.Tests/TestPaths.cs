namespace CharmChecker.Tests;

/// <summary>
/// assets/(リポジトリ非追跡、ローカル検証用画像)を見つけるための共通ヘルパー。
/// テスト実行ディレクトリから上方向にリポジトリルートを探索する。
/// </summary>
internal static class TestPaths
{
    public static string FindAssetsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "assets")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException("assets フォルダが見つかりません(ローカル検証用画像が未配置の可能性)。");
        }

        return Path.Combine(dir.FullName, "assets");
    }
}
