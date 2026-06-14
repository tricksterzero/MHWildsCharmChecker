using OpenCvSharp;

namespace CharmChecker.Tests;

public class OpenCvSmokeTests
{
    [Fact]
    public void ImRead_LoadsAssetImage_WithExpectedResolution()
    {
        var path = FindAssetImage("20260604111556_1.jpg");

        using var mat = Cv2.ImRead(path);

        Assert.False(mat.Empty());
        Assert.Equal(2560, mat.Width);
        Assert.Equal(1440, mat.Height);
    }

    // assets/はリポジトリ非追跡(.gitignore対象)のローカル検証用画像のため、
    // テスト実行ディレクトリから上方向にリポジトリルートを探索して参照する。
    private static string FindAssetImage(string fileName)
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

        return Path.Combine(dir.FullName, "assets", fileName);
    }
}
