using OpenCvSharp;
using Windows.Media.Ocr;
using CharmChecker.Core.Ocr;

namespace CharmChecker.Core.Skill;

/// <summary>
/// スクリーンショットからスキル名・Lvを読み取るパイプライン。
/// Python PoC (skill_ocr_test.py) の移植。
/// </summary>
public static class SkillReadingPipeline
{
    private static readonly Rect SkillAreaRel = new(0, 310, 470, 390);

    /// <summary>
    /// スクリーンショットからスキル一覧を読み取る。
    /// 護石パネルが見つからない場合はnullを返す。
    /// </summary>
    public static async Task<IReadOnlyList<SkillEntry>?> ReadAsync(string imagePath, IReadOnlyList<string> knownSkills)
    {
        var result = await ReadWithMetadataAsync(imagePath, knownSkills);
        return result?.Skills;
    }

    /// <summary>
    /// スクリーンショットからスキル一覧と護石名を読み取る。
    /// 護石パネルが見つからない場合はnullを返す。
    /// </summary>
    public static async Task<SkillReadResult?> ReadWithMetadataAsync(string imagePath, IReadOnlyList<string> knownSkills)
    {
        var fullOcr = await TextOcrReader.RecognizeAsync(imagePath);
        var anchor = FindAnchor(fullOcr);
        if (anchor is null)
            return null;

        var (ax, ay) = anchor.Value;
        if (!IsCharmPanel(fullOcr, ax, ay))
            return null;

        var charmName = ExtractCharmName(fullOcr, ax, ay);

        using var img = Cv2.ImRead(imagePath);
        using var crop = CropSkillArea(img, ax, ay);
        if (crop is null) return null;

        var variants = ImageVariantFactory.Create(crop);
        try
        {
            var skills = await RunVariantsAndMerge(variants, knownSkills);
            return new SkillReadResult(skills, charmName);
        }
        finally
        {
            foreach (var v in variants)
                v.Image.Dispose();
        }
    }

    /// <summary>
    /// アンカーが指すパネル範囲内(IsCharmPanelと同じ相対位置)にある「〜の護石」を護石名として返す。
    /// 複数パネル(装備中側・BOX側等)が同一画面に写る場合、アンカーと無関係な護石名を誤って
    /// 拾わないよう、範囲外の候補は無視する。
    /// </summary>
    internal static string? ExtractCharmName(OcrResult ocrResult, double anchorX, double anchorY)
    {
        foreach (var line in ocrResult.Lines)
        {
            var text = line.Text.Replace(" ", "");
            if (!text.Contains("の護石"))
                continue;

            var x0 = line.Words.Min(w => w.BoundingRect.X);
            var y0 = line.Words.Min(w => w.BoundingRect.Y);
            var dx = x0 - anchorX;
            var dy = y0 - anchorY;
            if (dx >= -50 && dx <= 250 && dy >= 80 && dy <= 280)
            {
                var idx = text.IndexOf("の護石");
                return text[..idx] + "の護石";
            }
        }
        return null;
    }

    internal static (double X, double Y)? FindAnchor(OcrResult ocrResult)
    {
        // 3段フォールバック、最右優先
        var candidates = FindAnchorCandidates(ocrResult, "装備詳細", "装");
        if (candidates.Count > 0)
            return candidates.MaxBy(c => c.X);

        candidates = FindAnchorCandidates(ocrResult, "備詳細", "備");
        if (candidates.Count > 0)
            return candidates.MaxBy(c => c.X);

        candidates = FindSkillLabelAnchorCandidates(ocrResult);
        if (candidates.Count > 0)
            return candidates.MaxBy(c => c.X);

        return null;
    }

