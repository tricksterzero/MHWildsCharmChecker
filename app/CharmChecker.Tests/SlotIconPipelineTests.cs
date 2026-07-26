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
    public void ReadSlots_IconAdjacentToFusedBlob_RecoversFirstSlot()
    {
        // 鑑定BOX画面の単一「装備詳細」パネル。1つ目のスロットアイコンがバーの縁取りと
        // 融合するが、2つ目のアイコン(確定フレーム)はこの塊に含まれず単に隣接するだけの
        // パターン(正: 武器1+防具[1,0,0] / 修正前の誤: 武器1+防具[0,0,0])。
        // 塊の境界自体はCannyの融合具合で伸縮し不安定なため、隣接する確定フレームの端を
        // 起点にした固定幅の窓で再探索して回復する。
        var path = Path.Combine(TestPaths.FindAssetsDir(), "case6 appraisal box", "20250906064316_1.jpg");
        var (armor, weapon) = CharmChecker.App.MainWindow.ReadSlots(path, hasWeaponSlot: true);

        Assert.Equal(new List<int> { 1, 0, 0 }, armor);
        Assert.Equal(new List<int> { 1, 0, 0 }, weapon);
    }

    [Fact]
    public void ReadSlots_BoxUndercountsDetail_StillPrefersBoxOverSpuriousDetailRegion()
    {
        // 真の21:9(3440x1440)のマカ錬金鑑定結果画面。BOX領域は正しく1枠(Lv3)のみ検出するが、
        // Detail領域(x:1400-1650)は護石の背景テクスチャ(木目模様)を2枠の偽陽性として誤検出し、
        // 検出数がBOX側(1件)を上回ってしまう(2件)。修正前は「検出数が多い方を採用」する
        // ロジックのため誤ってDetail側(防具[3,1,0])が採用されていた回帰テスト
        // (正: 防具[3,0,0]。BOX領域は1件でも検出できていれば常に信頼すべきで、
        // Detail領域の検出数が上回ることは根拠にならない)。
        var path = Path.Combine(TestPaths.FindAssetsDir(), "option 21_9 native craft result", "205B741.JPG");
        var (armor, weapon) = CharmChecker.App.MainWindow.ReadSlots(path, hasWeaponSlot: false);

        Assert.Equal(new List<int> { 3, 0, 0 }, armor);
        Assert.Equal(new List<int> { 0, 0, 0 }, weapon);
    }

    [Theory]
    [InlineData("20260726181601_1.jpg", true)]
    [InlineData("20260726181637_1.jpg", false)]
    [InlineData("20260726181828_1.jpg", false)]
    public void ReadSlots_DecorationEquipped_ThrowsAndExcludesWholeCharm(string fileName, bool hasWeaponSlot)
    {
        // 装飾品装着済みソケットは菱形が実体色で塗りつぶされ2次元形状になり、ClassifyLevelの
        // 列プロファイル方式(2次元形状を1次元に潰す)ではレベル誤判定を起こす
        // (実機ユーザー報告3件、2026-07-27。詳細な原因分析はCLAUDE.md参照)。
        // 2次元ベースの専用分類器を新設するコストとサンプル数(3枚)を鑑み、ユーザーの判断で
        // 装飾品装着済みスロットを含む護石は読み取り対象から除外する仕様とした。
        var path = Path.Combine(TestPaths.FindAssetsDir(), "option 21_9 native", fileName);

        Assert.Throws<DecorationEquippedException>(
            () => CharmChecker.App.MainWindow.ReadSlots(path, hasWeaponSlot));
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
