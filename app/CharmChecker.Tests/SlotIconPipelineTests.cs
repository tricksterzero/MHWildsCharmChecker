using CharmChecker.Core.SlotIcon;
using OpenCvSharp;

namespace CharmChecker.Tests;

/// <summary>
/// スロットアイコン検出+レベル判定のend-to-endテスト。
/// BOXパネル(2護石比較画面の右側)とDetailパネル(単一護石詳細画面)の両方を対象とする。
/// 種別(武器/防具)の判定は護石名ベースで行うため、ここではレベルのみ検証する。
/// </summary>
public class SlotIconPipelineTests
{
    public static IEnumerable<object[]> BoxPanelCases()
    {
        yield return new object[] { "20260614061312_1.jpg", new[] { SlotLevel.Lv3 } };
        yield return new object[] { "20260614061337_1.jpg", new[] { SlotLevel.Lv3 } };
        yield return new object[] { "20260614061353_1.jpg", new[] { SlotLevel.Lv2, SlotLevel.Lv1 } };
        yield return new object[] { "20260614061357_1.jpg", new[] { SlotLevel.Lv2, SlotLevel.Lv1 } };
        yield return new object[] { "20260614061411_1.jpg", new[] { SlotLevel.Lv2 } };
        yield return new object[] { "20260614061416_1.jpg", new[] { SlotLevel.Lv2 } };
        yield return new object[] { "20260614061441_1.jpg", new[] { SlotLevel.Lv1, SlotLevel.Lv1, SlotLevel.Lv1 } };
        yield return new object[] { "20260614061456_1.jpg", new[] { SlotLevel.Lv1, SlotLevel.Lv1, SlotLevel.Lv1 } };
        yield return new object[] { "20260614022239_1.jpg", new[] { SlotLevel.Lv3 } };
    }

    public static IEnumerable<object[]> DetailPanelCases()
    {
        yield return new object[] { "20260604111556_1.jpg", new[] { SlotLevel.Lv1, SlotLevel.Lv1, SlotLevel.Lv1 } };
        yield return new object[] { "20260612192419_1.jpg", new[] { SlotLevel.Lv1 } };
    }

    [Theory]
    [MemberData(nameof(BoxPanelCases))]
    public void BoxPanel_DetectAndClassifyLevel(string fileName, SlotLevel[] expected)
    {
        var path = Path.Combine(TestPaths.FindAssetsDir(), fileName);
        using var img = Cv2.ImRead(path);
        var (sx, sy) = SlotIconAnalyzer.ScaleFactors(img);

        using var region = new Mat(img, SlotIconAnalyzer.PanelRegion(img));
        using var gray = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);

