using CharmChecker.Core.Image;
using OpenCvSharp;

namespace CharmChecker.Tests;

/// <summary>上下の黒帯の検出(行の平均輝度が8以下を黒帯とみなす)のテスト。</summary>
public class LetterboxNormalizerTests
{
    /// <summary>全体をbodyの明るさで塗り、上下の指定行数だけbandの明るさにした画像を作る。</summary>
    private static Mat MakeImage(int height, int topRows, int bottomRows, int body = 100, int band = 0, int width = 64)
    {
        var mat = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.All(body));
        if (topRows > 0)
            mat[new Rect(0, 0, width, topRows)].SetTo(Scalar.All(band));
        if (bottomRows > 0)
            mat[new Rect(0, height - bottomRows, width, bottomRows)].SetTo(Scalar.All(band));
        return mat;
    }

    [Fact]
    public void NoLetterbox_ReturnsWholeImageAndScaleOne()
    {
        using var img = MakeImage(1440, 0, 0);
        Assert.Equal((0, 1439, 1.0), LetterboxNormalizer.DetectContentBounds(img));
    }

    [Fact]
    public void Letterbox_ReturnsContentRowsAndScale()
    {
        // WQHDモニタ+ゲーム内21:9設定と同じ形(上171行・下172行が黒、コンテンツ1097行)
        using var img = MakeImage(1440, 171, 172);
        var (top, bottom, scale) = LetterboxNormalizer.DetectContentBounds(img);
        Assert.Equal(171, top);
        Assert.Equal(1267, bottom);
        Assert.Equal(1097.0 / 1440.0, scale, 10);
    }

    [Theory]
    [InlineData(8, 10)]  // 境界値: 平均輝度8ちょうどは黒帯とみなす
    [InlineData(9, 0)]   // 9は黒帯とみなさない
    public void RowMeanThreshold_Boundary(int bandBrightness, int expectedTop)
    {
        using var img = MakeImage(1440, 10, 0, band: bandBrightness);
        Assert.Equal(expectedTop, LetterboxNormalizer.DetectContentBounds(img).Top);
    }

    [Fact]
    public void AllBlackImage_TreatedAsNoLetterbox()
    {
        // すべての行が暗い画像は黒帯と区別できないため、画像全体を返し倍率を正に保つ
        using var img = MakeImage(1440, 0, 0, body: 0);
        Assert.Equal((0, 1439, 1.0), LetterboxNormalizer.DetectContentBounds(img));
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LetterboxNormalizer.DetectContentBounds(null!));
        using var empty = new Mat();
        Assert.Throws<ArgumentException>(() => LetterboxNormalizer.DetectContentBounds(empty));
        using var gray = new Mat(new Size(10, 10), MatType.CV_8UC1, Scalar.All(100));
        Assert.Throws<ArgumentException>(() => LetterboxNormalizer.DetectContentBounds(gray));
    }
}
