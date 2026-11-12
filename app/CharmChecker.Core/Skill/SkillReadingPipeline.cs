using OpenCvSharp;
using Windows.Media.Ocr;
using CharmChecker.Core.Image;
using CharmChecker.Core.Ocr;

namespace CharmChecker.Core.Skill;

/// <summary>
/// スクリーンショットからスキル名・Lvを読み取るパイプライン。
/// Python PoC (skill_ocr_test.py) の移植。
/// </summary>
public static class SkillReadingPipeline
{
    private static readonly Rect SkillAreaRel = new(0, 310, 470, 390);

    /// <summary>基準解像度の幅(2560)。真の21:9(3440x1440等)のOCR前縮小の目標幅として使う。</summary>
    private const int ReferenceWidth = 2560;

    /// <summary>
    /// フルスクリーンOCRでアンカー未検出時に試す、右端からのフォールバック幅(基準解像度でのpx、
    /// 広い順)。実測(2026-07-26)で、ある画像は幅760pxのOCRで「装備詳細」等が1行として認識
    /// されず失敗し、幅710pxでは成功する閾値的な挙動を確認(中央パネルの内容をマスクしても
    /// 結果は変わらないため、視覚的な干渉ではなく対象領域の幅そのものに起因する)。
    /// 画面パターンによってアンカーの右端距離は大きく異なる(「装備変更」「マカ錬金」は右端から
    /// 500px程度以内だが、「狩猟後護石入手」は中央寄りで右端から約1020px)。1200pxは後者を
    /// カバーするための幅で、900px/700pxは前者のような右端寄り画面で1200pxでも失敗した場合の
    /// 追加の絞り込み用(中央寄り画面のアンカーはこの2段では救えない)。
    /// </summary>
    private static readonly int[] FallbackOcrWidths = { 1200, 900, 700 };

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
    /// 事前条件: imagePathは空でなく、knownSkillsはnullでない。画像として読み込めないファイルは
    /// <see cref="InvalidDataException"/>を投げる。
    /// </summary>
    public static async Task<SkillReadResult?> ReadWithMetadataAsync(string imagePath, IReadOnlyList<string> knownSkills)
    {
        ArgumentException.ThrowIfNullOrEmpty(imagePath);
        ArgumentNullException.ThrowIfNull(knownSkills);

        using var img = Cv2.ImRead(imagePath);
        // 壊れた画像や画像でないファイルではImReadが空の画像を返し、そのままだと後段でOpenCVの
        // 分かりにくい例外("!_src.empty()")になるため、ここで原因の分かる例外にする
        if (img.Empty())
            throw new InvalidDataException($"画像を読み込めませんでした: {imagePath}");
        var (_, _, scale) = LetterboxNormalizer.DetectContentBounds(img);

        // 真の21:9(3440x1440等、基準幅2560を超える)スクリーンショットは、フルスクリーンOCRを
        // 生のまま実行すると「装備詳細」等の複数文字テキストが1行として認識されないケースが
        // 実測で多発する(2026-07-26、17枚中11枚でアンカー未検出)。判別変数は画像の幅であり
        // 面積ではない(元画像より小さい右半分クロップでも同様に失敗することを確認済み)。
        // 基準幅相当へ等方縮小したコピーでOCRすると全数成功したため、幅が基準を超える場合のみ
        // 縮小する(拡大は既知の副作用があるため行わない。CropSkillAreaのコメント参照)。
        double ocrScale = img.Width > ReferenceWidth ? (double)ReferenceWidth / img.Width : 1.0;
        using var ocrTarget = new Mat();
        if (ocrScale != 1.0)
            Cv2.Resize(img, ocrTarget, new Size(), ocrScale, ocrScale, InterpolationFlags.Area);
        else
            img.CopyTo(ocrTarget);

        // 「装備スキル」逆算アンカー(FindAnchor)の固定pxオフセットを補正する
        // 係数はscale(LetterboxNormalizer由来、黒帯付き21:9でUI自体がコンテンツ高さ/1440に縮小
        // 配置される比率)とocrScale(真の21:9ネイティブでOCR専用に基準幅へ縮小する比率)の積。
        // 両者は排他的に働く(黒帯付き21:9はimg.Width<=2560のためocrScale=1.0、真の21:9ネイティブは
        // 黒帯が無いためscale=1.0)ため、積を取ればどちらのケースも正しく補正できる
        // (2026-08-05発見、真の21:9ネイティブのみ対応していたのは修正漏れだった)。
        double anchorLabelScale = scale * ocrScale;

        var anchorScan = await FindAnchorWithFallbackAsync(ocrTarget, anchorLabelScale);
        if (anchorScan is null)
            return null;
        var (fullOcr, anchorOffsetX, (ax, ay)) = anchorScan.Value;

        if (!IsCharmPanel(fullOcr, ax, ay))
            return null;

        var charmName = ExtractCharmName(fullOcr, ax, ay);

        // アンカー座標はocrTarget(縮小コピー)の座標系のため、元画像でのクロップ前に戻す。
        // フォールバックで右端から切り出したOCRを使った場合は、その切り出し開始位置(x0)を
        // ocrTarget全体の座標系に戻すため加算してから、元画像の座標系へ変換する
        double origAx = (ax + anchorOffsetX) / ocrScale;
        double origAy = ay / ocrScale;

        using var crop = CropSkillArea(img, origAx, origAy, scale);
        if (crop is null) return null;

        var variants = ImageVariantFactory.Create(crop);
        try
        {
            var skills = await RunVariantsAndMerge(variants, knownSkills);
            return new SkillReadResult(skills, charmName, origAx, origAy);
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

            if (IsInCharmNameArea(line, anchorX, anchorY))
            {
                var idx = text.IndexOf("の護石");
                var name = text[..idx] + "の護石";
                return StripLeadingNonJapanese(name);
            }
        }
        return null;
    }

