using OpenCvSharp;

namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// legacy/slot-icon-pipeline/pipeline.py で検証済みのスロットアイコン判定ロジックの移植を土台に、
/// 実データでのチューニングを加えたもの（Lv1/Lv2判定の閾値・n==1の扱いがPoCと異なる。
/// legacy側はビルド対象外の参照記録のため未追随）。
/// 詳細仕様はリポジトリルートのCLAUDE.mdを参照。
/// </summary>
public static class SlotIconAnalyzer
{
    /// <summary>基準解像度(2560x1440)に対する実画像の拡大率(sx, sy)。</summary>
    public static (double Sx, double Sy) ScaleFactors(Mat img)
    {
        return ((double)img.Width / SlotIconConstants.RefWidth, (double)img.Height / SlotIconConstants.RefHeight);
    }

    /// <summary>単一護石詳細画面のスロットアイコン探索領域を、実画像サイズに合わせて切り出す矩形。</summary>
    public static Rect DetailPanelRegion(Mat img)
    {
        int w = img.Width;
        int h = img.Height;
        int y0 = (int)(SlotIconConstants.DetailPanelY0Frac * h);
        int y1 = (int)(SlotIconConstants.DetailPanelY1Frac * h);
        int x0 = (int)(SlotIconConstants.DetailPanelX0Frac * w);
        int x1 = (int)(SlotIconConstants.DetailPanelX1Frac * w);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>装備BOX側スロットアイコンの探索領域を、実画像サイズに合わせて切り出す矩形。</summary>
    public static Rect PanelRegion(Mat img)
    {
        int w = img.Width;
        int h = img.Height;
        int y0 = (int)(SlotIconConstants.PanelY0Frac * h);
        int y1 = (int)(SlotIconConstants.PanelY1Frac * h);
        int x0 = (int)(SlotIconConstants.PanelX0Frac * w);
        int x1 = (int)(SlotIconConstants.PanelX1Frac * w);
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
            filtered = TryRecoverFusedFrame(gray, filtered, oversized, wLo, wHi, hLo, hHi);

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
                if (f.X >= oversize.X && f.Right <= oversize.Right)
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
                }
                else if (Math.Abs(f.Right - oversize.X) < adjacencyThreshold)
                {
                    TryRecoverSide(gray, oversize.Y, oversize.Height,
                        f.Right, fixedWindowWidth,
                        wLo, wHi, hLo, hHi, result);
                }
            }
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

        outResult.Add(new Rect(recovered.X + subRect.X, recovered.Y + subRect.Y, recovered.Width, recovered.Height));
    }

    /// <summary>
    /// 枠の下45%領域を(50,20)に正規化・Otsu二値化し、列ごとの白画素数プロファイルの
    /// ピーク数・谷比率からLv1/Lv2/Lv3を判定する。
    /// </summary>
    public static LevelClassification ClassifyLevel(Mat gray, Rect frame)
    {
        int y0 = frame.Y + (int)(frame.Height * SlotIconConstants.LevelCropTopFraction);
        var cropRect = new Rect(frame.X, y0, frame.Width, frame.Y + frame.Height - y0);
        using var crop = new Mat(gray, cropRect);
        using var resized = new Mat();
        Cv2.Resize(crop, resized, new Size(50, 20));
        using var binImg = new Mat();
        Cv2.Threshold(resized, binImg, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        const int width = 50;
        const int height = 20;
        var profile = new double[width];
        for (int col = 0; col < width; col++)
        {
            int count = 0;
            for (int row = 0; row < height; row++)
            {
                if (binImg.Get<byte>(row, col) > 127) count++;
            }
            profile[col] = count;
        }

        // np.convolve(profile, ones(3)/3, mode="same") 相当の移動平均（範囲外は0扱い）
        var smoothed = new double[width];
        for (int i = 0; i < width; i++)
        {
            double sum = profile[i];
            sum += i > 0 ? profile[i - 1] : 0;
            sum += i < width - 1 ? profile[i + 1] : 0;
            smoothed[i] = sum / 3.0;
        }

        double peak = smoothed.Max();
        if (peak <= 0)
        {
            return new LevelClassification(SlotLevel.Unknown, 0, []);
        }
        double threshold = peak * 0.75;

        var groups = new List<List<int>>();
        List<int>? current = null;
        for (int i = 0; i < smoothed.Length; i++)
        {
            if (smoothed[i] >= threshold)
            {
                current ??= [];
                current.Add(i);
            }
            else if (current != null)
            {
                groups.Add(current);
                current = null;
            }
        }
        if (current != null) groups.Add(current);

        var ratios = new List<double?>();
        for (int i = 0; i < groups.Count - 1; i++)
        {
            int segStart = groups[i][^1] + 1;
            int segEnd = groups[i + 1][0];
            if (segEnd > segStart)
            {
                double segMin = double.MaxValue;
                for (int j = segStart; j < segEnd; j++)
                {
                    segMin = Math.Min(segMin, smoothed[j]);
                }
                ratios.Add(segMin / peak);
            }
            else
            {
                ratios.Add(null);
            }
        }

        int n = groups.Count;
        SlotLevel level;
        if (n == 1)
        {
            level = SlotLevel.Lv1;
        }
        else if (n == 2 && ratios[0] is double r0 && r0 < 0.5)
        {
            level = SlotLevel.Lv1;
        }
        else if (n == 2 && ratios[0] is double r1 && r1 >= 0.50)
        {
            level = SlotLevel.Lv2;
        }
        else if (n >= 3)
        {
            level = SlotLevel.Lv3;
        }
        else
        {
            level = SlotLevel.Unknown;
        }

        return new LevelClassification(level, n, ratios);
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
