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

        var frames = new List<Rect>();
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width > wLo && rect.Width < wHi
                && rect.Height > hLo && rect.Height < hHi
                && rect.Y >= yMin)
            {
                frames.Add(rect);
            }
        }

        return frames.OrderBy(f => f.X).ToList();
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
        if (n == 2 && ratios[0] is double r0 && r0 < 0.5)
        {
            level = SlotLevel.Lv1;
        }
        else if (n == 2 && ratios[0] is double r1 && r1 >= 0.55)
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

    /// <summary>
    /// 枠右上のバッジ領域を明るさ(閾値150)で検出し、最大の明部を(32,32)に正規化して返す。
    /// 検出できなければnull。
    /// </summary>
    public static Mat? ExtractBadge(Mat img, Rect frame, double sx, double sy)
    {
        int y0 = Math.Max(0, (int)(frame.Y + SlotIconConstants.BadgeOffsetTop * sy));
        int y1 = (int)(frame.Y + SlotIconConstants.BadgeOffsetBottom * sy);
        int x0 = (int)(frame.X + frame.Width + SlotIconConstants.BadgeOffsetLeft * sx);
        int x1 = (int)(frame.X + frame.Width + SlotIconConstants.BadgeOffsetRight * sx);

        using var region = new Mat(img, new Rect(x0, y0, x1 - x0, y1 - y0));
        using var gray = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        using var mask = new Mat();
        Cv2.Threshold(gray, mask, 150, 255, ThresholdTypes.Binary);

        Cv2.FindContours(mask, out Point[][] contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0) return null;

        var biggest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        var badgeRect = Cv2.BoundingRect(biggest);
        if (badgeRect.Width == 0 || badgeRect.Height == 0) return null;

        using var badge = new Mat(region, badgeRect);
        using var badgeGray = new Mat();
        Cv2.CvtColor(badge, badgeGray, ColorConversionCodes.BGR2GRAY);

        var resized = new Mat();
        Cv2.Resize(badgeGray, resized, new Size(32, 32));
        return resized;
    }

    /// <summary>
    /// バッジ画像を武器/防具の参照テンプレートとmatchTemplateで比較し、相関の高い方を種別と判定する。
    /// </summary>
    public static TypeClassification ClassifyType(Mat? badge, Mat refBuki, Mat refBougu)
    {
        if (badge is null)
        {
            return new TypeClassification(SlotType.Unknown, null, null);
        }

        using var resultBuki = new Mat();
        Cv2.MatchTemplate(badge, refBuki, resultBuki, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(resultBuki, out _, out double cBuki);

        using var resultBougu = new Mat();
        Cv2.MatchTemplate(badge, refBougu, resultBougu, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(resultBougu, out _, out double cBougu);

        var type = cBuki > cBougu ? SlotType.Weapon : SlotType.Armor;
        return new TypeClassification(type, cBuki, cBougu);
    }

    /// <summary>
    /// 既知のスクリーンショットから武器/防具バッジの参照テンプレートを生成する。
    /// </summary>
    /// <param name="assetsDir">スクリーンショットを格納したディレクトリ(assets/)。</param>
    public static (Mat RefBuki, Mat RefBougu) BuildRefs(string assetsDir)
    {
        // buki ref: 20260614061441 右パネル frames[0]（剣バッジ確認済み）
        var refBuki = ExtractBadgeFromAsset(assetsDir, "20260614061441_1.jpg", frameIndex: 0);
        // bougu ref: 20260614061312 右パネル frame0（兜バッジ・Lv3確認済み）
        var refBougu = ExtractBadgeFromAsset(assetsDir, "20260614061312_1.jpg", frameIndex: 0);

        if (refBuki is null || refBougu is null)
        {
            throw new InvalidOperationException("参照バッジテンプレートの生成に失敗しました。");
        }

        return (refBuki, refBougu);
    }

    private static Mat? ExtractBadgeFromAsset(string assetsDir, string fileName, int frameIndex)
    {
        using var img = Cv2.ImRead(Path.Combine(assetsDir, fileName));
        var (sx, sy) = ScaleFactors(img);
        using var region = new Mat(img, PanelRegion(img));
        using var gray = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        var frames = DetectFrames(gray, sx, sy);
        return ExtractBadge(region, frames[frameIndex], sx, sy);
    }
}