        var frames = SlotIconAnalyzer.DetectFrames(gray, sx, sy);
        Assert.Equal(expected.Length, frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            var result = SlotIconAnalyzer.ClassifyLevel(gray, frames[i]);
            Assert.Equal(expected[i], result.Level);
        }
    }

    [Theory]
    [MemberData(nameof(DetailPanelCases))]
    public void DetailPanel_DetectAndClassifyLevel(string fileName, SlotLevel[] expected)
    {
        var path = Path.Combine(TestPaths.FindAssetsDir(), fileName);
        using var img = Cv2.ImRead(path);
        var (sx, sy) = SlotIconAnalyzer.ScaleFactors(img);

        using var region = new Mat(img, SlotIconAnalyzer.DetailPanelRegion(img));
        using var gray = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);

        var frames = SlotIconAnalyzer.DetectFrames(gray, sx, sy);
        Assert.Equal(expected.Length, frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            var result = SlotIconAnalyzer.ClassifyLevel(gray, frames[i]);
            Assert.Equal(expected[i], result.Level);
        }
    }

    [Theory]
    [InlineData("20260614061441_1.jpg", 3, 0)]
    [InlineData("20260614061353_1.jpg", 2, 1)]
    [InlineData("20260604111556_1.jpg", 0, 3)]
    public void DualRegion_PicksRegionWithMoreFrames(string fileName, int expectedBox, int expectedDetail)
    {
        var path = Path.Combine(TestPaths.FindAssetsDir(), fileName);
        using var img = Cv2.ImRead(path);
        var (sx, sy) = SlotIconAnalyzer.ScaleFactors(img);

        using var boxRegion = new Mat(img, SlotIconAnalyzer.PanelRegion(img));
        using var boxGray = new Mat();
        Cv2.CvtColor(boxRegion, boxGray, ColorConversionCodes.BGR2GRAY);
        var boxFrames = SlotIconAnalyzer.DetectFrames(boxGray, sx, sy);

        using var detRegion = new Mat(img, SlotIconAnalyzer.DetailPanelRegion(img));
        using var detGray = new Mat();
        Cv2.CvtColor(detRegion, detGray, ColorConversionCodes.BGR2GRAY);
        var detFrames = SlotIconAnalyzer.DetectFrames(detGray, sx, sy);

        Assert.Equal(expectedBox, boxFrames.Count);
        Assert.Equal(expectedDetail, detFrames.Count);

        var chosen = boxFrames.Count > detFrames.Count ? boxFrames : detFrames;
        Assert.Equal(Math.Max(expectedBox, expectedDetail), chosen.Count);
    }

    [Fact]
    public void ReadSlots_TiedFrameCount_PrefersBoxRegionOverSpuriousDetailRegion()
    {
        // 鑑定BOXプレビュー画面ではBOX領域・Detail領域とも2枠を検出し同数になるが、
        // Detail領域(x:1400-1650)側はこの画面では護石画像プレビュー部分を誤検出しており、
        // BOX領域の検出結果(Lv2+Lv1、正解)が採用されるべき回帰テスト
        var path = Path.Combine(TestPaths.FindAssetsDir(), "case5 craft result", "20260615054704_1.jpg");
        var (armor, weapon) = CharmChecker.App.MainWindow.ReadSlots(path, hasWeaponSlot: false);

        Assert.Equal(new List<int> { 2, 1, 0 }, armor);
        Assert.Equal(new List<int> { 0, 0, 0 }, weapon);
    }

    [Fact]
    public void ReadSlots_TiedSingleFrame_PrefersBoxRegionOverSpuriousDetailRegion()
    {
        // 同種の同数タイ問題の別ケース: BOX・Detailとも1枠ずつ検出するが、
        // Detail領域側がLv3と誤判定する（正解はBOX領域のLv1）
        var path = Path.Combine(TestPaths.FindAssetsDir(), "case5 craft result", "20260527222641_1.jpg");
        var (armor, weapon) = CharmChecker.App.MainWindow.ReadSlots(path, hasWeaponSlot: false);

        Assert.Equal(new List<int> { 1, 0, 0 }, armor);
        Assert.Equal(new List<int> { 0, 0, 0 }, weapon);
    }

    [Fact]
    public void ReadSlots_AdjacentIconFusedWithBarBorder_RecoversFirstSlot()
    {
        // 「ベースキャンプメニュー>装備変更」の2護石比較画面。1つ目のスロットアイコンが
        // スロットバーの縁取り(装飾UI)とCannyの輪郭検出で融合し、幅が正常範囲を大きく
        // 超えた1つの塊として検出され、本来2つあるはずのスロットが1つ(2つ目のみ)しか
        // 検出されなかった回帰テスト(正: 防具[2,1,0] / 修正前の誤: 防具[1,0,0])。
        // TryRecoverFusedFrameが、確定済みの2つ目のアイコンを手掛かりに、融合した塊の
        // 未検出側だけを狭い窓で再度Canny検出することで1つ目のLv2を回復する。
        var path = Path.Combine(TestPaths.FindAssetsDir(), "case2 equip change", "20260506085633_1.jpg");
        var (armor, weapon) = CharmChecker.App.MainWindow.ReadSlots(path, hasWeaponSlot: false);

        Assert.Equal(new List<int> { 2, 1, 0 }, armor);
        Assert.Equal(new List<int> { 0, 0, 0 }, weapon);
    }

    [Fact]
    public void ClassifyLevel_UniformCrop_ReturnsUnknown()
    {
        using var gray = new Mat(100, 50, MatType.CV_8UC1, Scalar.All(0));
        var frame = new Rect(0, 0, 50, 100);

        var result = SlotIconAnalyzer.ClassifyLevel(gray, frame);

        Assert.Equal(SlotLevel.Unknown, result.Level);
    }
}