    private static List<(double X, double Y)> FindAnchorCandidates(OcrResult ocrResult, string pattern, string wordChar)
    {
        var candidates = new List<(double X, double Y)>();
        foreach (var line in ocrResult.Lines)
        {
            var lineText = line.Text.Replace(" ", "");
            if (!lineText.Contains(pattern))
                continue;

            var y0 = line.Words.Min(w => w.BoundingRect.Y);
            var matched = line.Words.FirstOrDefault(w => w.Text.Contains(wordChar));
            if (matched != null)
                candidates.Add((matched.BoundingRect.X, y0));
            else
                candidates.Add((line.Words.Min(w => w.BoundingRect.X), y0));
        }
        return candidates;
    }

    private static List<(double X, double Y)> FindSkillLabelAnchorCandidates(OcrResult ocrResult)
    {
        var candidates = new List<(double X, double Y)>();
        foreach (var line in ocrResult.Lines)
        {
            var lineText = line.Text.Replace(" ", "");
            if (!lineText.Contains("装備スキル"))
                continue;

            var y0 = line.Words.Min(w => w.BoundingRect.Y);
            var matched = line.Words.FirstOrDefault(w => w.Text.Contains("装"));
            if (matched != null)
                candidates.Add((matched.BoundingRect.X - 20, y0 - 310));
            else
                candidates.Add((line.Words.Min(w => w.BoundingRect.X) - 20, y0 - 310));
        }
        return candidates;
    }

    internal static bool IsCharmPanel(OcrResult ocrResult, double anchorX, double anchorY)
    {
        foreach (var line in ocrResult.Lines)
        {
            var lineText = line.Text.Replace(" ", "");
            if (!lineText.Contains("護石"))
                continue;

            var x0 = line.Words.Min(w => w.BoundingRect.X);
            var y0 = line.Words.Min(w => w.BoundingRect.Y);
            var dx = x0 - anchorX;
            var dy = y0 - anchorY;
            if (dx >= -50 && dx <= 250 && dy >= 80 && dy <= 280)
                return true;
        }
        return false;
    }

    internal static Mat? CropSkillArea(Mat img, double anchorX, double anchorY)
    {
        int x0 = Math.Max(0, (int)(anchorX + SkillAreaRel.X));
        int y0 = Math.Max(0, (int)(anchorY + SkillAreaRel.Y));
        int x1 = Math.Min(img.Width, (int)(anchorX + SkillAreaRel.X + SkillAreaRel.Width));
        int y1 = Math.Min(img.Height, (int)(anchorY + SkillAreaRel.Y + SkillAreaRel.Height));

        int w = x1 - x0;
        int h = y1 - y0;
        if (w <= 0 || h <= 0) return null;

        return new Mat(img, new Rect(x0, y0, w, h));
    }

    private static async Task<IReadOnlyList<SkillEntry>> RunVariantsAndMerge(
        List<ImageVariantFactory.Variant> variants,
        IReadOnlyList<string> knownSkills)
    {
        var knownSkillSet = new HashSet<string>(knownSkills);
        var allResults = new List<(List<(double Y, string SkillName)> Names, List<(double Y, int Lv)> Lvs)>();

        foreach (var v in variants)
        {
            var ocrResult = await OcrMatAsync(v.Image);
            var texts = ParseOcrLines(ocrResult);

            var filtered = texts
                .Where(t => !t.Text.Contains("装備スキル") && t.Text != "スキル" && !t.Text.Contains("スロット"))
                .ToList();

            var names = new List<(double Y, string SkillName)>();
            var lvs = new List<(double Y, int Lv)>();

            foreach (var t in filtered)
            {
                if (t.X0 >= v.LvXThreshold)
                {
                    var parsed = LvParser.Parse(t.Text);
                    if (parsed is not null)
                        lvs.Add((t.Y0, parsed.Value));
                }
                else
                {
                    var normalized = SkillNameNormalizer.Normalize(t.Text, knownSkills, knownSkillSet);
                    if (normalized is not null)
                        names.Add((t.Y0, normalized));
                }
            }

            names.Sort((a, b) => a.Y.CompareTo(b.Y));
            lvs.Sort((a, b) => a.Y.CompareTo(b.Y));
            allResults.Add((names, lvs));
        }

        if (allResults.Count == 0)
            return [];

        // 名前・Lvはそれぞれ独立に最多検出のバリアントを採用する（あるバリアントは名前の検出に強く、
        // 別のバリアントはLvの検出に強い、といったケースがあるため）。ペアリングはY座標が近いもの
        // 同士で貪欲マッチングする（単純なインデックス順だと、名前が1つ欠落しただけで後続の
        // 全スキルのLvがずれてしまうため）。
        var bestNames = allResults.MaxBy(r => r.Names.Count)!.Names;
        var bestLvs = allResults.MaxBy(r => r.Lvs.Count)!.Lvs;
        var entries = PairByNearestY(bestNames, bestLvs);

        var rawImage = variants.FirstOrDefault(v => v.Name == "raw").Image;
        if (rawImage is not null)
            await TryRecoverOrphanNames(entries, rawImage, knownSkills, knownSkillSet);

        entries.Sort((a, b) => a.Y.CompareTo(b.Y));
        return entries.Select(e => e.Entry).ToList();
    }

