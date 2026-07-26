using System.Text;
using CharmChecker.Core.SlotIcon;
using OpenCvSharp;

namespace CharmChecker.Tests;

/// <summary>
/// EmptySlotTemplates.cs(app/CharmChecker.Core/SlotIcon/)を実データから再生成する手動実行専用ツール。
/// 通常のテスト実行(dotnet test)には含まれない(Skip指定)。新しい画面パターンで空スロットの
/// 誤判定(閾値超過による過検出)が見つかった場合、その画面の空スロットサンプルをAddXxxで追加し、
/// `dotnet test --filter FullyQualifiedName~EmptySlotTemplateGenerator` で再生成する。
///
/// 各Lvのテンプレートは画面パターンごとに複数件を個別保持し、最近傍(最小差分)方式でマッチさせる
/// 設計(MatchEmptyTemplate参照)。画面ごとに背景色が異なり、単純平均するとどの画面にも合わない
/// 中間値になり精度が落ちることを実データで確認済み(2026-07-27)。
///
/// **既知の限界**(leave-one-out検証で判明、2026-07-27、Codexの指摘): 類似した画面パターンの
/// サンプルが複数ある場合はdiffが0.0〜2.7程度に収まるが、類似サンプルが無い「孤立した」画面
/// パターン(単一サンプルしかないDetailパネル画面、鑑定BOXの武器スロット等)はleave-one-out差分が
/// 5.4〜38.0まで跳ね上がり、装着ケースの最小値(6.71)を上回ることがある。つまり閾値
/// (SlotIconConstants.EmptyTemplateThreshold)が保証するのは「このテンプレート集合に十分近い
/// 画面パターンのサンプルが複数存在する場合」のみ。新しい画面パターンの空スロットは、都度
/// サンプルを追加してテンプレートを拡充するのが正攻法。
/// </summary>
public class EmptySlotTemplateGenerator
{
    private const string OutputPath =
        @"C:\File\Program\MHWildsCharmChecker\app\CharmChecker.Core\SlotIcon\EmptySlotTemplates.cs";

    private record Source(string RelPath, bool UseDetailPanel, int X, int Y, int W, int H, SlotLevel Level, string Note);

    /// <summary>
    /// テンプレートの出典一覧(provenance)。全てassets/(TestPaths.FindAssetsDir())配下の画像から、
    /// 検出済みまたは実測済みのフレーム座標(パネル領域相対px、基準解像度2560x1440)を指定する。
    /// </summary>
    private static readonly Source[] Sources =
    [
        // --- case7 decoration check: 「装備変更」2護石比較画面、Lv3/Lv2スロット×装飾品有無 ---
        new(@"case7 decoration check\20260727071034_1.jpg", false, 145, 78, 50, 38, SlotLevel.Lv3, "Lv3スロット空(1)"),
        new(@"case7 decoration check\20260727070755_1.jpg", false, 145, 78, 50, 38, SlotLevel.Lv3, "Lv3スロット空(2)"),
        new(@"case7 decoration check\20260727071237_1.jpg", false, 146, 78, 49, 38, SlotLevel.Lv2, "Lv2スロット空(1つ目のスロット)"),
        new(@"case7 decoration check\20260727071132_1.jpg", false, 199, 83, 38, 33, SlotLevel.Lv1, "Lv1スロット空(2つ目のスロット、その1)"),
        new(@"case7 decoration check\20260727071210_1.jpg", false, 199, 83, 38, 33, SlotLevel.Lv1, "Lv1スロット空(2つ目のスロット、その2)"),
        new(@"case7 decoration check\20260727071237_1.jpg", false, 199, 83, 38, 33, SlotLevel.Lv1, "Lv1スロット空(2つ目のスロット、その3)"),

        // --- 既存資産(BoxPanelCases/DetailPanelCasesの検出フレームをそのまま採用) ---
        // ※ これらはDetectFramesの検出結果から取るため、下のGenerateメソッド内で別途動的に追加する
        //   (画面ごとに検出座標が変わりうるため、決め打ち座標ではなく実行時にDetectFramesを呼ぶ)。

        // --- 個別の既存ケース(実測済み座標) ---
        new(@"case5 craft result\20260527222641_1.jpg", false, 146, 83, 40, 34, SlotLevel.Lv1, "旧screen(craft result)防具スロット空"),
        new(@"case6 appraisal box\20250906064316_1.jpg", true, 145, 53, 39, 33, SlotLevel.Lv1, "鑑定BOX画面 防具スロット空"),
        new(@"case6 appraisal box\20250906064316_1.jpg", true, 90, 49, 49, 37, SlotLevel.Lv1, "鑑定BOX画面 武器スロット空(防具スロットと見た目が異なる専用テンプレート)"),
        new(@"option 21_9 native craft result\205B741.JPG", false, 105, 78, 50, 38, SlotLevel.Lv3, "真の21:9ネイティブ マカ錬金鑑定結果 防具スロット空"),
    ];

