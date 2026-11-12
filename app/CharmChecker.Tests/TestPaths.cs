namespace CharmChecker.Tests;

/// <summary>
/// リポジトリ内のフォルダを見つけるための共通ヘルパー。
/// テスト実行ディレクトリから上方向にたどり、指定した名前のフォルダを持つ最初のディレクトリを探す。
/// </summary>
internal static class TestPaths
{
    /// <summary>assets/(リポジトリ非追跡、ローカル検証用画像)のフォルダ。</summary>
    public static string FindAssetsDir() =>
        FindDirUpward("assets", "assets フォルダが見つかりません(ローカル検証用画像が未配置の可能性)。");

    /// <summary>resources/(skill-decoration-map.json等のデータファイル)のフォルダ。</summary>
    public static string FindResourcesDir() =>
        FindDirUpward("resources", "resources フォルダが見つかりません。");

    private static string FindDirUpward(string name, string notFoundMessage)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, name)))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException(notFoundMessage);
        }

        return Path.Combine(dir.FullName, name);
    }
}