    /// <summary>
    /// スキル名リストとLvリストを、それぞれ単純なインデックス順ではなくY座標が近いもの同士で
    /// 貪欲マッチングする。名前が1つでも検出漏れすると、インデックス順ペアリングでは
    /// 後続の全スキルのLvがずれてしまうため（例: 1つ目のスキル名が丸ごと検出できない場合、
    /// 2つ目以降のスキルに誤ったLvが割り当たる）。
    /// </summary>
    private static List<(double Y, SkillEntry Entry)> PairByNearestY(
        List<(double Y, string SkillName)> names,
        List<(double Y, int Lv)> lvs)
    {
        var candidatePairs = new List<(int NameIdx, int LvIdx, double Dist)>();
        for (int i = 0; i < names.Count; i++)
            for (int j = 0; j < lvs.Count; j++)
                candidatePairs.Add((i, j, Math.Abs(names[i].Y - lvs[j].Y)));
        candidatePairs.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        var usedNames = new bool[names.Count];
        var usedLvs = new bool[lvs.Count];
        var matchedLvIndexForName = new int?[names.Count];
        foreach (var (nameIdx, lvIdx, _) in candidatePairs)
        {
            if (usedNames[nameIdx] || usedLvs[lvIdx])
                continue;
            usedNames[nameIdx] = true;
            usedLvs[lvIdx] = true;
            matchedLvIndexForName[nameIdx] = lvIdx;
        }

        var entries = new List<(double Y, SkillEntry Entry)>();
        for (int i = 0; i < names.Count; i++)
        {
            var lv = matchedLvIndexForName[i] is int lvIdx ? lvs[lvIdx].Lv : (int?)null;
            entries.Add((names[i].Y, new SkillEntry(names[i].SkillName, lv)));
        }
        for (int j = 0; j < lvs.Count; j++)
        {
            if (usedLvs[j])
                continue;
            entries.Add((lvs[j].Y, new SkillEntry(null, lvs[j].Lv)));
        }

        return entries;
    }

    /// <summary>
    /// 名前が検出できなかった行(Lvのみ)を、既知スキル名として認識回復させる試み。
    /// Windows.Media.Ocrは1文字だけで同一行に他のテキストが無い場合、その文字を
    /// 行として一切検出しないことがある（「匠」等の1文字スキル名で確認済み）。
    /// 同じ画像内で既に名前が判明している別の行を「コンパニオン」として横に連結し、
    /// 再OCRすることで、単独では検出されない文字を検出させる（検証済みの回避策）。
    /// </summary>
    private static async Task TryRecoverOrphanNames(
        List<(double Y, SkillEntry Entry)> entries,
        Mat rawImage,
        IReadOnlyList<string> knownSkills,
        HashSet<string> knownSkillSet)
    {
        var orphanIndices = new List<int>();
        (double Y, string Name)? companion = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var (y, entry) = entries[i];
            if (entry.Name is null && entry.Lv is not null)
                orphanIndices.Add(i);
            else if (entry.Name is not null && companion is null)
                companion = (y, entry.Name);
        }

