using CharmChecker.Core.Skill;
using OpenCvSharp;

namespace CharmChecker.Tests;

/// <summary>スキル欄の切り出しから作る前処理バリエーション(元の5種+1.5倍に拡大した5種)のテスト。</summary>
public class ImageVariantFactoryTests
{
    private static Mat MakeCrop(int width = 470, int height = 390) =>
        new(new Size(width, height), MatType.CV_8UC3, Scalar.All(80));

    /// <summary>cropからバリエーションを作ってassertに渡し、終わったら各画像を解放する。</summary>
    private static void WithVariants(Mat crop, Action<List<ImageVariantFactory.Variant>> assert)
    {
        var variants = ImageVariantFactory.Create(crop);
        try
        {
            assert(variants);
        }
        finally
        {
            foreach (var v in variants) v.Image.Dispose();
        }
    }

    [Fact]
    public void Create_ReturnsOriginalFiveThenScaledFive()
    {
        using var crop = MakeCrop();
        WithVariants(crop, variants =>
            Assert.Equal(
                ["raw", "trim60", "otsu", "trim60_otsu", "gray", "raw_x1.5", "trim60_x1.5", "otsu_x1.5", "trim60_otsu_x1.5", "gray_x1.5"],
                variants.Select(v => v.Name).ToArray()));
    }

    [Fact]
    public void Create_OriginalVariants_KeepCoordinateScaleOneAndThresholds()
    {
        using var crop = MakeCrop();
        WithVariants(crop, variants =>
        {
            var original = variants.Take(5).ToList();
            Assert.All(original, v => Assert.Equal(1.0, v.CoordinateScale));
            Assert.Equal([300, 240, 300, 240, 300], original.Select(v => v.LvXThreshold).ToArray());
            Assert.Equal(crop.Cols, original[0].Image.Cols);
            Assert.Equal(crop.Cols - 60, original[1].Image.Cols);
        });
    }

    [Fact]
    public void Create_ScaledVariants_AreEnlargedWithScaledThresholds()
    {
        // 拡大した画像での座標を元の切り出しの座標に戻すため、CoordinateScaleに拡大率を持たせる。
        // Lvの左右を分ける境界(LvXThreshold)は拡大後の画像の座標で持つ
        using var crop = MakeCrop();
        WithVariants(crop, variants =>
        {
            var scaled = variants.Skip(5).ToList();
            Assert.All(scaled, v => Assert.Equal(1.5, v.CoordinateScale));
            Assert.Equal([450, 360, 450, 360, 450], scaled.Select(v => v.LvXThreshold).ToArray());
            Assert.Equal(705, scaled[0].Image.Cols);
            Assert.Equal(585, scaled[0].Image.Rows);
            Assert.Equal(705 - 60, scaled[1].Image.Cols);
        });
    }

    [Fact]
    public void Create_CropNarrowerThanTrimWidth_SkipsTrimVariants()
    {
        // 境界値: 幅60px以下ならトリミング系を作らない(拡大後は90pxで60pxを超えるため拡大側は作る)
        using var crop = MakeCrop(width: 60, height: 50);
        WithVariants(crop, variants =>
            Assert.Equal(["raw", "otsu", "gray", "raw_x1.5", "trim60_x1.5", "otsu_x1.5", "trim60_otsu_x1.5", "gray_x1.5"],
                variants.Select(v => v.Name).ToArray()));
    }

    [Fact]
    public void Create_InvalidCrop_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ImageVariantFactory.Create(null!));
        using var empty = new Mat();
        Assert.Throws<ArgumentException>(() => ImageVariantFactory.Create(empty));
        using var gray = new Mat(new Size(100, 100), MatType.CV_8UC1, Scalar.All(0));
        Assert.Throws<ArgumentException>(() => ImageVariantFactory.Create(gray));
    }
}
