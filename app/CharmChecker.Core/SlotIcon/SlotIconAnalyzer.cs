using OpenCvSharp;

namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// legacy/slot-icon-pipeline/pipeline.py で検証済みのスロットアイコン判定ロジックの移植。
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
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width > wLo && rect.Width < wHi
                && rect.Height > hLo && rect.Height < hHi
                && rect.Y >= yMin)
            {
                raw.Add(rect);
            }
        }

        raw.Sort((a, b) => a.X.CompareTo(b.X));
        var merged = MergeOverlapping(raw, 20 * sx);
        return FilterYCluster(merged, 15 * sy);
    }

    /// <summary>
    /// 枠の下45%領域を(50,20)に正規化・Otsu二値化し、列ごとの白画素数プロファイルの
    /// ピーク数・谷比率からLv1/Lv2/Lv3を判定する。
    /// </summary>
    public static LevelClassification ClassifyLevel(Mat gray, Rect frame)
    {
        int y0 = frame.Y + (int)(frame.Height * 0.45);
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
        var best = groups.MaxBy(g => (g.Count, g.Sum(f => f.Width * f.Height)))!;
        var bestSet = new HashSet<Rect>(best);
        return frames.Where(f => bestSet.Contains(f)).ToList();
    }

}