    /// <summary>
    /// 護石名の先頭に混入する非日本語文字(記号等)を取り除く。護石アイコン(装飾グラフィック)が
    /// OCRで記号として誤読され、テキスト行の先頭に混入するケースへの対応(2026-07-24、
    /// 21:9スクリーンショットの黒帯除去検証で発見。「、未解の護石」「)秘歴の護石」等)。
    /// 護石名はひらがな・カタカナ・漢字のみで構成されるため、それ以外の先頭文字を除去する。
    /// </summary>
    internal static string StripLeadingNonJapanese(string name)
    {
        int start = 0;
        while (start < name.Length && !IsJapaneseChar(name[start]))
            start++;
        return name[start..];
    }

    private static bool IsJapaneseChar(char c)
    {
        return (c >= '぀' && c <= 'ヿ')   // ひらがな・カタカナ
            || (c >= '一' && c <= '鿿')   // CJK統合漢字
            || (c >= '㐀' && c <= '䶿');  // CJK統合漢字拡張A
    }

    /// <summary>
    /// フルスクリーンOCRでアンカーが見つからない場合、右端から狭い幅を順に切り出して再OCRを
    /// 試みる。成功した場合はそのOCR結果、切り出し開始位置(ocrTarget全体の座標系でのX)、
    /// アンカー(そのOCR結果の座標系)を返す。全て失敗した場合はnullを返す。
    /// 戻り値のOcrResultはフォールバックを使わなかった場合、フルスクリーンでの結果そのもの(AnchorOffsetX=0)。
    /// anchorLabelScaleは<see cref="FindAnchor"/>に渡す。
    /// </summary>
    private static async Task<(OcrResult Ocr, double AnchorOffsetX, (double X, double Y) Anchor)?> FindAnchorWithFallbackAsync(
        Mat ocrTarget, double anchorLabelScale)
    {
        var fullOcr = await OcrMatAsync(ocrTarget);
        if (FindAnchor(fullOcr, anchorLabelScale) is { } fullAnchor)
            return (fullOcr, 0, fullAnchor);

        foreach (var width in FallbackOcrWidths)
        {
            int x0 = Math.Max(0, ocrTarget.Width - width);
            var region = new Rect(x0, 0, ocrTarget.Width - x0, ocrTarget.Height);
            using var cropped = new Mat(ocrTarget, region);
            var croppedOcr = await OcrMatAsync(cropped);
            if (FindAnchor(croppedOcr, anchorLabelScale) is { } croppedAnchor)
                return (croppedOcr, x0, croppedAnchor);
        }

        return null;
    }

