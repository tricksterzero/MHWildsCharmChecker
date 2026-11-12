using OpenCvSharp;
using CharmChecker.Core.Image;

namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// legacy/slot-icon-pipeline/pipeline.py で検証済みのスロットアイコン判定ロジックの移植を土台に、
/// 実データでのチューニングを加えたもの（Lvの判定はPoCの列プロファイル方式から空スロットの
/// テンプレート照合(<see cref="MatchEmptyTemplate"/>)に置き換え、融合した枠の回復なども加えている。
/// legacy側はビルド対象外の参照記録のため未追随）。
/// 詳細仕様はリポジトリルートのCLAUDE.mdを参照。
/// </summary>
public static class SlotIconAnalyzer
{
    /// <summary>
    /// 基準解像度(2560x1440)に対する実コンテンツの縮小率(sx, sy)。ゲーム内UIは右上コーナー基準に
    /// 縦横等方で配置される(横に広い21:9で幅だけが増えても、UI自体の大きさ・右上からの相対距離は
    /// 変わらない)ため、LetterboxNormalizerが返すコンテンツ高さ/1440の比率をそのまま両軸に使う。
    /// </summary>
    public static (double Sx, double Sy) ScaleFactors(Mat img)
    {
        var (_, _, scale) = LetterboxNormalizer.DetectContentBounds(img);
        return (scale, scale);
    }

    /// <summary>単一護石詳細画面のスロットアイコン探索領域を、実画像サイズに合わせて切り出す矩形。</summary>
    public static Rect DetailPanelRegion(Mat img) => RegionFromTopRight(img,
        SlotIconConstants.DetailPanelX0, SlotIconConstants.DetailPanelX1,
        SlotIconConstants.DetailPanelY0, SlotIconConstants.DetailPanelY1);

    /// <summary>装備BOX側スロットアイコンの探索領域を、実画像サイズに合わせて切り出す矩形。</summary>
    public static Rect PanelRegion(Mat img) => RegionFromTopRight(img,
        SlotIconConstants.PanelX0, SlotIconConstants.PanelX1,
        SlotIconConstants.PanelY0, SlotIconConstants.PanelY1);

