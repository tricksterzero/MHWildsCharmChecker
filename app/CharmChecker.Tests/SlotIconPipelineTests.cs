using CharmChecker.Core.SlotIcon;
using OpenCvSharp;

namespace CharmChecker.Tests;

/// <summary>
/// legacy/slot-icon-pipeline/pipeline.py のend-to-endテスト（9パネル全て正解）の移植。
/// 「装備の確認・売却」画面の装備BOX側(右パネル)のスロットアイコンを対象とする。
/// </summary>
public class SlotIconPipelineTests
{
    private static readonly (Mat RefBuki, Mat RefBougu) Refs =
        SlotIconAnalyzer.BuildRefs(TestPaths.FindAssetsDir());

    public static IEnumerable<object[]> PanelCases()
    {
        yield return new object[] { "20260614061312_1.jpg", new[] { (SlotLevel.Lv3, SlotType.Armor) } };
        yield return new object[] { "20260614061337_1.jpg", new[] { (SlotLevel.Lv3, SlotType.Armor) } };
        yield return new object[] { "20260614061353_1.jpg", new[] { (SlotLevel.Lv2, SlotType.Armor), (SlotLevel.Lv1, SlotType.Armor) } };
        yield return new object[] { "20260614061357_1.jpg", new[] { (SlotLevel.Lv2, SlotType.Armor), (SlotLevel.Lv1, SlotType.Armor) } };
        yield return new object[] { "20260614061411_1.jpg", new[] { (SlotLevel.Lv2, SlotType.Armor) } };
        yield return new object[] { "20260614061416_1.jpg", new[] { (SlotLevel.Lv2, SlotType.Armor) } };
        yield return new object[] { "20260614061441_1.jpg", new[] { (SlotLevel.Lv1, SlotType.Weapon), (SlotLevel.Lv1, SlotType.Armor) } };
        yield return new object[] { "20260614061456_1.jpg", new[] { (SlotLevel.Lv1, SlotType.Weapon), (SlotLevel.Lv1, SlotType.Armor) } };
        yield return new object[] { "20260614022239_1.jpg", new[] { (SlotLevel.Lv3, SlotType.Armor) } };
    }

    [Theory]
    [MemberData(nameof(PanelCases))]
    public void RightPanel_SlotIcons_AreClassifiedCorrectly(string fileName, (SlotLevel Level, SlotType Type)[] expected)
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
            var levelResult = SlotIconAnalyzer.ClassifyLevel(gray, frames[i]);
            using var badge = SlotIconAnalyzer.ExtractBadge(region, frames[i], sx, sy);
            var typeResult = SlotIconAnalyzer.ClassifyType(badge, Refs.RefBuki, Refs.RefBougu);

            Assert.Equal(expected[i].Level, levelResult.Level);
            Assert.Equal(expected[i].Type, typeResult.Type);
        }
    }
}