    /// <summary>
    /// 「装備スキル」の左端から、そのパネルのアンカー(「装備詳細」の「装」の位置)までの距離(基準解像度でのpx)。
    /// 「装備スキル」からアンカーを逆算するときに足す。
    /// </summary>
    private const double SkillLabelToAnchorX = -20;
    private const double SkillLabelToAnchorY = -310;

    /// <summary>
    /// anchorLabelScaleは「装備スキル」からの逆算に使う固定pxオフセット(<see cref="SkillLabelToAnchorX"/>・
    /// <see cref="SkillLabelToAnchorY"/>、基準解像度前提)を補正するために使う(UIの縮小率×OCR前の縮小率)。
    /// 「装備詳細」「備詳細」経路は検出したテキスト自体の座標をそのまま返すため補正不要だが、
    /// 「装備スキル」からの逆算経路は基準解像度でのオフセット値を直接引くため、OCR対象が縮小済みの
    /// 場合はオフセットも同率で縮小しないと座標系が食い違う
    /// (2026-08-05発見、真の21:9ネイティブでY方向に約100px級のずれが生じる計算上のバグだった)。
    /// </summary>
    internal static (double X, double Y)? FindAnchor(OcrResult ocrResult, double anchorLabelScale = 1.0)
    {
        // 「装備詳細」(読めなければ「備詳細」)の候補と、「装備スキル」からの逆算候補を集め、最右を採る
        var detailLabels = FindLabelCandidates(ocrResult, "装備詳細", "装", 0, 0);
        if (detailLabels.Count == 0)
            detailLabels = FindLabelCandidates(ocrResult, "備詳細", "備", 0, 0);
        var skillLabels = FindLabelCandidates(ocrResult, "装備スキル", "装",
            SkillLabelToAnchorX * anchorLabelScale, SkillLabelToAnchorY * anchorLabelScale);
        if (detailLabels.Count == 0 && skillLabels.Count == 0)
            return null;
        return ChooseAnchor(detailLabels, skillLabels, anchorLabelScale);
    }

    /// <summary>
    /// 「装備スキル」からの逆算候補を、どの「装備詳細」候補からもこの距離(基準解像度でのpx)以上
    /// 右にある場合に限り別パネルの候補とみなす。実測(2026-11-07、護石画面の全画像)で、同じパネルの
    /// 逆算候補と「装備詳細」候補のX差は-36〜+42、2護石比較画面の左右パネル間の距離は476〜484だった。
    /// </summary>
    private const double SeparatePanelMinDistance = 200.0;

    /// <summary>
    /// アンカー候補から最右のパネルのアンカーを選ぶ(副作用なし)。「装備詳細」候補を優先し(逆算候補は
    /// 位置が粗いため、同じパネルでは「装備詳細」の位置を使う)、「装備詳細」候補のどれよりも
    /// <see cref="SeparatePanelMinDistance"/>×<paramref name="scale"/>以上右にある逆算候補だけを
    /// 別パネルとして加える。2護石比較画面で右パネルの「装備詳細」が半透明の背景のためOCRで読めず、
    /// 左パネルだけが見つかる場合に、右パネルの「装備スキル」から右パネルを選ぶため(2026-11-07。
    /// 修正前は「装備詳細」が1つでも見つかると逆算候補を使わず、左パネルのスキルと右パネルの
    /// スロットが混ざっていた)。
    /// 事前条件: scaleは正の値。事後条件: どちらの候補も空ならnull、それ以外はいずれかの候補を返す。
    /// </summary>
    internal static (double X, double Y)? ChooseAnchor(
        IReadOnlyList<(double X, double Y)> detailLabels,
        IReadOnlyList<(double X, double Y)> skillLabels,
        double scale)
    {
        ArgumentNullException.ThrowIfNull(detailLabels);
        ArgumentNullException.ThrowIfNull(skillLabels);
        if (!(scale > 0))
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "scaleは正の値である必要があります。");

        if (detailLabels.Count == 0)
            return skillLabels.Count > 0 ? skillLabels.MaxBy(c => c.X) : null;