    /// <summary>
    /// 基準解像度での座標で表した領域を、実画像の矩形にする。X座標は右上コーナーからの距離として、
    /// Y座標は黒帯の上端からの距離として、それぞれUIの縮小率を掛けて置き直す(<see cref="SlotIconConstants"/>参照)。
    /// </summary>
    private static Rect RegionFromTopRight(Mat img, double refX0, double refX1, double refY0, double refY1)
    {
        var (top, _, s) = LetterboxNormalizer.DetectContentBounds(img);
        int w = img.Width;
        int y0 = top + (int)(refY0 * s);
        int y1 = top + (int)(refY1 * s);
        int x0 = w - (int)((SlotIconConstants.RefWidth - refX0) * s);
        int x1 = w - (int)((SlotIconConstants.RefWidth - refX1) * s);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    // ソケット枠の輪郭を取るCannyのしきい値(DetectFramesと、融合した枠の回復で同じ値を使う)
    private const double CannyThreshold1 = 50;
    private const double CannyThreshold2 = 150;

    /// <summary>ソケット枠とみなす大きさの範囲(UIの倍率を掛けた後のpx。上下限は含まない)。</summary>
    private readonly record struct FrameSizeRange(double WidthMin, double WidthMax, double HeightMin, double HeightMax)
    {
        public static FrameSizeRange Scaled(double sx, double sy) => new(
            SlotIconConstants.FrameWidthMin * sx, SlotIconConstants.FrameWidthMax * sx,
            SlotIconConstants.FrameHeightMin * sy, SlotIconConstants.FrameHeightMax * sy);

        /// <summary>幅・高さとも範囲内か。</summary>
        public bool Fits(Rect r) =>
            r.Width > WidthMin && r.Width < WidthMax && r.Height > HeightMin && r.Height < HeightMax;

        /// <summary>
        /// 高さは範囲内で、幅だけが上限以上・上限の4倍未満か(枠が隣の飾りと1つに融合した可能性がある塊)。
        /// </summary>
        public bool IsWidthOnlyOversized(Rect r) =>
            r.Width >= WidthMax && r.Width < WidthMax * 4 && r.Height > HeightMin && r.Height < HeightMax;
    }

    /// <summary>2つの矩形のY範囲が重なるか(同じ行にあるか)。</summary>
    private static bool OverlapsVertically(Rect a, Rect b) => a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    /// <summary>
    /// Canny+輪郭検出でソケット枠を検出する。サイズ・y座標でフィルタし、x座標の昇順で返す。
    /// </summary>
    public static List<Rect> DetectFrames(Mat gray, double sx, double sy)
    {
        var size = FrameSizeRange.Scaled(sx, sy);
        double yMin = SlotIconConstants.FrameYMin * sy;

        using var edges = new Mat();
        Cv2.Canny(gray, edges, CannyThreshold1, CannyThreshold2);

        Cv2.FindContours(edges, out Point[][] contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var raw = new List<Rect>();
        var oversized = new List<Rect>();
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (size.Fits(rect) && rect.Y >= yMin)
            {
                raw.Add(rect);
            }
            else if (size.IsWidthOnlyOversized(rect) && rect.Y >= yMin)
            {
                // 幅だけが上限を超える塊。ソケット枠が隣接UI装飾(スロットバーの縁取り等)と
                // Cannyで1つに融合した可能性がある候補として保持する（TryRecoverFusedFrame参照）。
                oversized.Add(rect);
            }
        }

        raw.Sort((a, b) => a.X.CompareTo(b.X));
        var merged = MergeOverlapping(raw, SlotIconConstants.MergeXThreshold * sx);
        var filtered = FilterYCluster(merged, SlotIconConstants.ClusterYThreshold * sy);

        // 確定した枠が1つもない場合は回復を試みない。正しいパネルがDetail側にある画面(狩猟後の鑑定BOX等)で
        // BOX側のノイズの塊から枠を「回復」すると、ReadSlotsの「BOX側で1件以上検出できればBOX優先」の
        // 規則で誤った領域が選ばれるため(2026-11-08にコメントとCLAUDE.mdを実際の動作に合わせて明記)
        if (oversized.Count > 0 && filtered.Count > 0)
        {
            filtered = TryRecoverFusedFrame(gray, filtered, oversized, size);
            // 回復処理で別行のノイズ(例: 「RARE」テキストの誤検出)が同じ行に確定フレームの無い
            // 融合塊から回復されると、ノイズと本物のフレームが混在した状態になりうる。
            // 再度Yクラスタリングし、最頻グループ(=本物のスロット行)だけに絞り直す。
            filtered = FilterYCluster(filtered, SlotIconConstants.ClusterYThreshold * sy);
        }

        return filtered;
    }

    /// <summary>
    /// ソケット枠が隣接するUI装飾(スロットバーの縁取り等)とCannyで1つの輪郭に融合し、
    /// 幅だけが異常に大きい塊として検出された場合の回復処理。実データで2パターン確認済み:
    ///
    /// (1) 「装備変更」画面の2護石比較パネル: 1つ目のアイコンがバーの縁取りと融合し、
    ///     その塊の中に2つ目のアイコン(確定フレーム)が含まれる形で検出される。
    /// (2) 単一「装備詳細」パネル(鑑定BOX画面等): 1つ目のアイコンがバーの縁取りと融合するが、
    ///     2つ目のアイコン(確定フレーム)は塊に含まれず、単に隣接するだけの場合がある。
    ///     この場合、塊自体の境界はCannyの融合具合で伸縮し不安定(実験で、狭い窓から広い窓へ
    ///     切り出し幅を変えると塊が際限なく広がり続けることを確認済み)なため、塊の境界ではなく
    ///     隣接する確定フレームの端を基準にした固定幅の窓で再探索する方が安定する。
    /// </summary>
    private static List<Rect> TryRecoverFusedFrame(
        Mat gray, List<Rect> accepted, List<Rect> oversizedCandidates, FrameSizeRange size)
    {
        var result = new List<Rect>(accepted);
        double adjacencyThreshold = size.WidthMin * 0.67;

        // 塊と同じ行で、X方向に[x, x+width)の窓から枠を探し、見つかればresultに加える
        void RecoverInWindow(Rect oversize, int x, int width) =>
            AddIfNotOverlapping(result, FindFrameInWindow(gray, new Rect(x, oversize.Y, width, oversize.Height), size));

        foreach (var oversize in oversizedCandidates)
        {
            Rect? contained = null;
            foreach (var f in accepted)
            {
                // X範囲の内包だけで判定すると、Y範囲が全く異なる(=別行の)無関係な大型輪郭を
                // 誤って内包パターンと認識してしまう(隣接パターンの同じ行の条件と同じ理由で必須)
                if (OverlapsVertically(oversize, f) && f.X >= oversize.X && f.Right <= oversize.Right)
                {
                    contained = f;
                    break;
                }
            }

            if (contained is { } c)
            {
                // 窓の外側境界(隣接する確定フレームと逆側)に余白を持たせる。境界ぎりぎりで
                // 切り出すとCannyが輪郭を正しく閉じられず、本来のアイコン形状より小さい
                // 断片に分裂することを実験で確認済みのため、内側(隣接フレーム側)は境界通り、
                // 外側だけ余白を追加する。
                int outerMargin = (int)(size.WidthMax * 0.4);

                RecoverInWindow(oversize, oversize.X - outerMargin, (c.X - oversize.X) + outerMargin);
                RecoverInWindow(oversize, c.Right, (oversize.Right - c.Right) + outerMargin);
                continue;
            }

            // 塊が確定フレームを含んでいない=単に隣接しているだけのケース。
            // 塊自体の境界(不安定)ではなく、隣接する確定フレームの端を起点に、
            // アイコン1個分相当の固定幅の窓で再探索する。
            // X座標の近さだけで判定すると、Y座標が全く異なる(=別の行の)無関係な形状まで
            // 「隣接」とみなしてしまうため、Y範囲が重なっている場合に限定する
            // (実データで、別行の無関係な形状を誤検出する回帰を確認したため必須の条件)。
            int fixedWindowWidth = (int)size.WidthMax;
            bool recoveredViaAdjacency = false;
            foreach (var f in accepted)
            {
                if (!OverlapsVertically(oversize, f))
                    continue;

                if (Math.Abs(oversize.Right - f.X) < adjacencyThreshold)
                {
                    RecoverInWindow(oversize, f.X - fixedWindowWidth, fixedWindowWidth);
                    recoveredViaAdjacency = true;
                }
                else if (Math.Abs(f.Right - oversize.X) < adjacencyThreshold)
                {
                    RecoverInWindow(oversize, f.Right, fixedWindowWidth);
                    recoveredViaAdjacency = true;
                }
            }

            if (recoveredViaAdjacency)
                continue;

            // 同じ行に確定フレームが1つも無い(=確定フレームは別の行のノイズ(「RARE」の文字の誤検出等)
            // だけで、護石のスロットは丸ごと融合した)場合のフォールバック。確定フレームが領域内に
            // 1つも無い場合はここに来ない(DetectFramesの呼び出し条件を参照)。手掛かりが無いため
            // 塊自体の左端・右端それぞれから固定幅の窓で再探索し、有効な候補が出た方を採用する
            // (実データで、真のソケット枠は融合塊の左端から始まるケースを確認済みだが、
            // 将来別の画面パターンで右端から始まるケースもありうるため両側を試す)。
            RecoverInWindow(oversize, oversize.X, fixedWindowWidth);
            RecoverInWindow(oversize, oversize.Right - fixedWindowWidth, fixedWindowWidth);
        }

        result.Sort((a, b) => a.X.CompareTo(b.X));
        return result;
    }

    /// <summary>
    /// 窓(<paramref name="window"/>、画像からはみ出す分は切り詰める)の中でCanny+輪郭検出をやり直し、
    /// ソケット枠の大きさに合う輪郭を1つに決められれば、画像全体の座標で返す(副作用なし)。
    /// 窓の幅が枠の最小幅より狭い・候補が無い・候補が複数あって決められない場合はnull。
    /// </summary>
    private static Rect? FindFrameInWindow(Mat gray, Rect window, FrameSizeRange size)
    {
        if (window.Width < size.WidthMin)
            return null;

        var bounds = new Rect(0, 0, gray.Cols, gray.Rows);
        var subRect = window.Intersect(bounds);
        if (subRect.Width <= 0 || subRect.Height <= 0)
            return null;

        using var subGray = new Mat(gray, subRect);
        using var subEdges = new Mat();
        Cv2.Canny(subGray, subEdges, CannyThreshold1, CannyThreshold2);
        Cv2.FindContours(subEdges, out Point[][] subContours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var candidates = new List<Rect>();
        foreach (var contour in subContours)
        {
            var r = Cv2.BoundingRect(contour);
            if (size.Fits(r))
                candidates.Add(r);
        }

        if (candidates.Count == 0)
            return null;

        Rect recovered;
        if (candidates.Count == 1)
        {
            recovered = candidates[0];
        }
        else if (window.Width < size.WidthMax * 1.5)
        {
            // 窓の幅が1個分のアイコン相当のため、複数候補は同一アイコンの内側/外側境界の
            // 重複検出である可能性が高いと判断し、最大面積のものを採用する
            // (MergeOverlappingが近接候補から大きい方を選ぶのと同じ考え方)。
            recovered = candidates.MaxBy(c => c.Width * c.Height);
        }
        else
        {
            // 窓が複数アイコン分の幅を持ちうる場合、どれが正しいか判断できないため採用しない
            return null;
        }

        return new Rect(recovered.X + subRect.X, recovered.Y + subRect.Y, recovered.Width, recovered.Height);
    }

    /// <summary>
    /// 回復した枠を<paramref name="frames"/>に加える。nullなら何もしない。
    /// 複数のoversizedCandidatesが同じ枠を回復した場合や、1つの内包塊内に確定フレームが
    /// 複数含まれる場合に、同一の枠を重複して追加してしまうことを防ぐため、既にある枠とX範囲が
    /// 重なっていれば加えない(同一アイコンの重複検出とみなす)。
    /// </summary>
    private static void AddIfNotOverlapping(List<Rect> frames, Rect? recovered)
    {
        if (recovered is not { } rect || frames.Any(existing => RangesOverlap(existing, rect)))
            return;
        frames.Add(rect);
    }

    private static bool RangesOverlap(Rect a, Rect b)
    {
        int overlapStart = Math.Max(a.X, b.X);
        int overlapEnd = Math.Min(a.Right, b.Right);
        return overlapEnd > overlapStart;
    }

    /// <summary>
    /// 枠下部の三角マーク(レベル表示)領域を、検証済み空スロットのテンプレート(<see cref="EmptySlotTemplates"/>)
    /// とカラー画素で比較し、最も近いLvのテンプレートとその差分値を返す。
    /// 差分が<see cref="SlotIconConstants.EmptyTemplateThreshold"/>以下なら「空スロット」とみなしLevelを返す。
    /// それ以外(装飾品で塗り分けられ差分が大きい)はLevel=nullを返す(装着済みとして護石全体を除外する運用)。
    /// 三角マークは装飾品の色でそのまま塗り分けられるため、色情報が必須(グレースケールでは分離できない
    /// ことを実データで確認済み、詳細はCLAUDE.md参照)。装飾品の色・サイズ(スロットサイズとの一致/不一致)に
    /// 左右されない安定した判定を狙った設計で、旧方式(枠上部中心の輝度percentile方式、削除済み)が
    /// サイズ不一致ケースで機能しなかった問題への対応として新設した。
    /// 各Lvのテンプレートは画面パターンごとに複数件を個別保持し、最も近い1件(最近傍)との差分を採用する
    /// (画面ごとに背景色が異なるため、単純平均するとどの画面にも合わない中間値になり精度が落ちることを
    /// 実データで確認済み)。
    ///
    /// 比較は、照合対象・テンプレートの両方からチャネルごとの平均を引いてから行う。パネルが半透明で
    /// 背景(3Dシーン)の色が透けるため、同じ画面の空スロットでも全体の明るさ・色味がずれ、色の
    /// 絶対値で比べると閾値を超えて除外されていた(2026-11-07)。三角マークの形・塗り分けの違いは
    /// 平均を引いても残るため、装飾品ありとの分離は保たれる(装飾品ありの最小差分6.71→7.77)。
    /// また、枠そのままで閾値を超えた場合のみ、枠の位置・大きさを±<see cref="SlotIconConstants.FrameJitterSearchPx"/>px
    /// ずらして最小差分を探す(輪郭検出による枠の揺れ対策)。
    /// さらに、枠の右端が<paramref name="colorRegion"/>の右端に接していて閾値を超えた場合は、幅を
    /// <see cref="EdgeFrameRetryWidths"/>の値にした枠でも照合し、最小差分を使う(右端の飾りとの融合対策)。
    ///
    /// 事前条件: <paramref name="colorRegion"/>は3チャネル(BGR)、<paramref name="frame"/>は幅・高さが正で
    /// <paramref name="colorRegion"/>の範囲内。満たさない場合は例外を投げる。
    /// 事後条件: 戻り値のBestDiffは0以上。Levelは、BestDiffが閾値以下ならLv1〜Lv3のいずれか、超えればnull。
    /// </summary>
    public static EmptyTemplateMatch MatchEmptyTemplate(Mat colorRegion, Rect frame)
    {
        ArgumentNullException.ThrowIfNull(colorRegion);
        if (colorRegion.Type() != MatType.CV_8UC3)
            throw new ArgumentException("colorRegionは3チャネル(BGR)の画像である必要があります。", nameof(colorRegion));
        if (frame.Width <= 0 || frame.Height <= 0 || !IsInside(frame, colorRegion))
            throw new ArgumentOutOfRangeException(nameof(frame), frame, "frameの幅・高さが0以下か、colorRegionの範囲外です。");

        var best = FindNearestTemplateWithJitter(colorRegion, frame);

        // 探索領域の右端に接する枠は、スロット欄の右端の飾りと1つの枠として検出され、実際のアイコンより
        // 幅が広くなることがある(2護石比較画面の右パネルの3つ目のスロット、2026-11-08)。そのままで
        // 照合に失敗した場合のみ、左端と高さはそのままに、幅をアイコンの縦横比から求めた値にして照合し直す。
        if (best.Diff > SlotIconConstants.EmptyTemplateThreshold && frame.Right >= colorRegion.Width - 1)
        {
            foreach (int width in EdgeFrameRetryWidths(frame))
            {
                var candidate = FindNearestTemplateWithJitter(colorRegion, new Rect(frame.X, frame.Y, width, frame.Height));
                if (candidate.Diff < best.Diff) best = candidate;
            }
        }

        bool isEmpty = best.Diff <= SlotIconConstants.EmptyTemplateThreshold;
        return new EmptyTemplateMatch(isEmpty ? best.Level : null, best.Diff);
    }

    /// <summary>
    /// 枠そのままで照合し、閾値を超えた場合のみ枠の位置・大きさを±<see cref="SlotIconConstants.FrameJitterSearchPx"/>px
    /// ずらした最小差分を返す(副作用なし)。frameはcolorRegionの範囲内であること(呼び出し側で保証)。
    /// </summary>
    private static (SlotLevel Level, double Diff) FindNearestTemplateWithJitter(Mat colorRegion, Rect frame)
    {
        var best = FindNearestTemplate(CenteredLevelBand(colorRegion, frame));
        if (best.Diff <= SlotIconConstants.EmptyTemplateThreshold)
            return best;

        int jitter = SlotIconConstants.FrameJitterSearchPx;
        for (int dx = -jitter; dx <= jitter; dx++)
        for (int dy = -jitter; dy <= jitter; dy++)
        for (int ds = -jitter; ds <= jitter; ds++)
        {
            if (dx == 0 && dy == 0 && ds == 0) continue;
            var shifted = new Rect(frame.X + dx, frame.Y + dy, frame.Width + ds, frame.Height + ds);
            if (!IsInside(shifted, colorRegion)) continue;
            var candidate = FindNearestTemplate(CenteredLevelBand(colorRegion, shifted));
            if (candidate.Diff < best.Diff) best = candidate;
        }
        return best;
    }

    /// <summary>
    /// 探索領域の右端に接する枠を照合し直すときの幅(枠の高さ×<see cref="SlotIconConstants.SlotIconAspectRatios"/>の
    /// 各縦横比を丸めた値)のうち、元の枠より狭いものを重複なく返す(副作用なし)。
    /// 事前条件: frameの幅・高さが正。
    /// </summary>
    internal static IReadOnlyList<int> EdgeFrameRetryWidths(Rect frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(frame), frame, "frameの幅・高さは正である必要があります。");

        return SlotIconConstants.SlotIconAspectRatios
            .Select(ratio => (int)Math.Round(frame.Height * ratio))
            .Where(width => width > 0 && width < frame.Width)
            .Distinct()
            .ToList();
    }

    /// <summary>平均を引いた全テンプレート(Lvごと)。EmptySlotTemplatesは不変のため初回参照時に1度だけ作る。</summary>
    private static readonly (SlotLevel Level, float[] Centered)[] CenteredTemplates =
        EmptySlotTemplates.Lv1.Select(t => (SlotLevel.Lv1, CenterPerChannel(t)))
            .Concat(EmptySlotTemplates.Lv2.Select(t => (SlotLevel.Lv2, CenterPerChannel(t))))
            .Concat(EmptySlotTemplates.Lv3.Select(t => (SlotLevel.Lv3, CenterPerChannel(t))))
            .ToArray();

    private static bool IsInside(Rect rect, Mat region) =>
        rect.X >= 0 && rect.Y >= 0 && rect.Right <= region.Width && rect.Bottom <= region.Height;

    /// <summary>平均を引いた照合対象に最も近いテンプレートのLvと差分を返す(副作用なし)。</summary>
    private static (SlotLevel Level, double Diff) FindNearestTemplate(float[] centeredBand)
    {
        var best = (Level: SlotLevel.Unknown, Diff: double.MaxValue);
        foreach (var (level, template) in CenteredTemplates)
        {
            double diff = MeanAbsDiff(centeredBand, template);
            if (diff < best.Diff) best = (level, diff);
        }
        return best;
    }

    /// <summary>
    /// 枠下部(三角マーク)の領域をテンプレートと同じサイズへ縮小し、チャネルごとの平均を引いた
    /// 画素列(BGR、行優先)を返す。frameはregionの範囲内であること(呼び出し側で保証)。
    /// </summary>
    private static float[] CenteredLevelBand(Mat colorRegion, Rect frame)
    {
        int y0 = frame.Y + (int)(frame.Height * SlotIconConstants.LevelCropTopFraction);
        var bandRect = new Rect(frame.X, y0, frame.Width, frame.Y + frame.Height - y0);
        using var band = new Mat(colorRegion, bandRect);
        using var resized = new Mat();
        Cv2.Resize(band, resized, new Size(EmptySlotTemplates.TemplateWidth, EmptySlotTemplates.TemplateHeight));

        var flat = new byte[EmptySlotTemplates.TemplateWidth * EmptySlotTemplates.TemplateHeight * 3];
        int idx = 0;
        for (int y = 0; y < EmptySlotTemplates.TemplateHeight; y++)
        {
            for (int x = 0; x < EmptySlotTemplates.TemplateWidth; x++)
            {
                var px = resized.Get<Vec3b>(y, x);
                flat[idx++] = px.Item0;
                flat[idx++] = px.Item1;
                flat[idx++] = px.Item2;
            }
        }
        return CenterPerChannel(flat);
    }

    /// <summary>BGRの画素列(長さは3の倍数)から、B・G・Rそれぞれの平均を引いた値を返す(副作用なし)。</summary>
    internal static float[] CenterPerChannel(byte[] bgr)
    {
        ArgumentNullException.ThrowIfNull(bgr);
        if (bgr.Length == 0 || bgr.Length % 3 != 0)
            throw new ArgumentException("画素列の長さは3の正の倍数である必要があります。", nameof(bgr));

        var mean = new double[3];
        for (int i = 0; i < bgr.Length; i++) mean[i % 3] += bgr[i];
        int pixels = bgr.Length / 3;
        for (int c = 0; c < 3; c++) mean[c] /= pixels;

        var centered = new float[bgr.Length];
        for (int i = 0; i < bgr.Length; i++) centered[i] = (float)(bgr[i] - mean[i % 3]);
        return centered;
    }

    private static double MeanAbsDiff(float[] a, float[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
        return sum / a.Length;
    }

    private static List<Rect> MergeOverlapping(List<Rect> sorted, double threshold)
    {
        if (sorted.Count <= 1) return sorted;
        var merged = new List<Rect> { sorted[0] };
        for (int i = 1; i < sorted.Count; i++)
        {
            var prev = merged[^1];
            var cur = sorted[i];
            if (cur.X - prev.X < threshold)
            {
                if (cur.Width * cur.Height > prev.Width * prev.Height)
                    merged[^1] = cur;
            }
            else
            {
                merged.Add(cur);
            }
        }
        return merged;
    }

    private static List<Rect> FilterYCluster(List<Rect> frames, double yThreshold)
    {
        if (frames.Count <= 1) return frames;
        var sortedByY = frames.OrderBy(f => f.Y).ToList();
        var groups = new List<List<Rect>> { new() { sortedByY[0] } };
        for (int i = 1; i < sortedByY.Count; i++)
        {
            if (sortedByY[i].Y - groups[^1][^1].Y < yThreshold)
                groups[^1].Add(sortedByY[i]);
            else
                groups.Add(new() { sortedByY[i] });
        }
        var best = groups.MaxBy(g => (g.Count, g.Sum(f => (long)f.Width * f.Height)))!;
        var bestSet = new HashSet<Rect>(best);
        return frames.Where(f => bestSet.Contains(f)).ToList();
    }

}
