using OpenCvSharp;
using CharmChecker.Core.Image;

namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// legacy/slot-icon-pipeline/pipeline.py で検証済みのスロットアイコン判定ロジックの移植を土台に、
/// 実データでのチューニングを加えたもの（Lv1/Lv2判定の閾値・n==1の扱いがPoCと異なる。
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
    public static Rect DetailPanelRegion(Mat img)
    {
        var (top, _, s) = LetterboxNormalizer.DetectContentBounds(img);
        int w = img.Width;
        int y0 = top + (int)(SlotIconConstants.DetailPanelY0 * s);
        int y1 = top + (int)(SlotIconConstants.DetailPanelY1 * s);
        int x0 = w - (int)((SlotIconConstants.RefWidth - SlotIconConstants.DetailPanelX0) * s);
        int x1 = w - (int)((SlotIconConstants.RefWidth - SlotIconConstants.DetailPanelX1) * s);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>装備BOX側スロットアイコンの探索領域を、実画像サイズに合わせて切り出す矩形。</summary>
    public static Rect PanelRegion(Mat img)
    {
        var (top, _, s) = LetterboxNormalizer.DetectContentBounds(img);
        int w = img.Width;
        int y0 = top + (int)(SlotIconConstants.PanelY0 * s);
        int y1 = top + (int)(SlotIconConstants.PanelY1 * s);
        int x0 = w - (int)((SlotIconConstants.RefWidth - SlotIconConstants.PanelX0) * s);
        int x1 = w - (int)((SlotIconConstants.RefWidth - SlotIconConstants.PanelX1) * s);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>
    /// Canny+輪郭検出でソケット枠を検出する。サイズ・y座標でフィルタし、x座標の昇順で返す。
    /// </summary>
    public static List<Rect> DetectFrames(Mat gray, double sx, double sy)
    {
        double wLo = SlotIconConstants.FrameWidthMin * sx;
        double wHi = SlotIconConstants.FrameWidthMax * sx;
        double hLo = SlotIconConstants.FrameHeightMin * sy;
        double hHi = SlotIconConstants.FrameHeightMax * sy;
        double yMin = SlotIconConstants.FrameYMin * sy;

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        Cv2.FindContours(edges, out Point[][] contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var raw = new List<Rect>();
        var oversized = new List<Rect>();
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width > wLo && rect.Width < wHi
                && rect.Height > hLo && rect.Height < hHi
                && rect.Y >= yMin)
            {
                raw.Add(rect);
            }
            else if (rect.Width >= wHi && rect.Width < wHi * 4
                && rect.Height > hLo && rect.Height < hHi
                && rect.Y >= yMin)
            {
                // 幅だけが上限を超える塊。ソケット枠が隣接UI装飾(スロットバーの縁取り等)と
                // Cannyで1つに融合した可能性がある候補として保持する（TryRecoverFusedFrame参照）。
                oversized.Add(rect);
            }
        }

        raw.Sort((a, b) => a.X.CompareTo(b.X));
        var merged = MergeOverlapping(raw, SlotIconConstants.MergeXThreshold * sx);
        var filtered = FilterYCluster(merged, SlotIconConstants.ClusterYThreshold * sy);

        if (oversized.Count > 0 && filtered.Count > 0)
        {
            filtered = TryRecoverFusedFrame(gray, filtered, oversized, wLo, wHi, hLo, hHi);
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
        Mat gray, List<Rect> accepted, List<Rect> oversizedCandidates,
        double wLo, double wHi, double hLo, double hHi)
    {
        var result = new List<Rect>(accepted);
        double adjacencyThreshold = wLo * 0.67;

        foreach (var oversize in oversizedCandidates)
        {
            Rect? contained = null;
            foreach (var f in accepted)
            {
                // X範囲の内包だけで判定すると、Y範囲が全く異なる(=別行の)無関係な大型輪郭を
                // 誤って内包パターンと認識してしまう(隣接パターンのsameRow条件と同じ理由で必須)
                bool sameRow = oversize.Y < f.Y + f.Height && f.Y < oversize.Y + oversize.Height;
                if (sameRow && f.X >= oversize.X && f.Right <= oversize.Right)
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
                int outerMargin = (int)(wHi * 0.4);

                TryRecoverSide(gray, oversize.Y, oversize.Height,
                    oversize.X - outerMargin, (c.X - oversize.X) + outerMargin,
                    wLo, wHi, hLo, hHi, result);
                TryRecoverSide(gray, oversize.Y, oversize.Height,
                    c.Right, (oversize.Right - c.Right) + outerMargin,
                    wLo, wHi, hLo, hHi, result);
                continue;
            }

            // 塊が確定フレームを含んでいない=単に隣接しているだけのケース。
            // 塊自体の境界(不安定)ではなく、隣接する確定フレームの端を起点に、
            // アイコン1個分相当の固定幅の窓で再探索する。
            // X座標の近さだけで判定すると、Y座標が全く異なる(=別の行の)無関係な形状まで
            // 「隣接」とみなしてしまうため、Y範囲が重なっている場合に限定する
            // (実データで、別行の無関係な形状を誤検出する回帰を確認したため必須の条件)。
            int fixedWindowWidth = (int)wHi;
            bool recoveredViaAdjacency = false;
            foreach (var f in accepted)
            {
                bool sameRow = oversize.Y < f.Y + f.Height && f.Y < oversize.Y + oversize.Height;
                if (!sameRow)
                    continue;

                if (Math.Abs(oversize.Right - f.X) < adjacencyThreshold)
                {
                    TryRecoverSide(gray, oversize.Y, oversize.Height,
                        f.X - fixedWindowWidth, fixedWindowWidth,
                        wLo, wHi, hLo, hHi, result);
                    recoveredViaAdjacency = true;
                }
                else if (Math.Abs(f.Right - oversize.X) < adjacencyThreshold)
                {
                    TryRecoverSide(gray, oversize.Y, oversize.Height,
                        f.Right, fixedWindowWidth,
                        wLo, wHi, hLo, hHi, result);
                    recoveredViaAdjacency = true;
                }
            }

            if (recoveredViaAdjacency)
                continue;

            // 同じ行に確定フレームが1つも無い(=護石の唯一のスロットが丸ごと融合した、
            // または他候補が全て別行のノイズだった)場合のフォールバック。手掛かりが無いため
            // 塊自体の左端・右端それぞれから固定幅の窓で再探索し、有効な候補が出た方を採用する
            // (実データで、真のソケット枠は融合塊の左端から始まるケースを確認済みだが、
            // 将来別の画面パターンで右端から始まるケースもありうるため両側を試す)。
            TryRecoverSide(gray, oversize.Y, oversize.Height,
                oversize.X, fixedWindowWidth,
                wLo, wHi, hLo, hHi, result);
            TryRecoverSide(gray, oversize.Y, oversize.Height,
                oversize.Right - fixedWindowWidth, fixedWindowWidth,
                wLo, wHi, hLo, hHi, result);
        }

        result.Sort((a, b) => a.X.CompareTo(b.X));
        return result;
    }

    private static void TryRecoverSide(
        Mat gray, int y, int height, int subX, int subWidth,
        double wLo, double wHi, double hLo, double hHi,
        List<Rect> outResult)
    {
        if (subWidth < wLo)
            return;

        var subRect = new Rect(subX, y, subWidth, height);
        var bounds = new Rect(0, 0, gray.Cols, gray.Rows);
        subRect = subRect.Intersect(bounds);
        if (subRect.Width <= 0 || subRect.Height <= 0)
            return;

        using var subGray = new Mat(gray, subRect);
        using var subEdges = new Mat();
        Cv2.Canny(subGray, subEdges, 50, 150);
        Cv2.FindContours(subEdges, out Point[][] subContours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var candidates = new List<Rect>();
        foreach (var contour in subContours)
        {
            var r = Cv2.BoundingRect(contour);
            if (r.Width > wLo && r.Width < wHi && r.Height > hLo && r.Height < hHi)
                candidates.Add(r);
        }

        if (candidates.Count == 0)
            return;

        Rect recovered;
        if (candidates.Count == 1)
        {
            recovered = candidates[0];
        }
        else if (subWidth < wHi * 1.5)
        {
            // 窓の幅が1個分のアイコン相当のため、複数候補は同一アイコンの内側/外側境界の
            // 重複検出である可能性が高いと判断し、最大面積のものを採用する
            // (MergeOverlappingが近接候補から大きい方を選ぶのと同じ考え方)。
            recovered = candidates.MaxBy(c => c.Width * c.Height);
        }
        else
        {
            // 窓が複数アイコン分の幅を持ちうる場合、どれが正しいか判断できないため採用しない
            return;
        }

        var recoveredRect = new Rect(recovered.X + subRect.X, recovered.Y + subRect.Y, recovered.Width, recovered.Height);

        // 複数のoversizedCandidatesが同じ枠を回復した場合や、1つの内包塊内に確定フレームが
        // 複数含まれる場合に、同一の枠を重複して追加してしまうことを防ぐ
        // (X範囲が重なっていれば同一アイコンの重複検出とみなす)
        if (outResult.Any(existing => RangesOverlap(existing, recoveredRect)))
            return;

        outResult.Add(recoveredRect);
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
    /// </summary>
    public static EmptyTemplateMatch MatchEmptyTemplate(Mat colorRegion, Rect frame)
    {
        int y0 = frame.Y + (int)(frame.Height * SlotIconConstants.LevelCropTopFraction);
        var bandRect = new Rect(frame.X, y0, frame.Width, frame.Y + frame.Height - y0);
        using var band = new Mat(colorRegion, bandRect);
        using var resized = new Mat();
        Cv2.Resize(band, resized, new Size(EmptySlotTemplates.TemplateWidth, EmptySlotTemplates.TemplateHeight));

        var candidates = new (SlotLevel Level, byte[][] Templates)[]
        {
            (SlotLevel.Lv1, EmptySlotTemplates.Lv1),
            (SlotLevel.Lv2, EmptySlotTemplates.Lv2),
            (SlotLevel.Lv3, EmptySlotTemplates.Lv3),
        };

        SlotLevel bestLevel = SlotLevel.Unknown;
        double bestDiff = double.MaxValue;
        foreach (var (level, templates) in candidates)
        {
            foreach (var template in templates)
            {
                double diff = MeanAbsDiff(resized, template);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestLevel = level;
                }
            }
        }

        bool isEmpty = bestDiff <= SlotIconConstants.EmptyTemplateThreshold;
        return new EmptyTemplateMatch(isEmpty ? bestLevel : null, bestDiff);
    }

    private static double MeanAbsDiff(Mat resizedBgr, byte[] template)
    {
        int w = EmptySlotTemplates.TemplateWidth;
        int h = EmptySlotTemplates.TemplateHeight;
        long sum = 0;
        int idx = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var px = resizedBgr.Get<Vec3b>(y, x);
                sum += Math.Abs(px.Item0 - template[idx])
                     + Math.Abs(px.Item1 - template[idx + 1])
                     + Math.Abs(px.Item2 - template[idx + 2]);
                idx += 3;
            }
        }
        return sum / (double)(w * h * 3);
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