    [Fact(Skip = "手動実行専用: EmptySlotTemplates.csを実データから再生成するツール。実行するとリポジトリのソースファイルを上書きする。新しい画面パターンを追加する場合のみ、フィルタ指定で明示的に実行すること。")]
    public void Generate()
    {
        var samples = new Dictionary<SlotLevel, List<(byte[] Band, string Note)>>
        {
            [SlotLevel.Lv1] = [],
            [SlotLevel.Lv2] = [],
            [SlotLevel.Lv3] = [],
        };

        void AddSample(Mat color, Rect frame, SlotLevel level, string note)
        {
            int y0 = frame.Y + (int)(frame.Height * SlotIconConstants.LevelCropTopFraction);
            var bandRect = new Rect(frame.X, y0, frame.Width, frame.Y + frame.Height - y0);
            using var band = new Mat(color, bandRect);
            using var resized = new Mat();
            Cv2.Resize(band, resized, new Size(EmptySlotTemplates.TemplateWidth, EmptySlotTemplates.TemplateHeight));
            var flat = new byte[EmptySlotTemplates.TemplateWidth * EmptySlotTemplates.TemplateHeight * 3];
            int idx = 0;
            for (int y = 0; y < EmptySlotTemplates.TemplateHeight; y++)
                for (int x = 0; x < EmptySlotTemplates.TemplateWidth; x++)
                {
                    var px = resized.Get<Vec3b>(y, x);
                    flat[idx++] = px.Item0; flat[idx++] = px.Item1; flat[idx++] = px.Item2;
                }
            samples[level].Add((flat, note));
        }

        foreach (var s in Sources)
        {
            var path = Path.Combine(TestPaths.FindAssetsDir(), s.RelPath);
            using var img = Cv2.ImRead(path);
            var region = s.UseDetailPanel ? SlotIconAnalyzer.DetailPanelRegion(img) : SlotIconAnalyzer.PanelRegion(img);
            using var color = new Mat(img, region);
            AddSample(color, new Rect(s.X, s.Y, s.W, s.H), s.Level, $"{s.RelPath} ({s.Note})");
        }

        // 既存資産(BoxPanelCases/DetailPanelCases): DetectFramesの検出結果をそのまま採用
        void AddDetected(string relPath, bool useDetail, SlotLevel[] expected)
        {
            var path = Path.Combine(TestPaths.FindAssetsDir(), relPath);
            using var img = Cv2.ImRead(path);
            var (sx, sy) = SlotIconAnalyzer.ScaleFactors(img);
            var region = useDetail ? SlotIconAnalyzer.DetailPanelRegion(img) : SlotIconAnalyzer.PanelRegion(img);
            using var color = new Mat(img, region);
            using var gray = new Mat();
            Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
            var frames = SlotIconAnalyzer.DetectFrames(gray, sx, sy);
            if (frames.Count != expected.Length) return; // 検出数不一致はスキップ
            for (int i = 0; i < frames.Count; i++)
                AddSample(color, frames[i], expected[i], $"{relPath} (検出フレーム#{i})");
        }

        AddDetected("20260614061312_1.jpg", false, [SlotLevel.Lv3]);
        AddDetected("20260614061337_1.jpg", false, [SlotLevel.Lv3]);
        AddDetected("20260614061353_1.jpg", false, [SlotLevel.Lv2, SlotLevel.Lv1]);
        AddDetected("20260614061357_1.jpg", false, [SlotLevel.Lv2, SlotLevel.Lv1]);
        AddDetected("20260614061411_1.jpg", false, [SlotLevel.Lv2]);
        AddDetected("20260614061416_1.jpg", false, [SlotLevel.Lv2]);
        AddDetected("20260614061441_1.jpg", false, [SlotLevel.Lv1, SlotLevel.Lv1, SlotLevel.Lv1]);
        AddDetected("20260614061456_1.jpg", false, [SlotLevel.Lv1, SlotLevel.Lv1, SlotLevel.Lv1]);
        AddDetected("20260614022239_1.jpg", false, [SlotLevel.Lv3]);
        AddDetected("20260604111556_1.jpg", true, [SlotLevel.Lv1, SlotLevel.Lv1, SlotLevel.Lv1]);
        AddDetected("20260612192419_1.jpg", true, [SlotLevel.Lv1]);

        var sb = new StringBuilder();
        sb.AppendLine("// 自動生成: app/CharmChecker.Tests/EmptySlotTemplateGenerator.cs により生成。");
        sb.AppendLine("// 出典・座標・件数はそのファイルのSources配列およびAddDetected呼び出しを参照。");
        sb.AppendLine("// (2026-07-27、装飾品検知の再設計にあたり、装飾品の色・輪郭の閉じ方に依存しない");
        sb.AppendLine("// 下部三角マークのテンプレート照合方式へ移行した際に作成)");
        sb.AppendLine("namespace CharmChecker.Core.SlotIcon;");
        sb.AppendLine();
        sb.AppendLine("public static class EmptySlotTemplates");
        sb.AppendLine("{");
        sb.AppendLine("    public const int TemplateWidth = 40;");
        sb.AppendLine("    public const int TemplateHeight = 20;");
        sb.AppendLine();
        foreach (var level in new[] { SlotLevel.Lv1, SlotLevel.Lv2, SlotLevel.Lv3 })
        {
            var list = samples[level];
            sb.AppendLine($"    // {level} empty templates (BGR, row-major, 40x20)、{list.Count}件:");
            foreach (var (_, note) in list)
                sb.AppendLine($"    //   - {note}");
            sb.AppendLine($"    public static readonly byte[][] {level} = [");
            foreach (var (flat, _) in list)
                sb.AppendLine($"        [{string.Join(",", flat)}],");
            sb.AppendLine("    ];");
            sb.AppendLine();
        }
        sb.AppendLine("}");

        File.WriteAllText(OutputPath, sb.ToString());

        Assert.True(samples[SlotLevel.Lv1].Count > 0, "Lv1テンプレートが1件も生成されなかった");
        Assert.True(samples[SlotLevel.Lv2].Count > 0, "Lv2テンプレートが1件も生成されなかった");
        Assert.True(samples[SlotLevel.Lv3].Count > 0, "Lv3テンプレートが1件も生成されなかった");
    }
}