        var rightmostDetail = detailLabels.MaxBy(c => c.X);
        double minDistance = SeparatePanelMinDistance * scale;
        var separatePanels = skillLabels.Where(c => c.X - rightmostDetail.X >= minDistance).ToList();
        return separatePanels.Count > 0 ? separatePanels.MaxBy(c => c.X) : rightmostDetail;
    }

    /// <summary>
    /// 空白を除いた行のテキストに<paramref name="pattern"/>を含む行ごとに、アンカー候補を返す(副作用なし)。
    /// X座標は<paramref name="wordChar"/>を含む最初の単語の左端(無ければ行の左端)、Y座標は行の上端で、
    /// それぞれに<paramref name="offsetX"/>・<paramref name="offsetY"/>を足す。
    /// </summary>
    private static List<(double X, double Y)> FindLabelCandidates(
        OcrResult ocrResult, string pattern, string wordChar, double offsetX, double offsetY)
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
                candidates.Add((matched.BoundingRect.X + offsetX, y0 + offsetY));
            else
                candidates.Add((line.Words.Min(w => w.BoundingRect.X) + offsetX, y0 + offsetY));
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

            if (IsInCharmNameArea(line, anchorX, anchorY))
                return true;
        }
        return false;
    }

    // 護石名が表示される範囲: 行の左上の、アンカーからの距離(基準解像度でのpx。UIの倍率は掛けていない)
    private const double CharmNameAreaMinDx = -50;
    private const double CharmNameAreaMaxDx = 250;
    private const double CharmNameAreaMinDy = 80;
    private const double CharmNameAreaMaxDy = 280;

    /// <summary>
    /// 行の左上(単語の左端・上端の最小値)が、アンカーから見て護石名の範囲にあるか(副作用なし)。
    /// <see cref="IsCharmPanel"/>と<see cref="ExtractCharmName"/>で同じ範囲を使う。
    /// </summary>
    private static bool IsInCharmNameArea(OcrLine line, double anchorX, double anchorY)
    {
        var dx = line.Words.Min(w => w.BoundingRect.X) - anchorX;
        var dy = line.Words.Min(w => w.BoundingRect.Y) - anchorY;
        return dx >= CharmNameAreaMinDx && dx <= CharmNameAreaMaxDx
            && dy >= CharmNameAreaMinDy && dy <= CharmNameAreaMaxDy;
    }

    /// <summary>
    /// アンカー位置からスキル領域(SkillAreaRel)を切り出す。scaleは基準解像度1440に対する
    /// 実コンテンツ高さの比率(21:9等のレターボックス画像で1440未満になる。詳細は
    /// <see cref="LetterboxNormalizer"/>)。scale!=1の場合、切り出した領域をSkillAreaRelの
    /// 基準サイズへ拡大する。これはアンカー検出やIsCharmPanel等のテキストベースの検出を
    /// 生画像のまま(拡大せず)行うための設計: 実験の結果、フルスクリーンOCR前に画像全体を
    /// 拡大すると特定のパネルでテキスト認識が崩れるケースが確認されたため、テキスト検出は
    /// 生画像で行い、固定pxオフセットに依存するこの幾何クロップのみスケール補正する
    /// (2026-07-24)。
    /// </summary>
    internal static Mat? CropSkillArea(Mat img, double anchorX, double anchorY, double scale = 1.0)
    {
        int relX = (int)Math.Round(SkillAreaRel.X * scale);
        int relY = (int)Math.Round(SkillAreaRel.Y * scale);
        int relW = (int)Math.Round(SkillAreaRel.Width * scale);
        int relH = (int)Math.Round(SkillAreaRel.Height * scale);

        int x0 = Math.Max(0, (int)(anchorX + relX));
        int y0 = Math.Max(0, (int)(anchorY + relY));
        int x1 = Math.Min(img.Width, (int)(anchorX + relX + relW));
        int y1 = Math.Min(img.Height, (int)(anchorY + relY + relH));

        int w = x1 - x0;
        int h = y1 - y0;
        if (w <= 0 || h <= 0) return null;

        using var cropped = new Mat(img, new Rect(x0, y0, w, h));
        if (scale == 1.0)
            return cropped.Clone();

        var resized = new Mat();
        Cv2.Resize(cropped, resized, new Size(SkillAreaRel.Width, SkillAreaRel.Height), 0, 0, InterpolationFlags.Cubic);
        return resized;
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
                // 拡大した前処理のY座標は、元のクロップの座標に戻してから他の前処理と同じ扱いにする
                // (X座標はLvXThresholdと同じ画像の座標で比べるため戻さない)
                double y = t.Y0 / v.CoordinateScale;
                if (t.X0 >= v.LvXThreshold)
                {
                    var parsed = LvParser.Parse(t.Text);
                    if (parsed is not null)
                        lvs.Add((y, parsed.Value));
                }
                else
                {
                    var normalized = SkillNameNormalizer.Normalize(t.Text, knownSkills, knownSkillSet);
                    if (normalized is not null)
                        names.Add((y, normalized));
                }
            }

            names.Sort((a, b) => a.Y.CompareTo(b.Y));
            lvs.Sort((a, b) => a.Y.CompareTo(b.Y));
            allResults.Add((names, lvs));
        }

        if (allResults.Count == 0)
            return [];

        // 名前・Lvはそれぞれ独立に最多検出のバリアントを採用する（あるバリアントは名前の検出に強く、
        // 別のバリアントはLvの検出に強い、といったケースがあるため）。ペアリングはY座標順を保った
        // 単調アライメント(DP)で行う（詳細はPairInYOrderのXMLコメント参照。単純なインデックス順や
        // 素朴なY最近傍の貪欲マッチングでは、名前の検出漏れや行間隔次第で誤ったペアになる問題があった）。
        var bestNames = allResults.MaxBy(r => r.Names.Count)!.Names;
        var bestLvs = allResults.MaxBy(r => r.Lvs.Count)!.Lvs;
        var entries = PairInYOrder(bestNames, bestLvs);

        var rawImage = variants.FirstOrDefault(v => v.Name == ImageVariantFactory.RawVariantName).Image;
        if (rawImage is not null)
            await TryRecoverOrphanNames(entries, rawImage, knownSkills, knownSkillSet);

        entries.Sort((a, b) => a.Y.CompareTo(b.Y));
        return entries.Select(e => e.Entry).ToList();
    }

    /// <summary>
    /// 同一行内でLvテキストのY座標が名前テキストのY座標よりどれだけ下にずれるかの実データ平均
    /// (6画像・17スキル項目を実測、31px〜48pxで変動、行間隔は約80pxでほぼ一定)。
    /// 生のY座標差をそのままコストにすると、この行内オフセットが行間隔の半分(約40px)を
    /// 超える画像で「1行分ずれた誤ペアリング」の方が正しいペアリングよりコストが低くなり、
    /// GapCostをどう調整しても直せない構造的な誤りが起きることをCodexとの相談で確認済み
    /// (`GapCost`は「マッチさせず読み飛ばす」コストで、マッチ同士の相対比較には効かないため)。
    /// このオフセットをコストに織り込む(生の差ではなく、期待オフセットからのズレを見る)ことで、
    /// 同一行のペアを常に安く評価できるようにする。
    /// </summary>
    private const double AssumedRowOffset = 37.0;

    /// <summary>
    /// スキル名リストとLvリストを、Y座標順を保った単調な対応付け(モノトニック・アライメント)で
    /// ペアリングする。名前・Lvはあらかじめ各々Y昇順でソート済みであることを前提とする。
    ///
    /// 単純な「Y最近傍」の貪欲マッチングでは、行間隔(row spacing)が行内の名前-Lv間オフセット
    /// (within-row offset)の2倍未満の場合、後の行の名前が前の行のLvに、前の行の名前が後の行の
    /// Lvに誤って交差マッチしてしまう問題があった（実例: 名前Y=[40,120], Lv Y=[88,168]の場合、
    /// 2番目の名前(120)は自分の本来のLv(168, 差48)より1番目のLv(88, 差32)の方が近く、
    /// 素朴な最近傍マッチングだと1番目と2番目のLvが入れ替わって割り当たる）。
    /// 行の並び順(Y昇順)はOCRの検出順と一致するはずなので、対応関係が交差しない
    /// （名前リストの前の要素は、Lvリストの前の要素にしか対応しない）という制約を守った上で
    /// 最小コストの対応を求めるDP（編集距離と同じ考え方、名前・Lvどちらかを読み飛ばす
    /// スキップを許容する）を使う。これにより、名前の検出漏れ（スキップが必要なケース、
    /// 例:「匠」等の1文字名検出漏れ）にも対応しつつ、交差マッチを防げる。
    /// </summary>
    internal static List<(double Y, SkillEntry Entry)> PairInYOrder(
        List<(double Y, string SkillName)> names,
        List<(double Y, int Lv)> lvs)
    {
        const double GapCost = 60.0;
        int n = names.Count, m = lvs.Count;

        var dp = new double[n + 1, m + 1];
        var choice = new byte[n + 1, m + 1]; // 0=match, 1=nameをスキップ, 2=lvをスキップ
        for (int i = 1; i <= n; i++) dp[i, 0] = i * GapCost;
        for (int j = 1; j <= m; j++) dp[0, j] = j * GapCost;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                double rawDiff = lvs[j - 1].Y - names[i - 1].Y;
                double matchCost = dp[i - 1, j - 1] + Math.Abs(rawDiff - AssumedRowOffset);
                double skipNameCost = dp[i - 1, j] + GapCost;
                double skipLvCost = dp[i, j - 1] + GapCost;

                double best = matchCost;
                byte c = 0;
                if (skipNameCost < best) { best = skipNameCost; c = 1; }
                if (skipLvCost < best) { best = skipLvCost; c = 2; }
                dp[i, j] = best;
                choice[i, j] = c;
            }
        }

        var matchedLvIndexForName = new int?[n];
        var usedLvs = new bool[m];
        int ci = n, cj = m;
        while (ci > 0 || cj > 0)
        {
            if (ci > 0 && cj > 0 && choice[ci, cj] == 0)
            {
                matchedLvIndexForName[ci - 1] = cj - 1;
                usedLvs[cj - 1] = true;
                ci--; cj--;
            }
            else if (ci > 0 && (cj == 0 || choice[ci, cj] == 1))
            {
                ci--;
            }
            else
            {
                cj--;
            }
        }

        var entries = new List<(double Y, SkillEntry Entry)>();
        for (int i = 0; i < n; i++)
        {
            var lv = matchedLvIndexForName[i] is int lvIdx ? lvs[lvIdx].Lv : (int?)null;
            entries.Add((names[i].Y, new SkillEntry(names[i].SkillName, lv)));
        }
        for (int j = 0; j < m; j++)
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
            // orphanYはLv行のY座標(ペアリングで名前と対応しなかったLvは、そのLvのY座標で加えるため)。
            // 名前は実データでLvより平均37px上に位置する(AssumedRowOffset)ため、Lv位置を
            // そのまま中心にすると行間隔(約80px)の半分を超える画像で名前の上端が窓の外に
            // はみ出しうる。名前の推定位置を中心に据える
            using var orphanBand = ExtractBand(orphanY - AssumedRowOffset);
            if (orphanBand is null)
                continue;

            using var companionResized = new Mat();
            if (companionBand.Rows == orphanBand.Rows)
                Cv2.CopyTo(companionBand, companionResized);
            else
                Cv2.Resize(companionBand, companionResized, new Size(companionBand.Cols, orphanBand.Rows));

            using var combined = new Mat();
            Cv2.HConcat(new[] { orphanBand, companionResized }, combined);

            var ocr = await OcrMatAsync(combined);

            foreach (var line in ocr.Lines)
            {
                if (line.Words.Count == 0)
                    continue;

                // コンパニオンはorphanBandの右側に連結しているため、X座標がorphanBandの幅を
                // 超える単語はコンパニオン側の再認識結果として除外する。行単位ではなく単語単位で
                // 判定することで、OCRが孤児側とコンパニオン側を同一行として結合した場合でも、
                // コンパニオン側の単語が混入したテキストを正規化してしまうことを防ぐ
                // (テキスト内容による判定ではなく座標で判定するのは、OCRがコンパニオン側を
                // 誤読した場合に誤って別スキル名を採用してしまうリスクを避けるため)。
                var orphanWords = line.Words.Where(w => w.BoundingRect.X < orphanBand.Cols).ToList();
                if (orphanWords.Count == 0)
                    continue;

                var text = string.Concat(orphanWords.Select(w => w.Text)).Replace(" ", "");
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
