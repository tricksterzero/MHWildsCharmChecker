using CharmChecker.Core.Ocr;

namespace CharmChecker.Tests;

public class TextOcrSmokeTests
{
    [Fact]
    public async Task RecognizeAsync_ReadsSkillNamesFromKnownAsset()
    {
        var path = Path.Combine(TestPaths.FindAssetsDir(), "20260604111556_1.jpg");

        var result = await TextOcrReader.RecognizeAsync(path);

        // Windows.Media.Ocrは漢字を1文字ずつ別の単語として認識し、Line.Textは文字間に
        // 半角スペースを挟んで結合するため、比較前にスペースを取り除く。
        var text = result.Text.Replace(" ", "");

        Assert.NotEmpty(result.Lines);
        Assert.Contains("栄世の護石", text);
        Assert.Contains("攻撃", text);
        Assert.Contains("業物", text);
        Assert.Contains("攻勢", text);
    }
}