        if (orphanIndices.Count == 0 || companion is null)
            return;

        const int halfHeight = 40;
        // スキルアイコン(装飾グラフィック)を含めるとOCRの行検出が阻害されるため、
        // アイコン列(x:0-50付近)を除外し、名前テキスト部分のみを切り出す。
        const int xOffset = 50;
        const int bandWidth = 250;

        Mat? ExtractBand(double centerY)
        {
            int y0 = Math.Max(0, (int)centerY - halfHeight);
            int y1 = Math.Min(rawImage.Rows, (int)centerY + halfHeight);
            int w = Math.Min(bandWidth, rawImage.Cols - xOffset);
            if (y1 - y0 <= 0 || w <= 0)
                return null;
            return new Mat(rawImage, new Rect(xOffset, y0, w, y1 - y0));
        }

        using var companionBand = ExtractBand(companion.Value.Y);
        if (companionBand is null)
            return;

        foreach (var i in orphanIndices)
        {
            var (orphanY, orphanEntry) = entries[i];
            using var orphanBand = ExtractBand(orphanY);
            if (orphanBand is null)
                continue;

            using var companionResized = new Mat();
            if (companionBand.Rows == orphanBand.Rows)
                Cv2.CopyTo(companionBand, companionResized);
            else
                Cv2.Resize(companionBand, companionResized, new Size(companionBand.Cols, orphanBand.Rows));

            using var combined = new Mat();
            Cv2.HConcat(new[] { orphanBand, companionResized }, combined);

            using var bgra = new Mat();
            Cv2.CvtColor(combined, bgra, ColorConversionCodes.BGR2BGRA);
            var bytes = new byte[bgra.Rows * bgra.Cols * 4];
            System.Runtime.InteropServices.Marshal.Copy(bgra.Data, bytes, 0, bytes.Length);
            var ocr = await TextOcrReader.RecognizeBytesAsync(bytes, bgra.Cols, bgra.Rows);

            foreach (var line in ocr.Lines)
            {
                if (line.Words.Count == 0)
                    continue;

                // コンパニオンはorphanBandの右側に連結しているため、行の左端X座標が
                // orphanBandの幅を超えるものはコンパニオン側の再認識結果として除外する。
                // テキスト内容による判定(コンパニオン名を含むか)ではなく座標で判定することで、
                // OCRがコンパニオン側を誤読した場合に誤って別スキル名を採用してしまう
                // リスクを避ける。
                var x0 = line.Words.Min(w => w.BoundingRect.X);
                if (x0 >= orphanBand.Cols)
                    continue;

                var text = line.Text.Replace(" ", "");
                var normalized = SkillNameNormalizer.Normalize(text, knownSkills, knownSkillSet);
                if (normalized is not null)
                {
                    entries[i] = (orphanY, orphanEntry with { Name = normalized });
                    break;
                }
            }
        }
    }

    private static async Task<OcrResult> OcrMatAsync(Mat mat)
    {
        using var bgra = new Mat();
        Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);
        var bytes = new byte[bgra.Rows * bgra.Cols * 4];
        System.Runtime.InteropServices.Marshal.Copy(bgra.Data, bytes, 0, bytes.Length);
        return await TextOcrReader.RecognizeBytesAsync(bytes, bgra.Cols, bgra.Rows);
    }

    private static List<OcrTextItem> ParseOcrLines(OcrResult ocrResult)
    {
        var items = new List<OcrTextItem>();
        foreach (var line in ocrResult.Lines)
        {
            if (line.Words.Count == 0)
                continue;

            var text = line.Text.Replace(" ", "");
            var x0 = line.Words.Min(w => w.BoundingRect.X);
            var y0 = line.Words.Min(w => w.BoundingRect.Y);
            var x1 = line.Words.Max(w => w.BoundingRect.X + w.BoundingRect.Width);
            var y1 = line.Words.Max(w => w.BoundingRect.Y + w.BoundingRect.Height);
            items.Add(new OcrTextItem(text, x0, y0, x1, y1));
        }
        return items;
    }
}
