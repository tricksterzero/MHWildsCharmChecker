using OpenCvSharp;

namespace CharmChecker.Core.Skill;

/// <summary>
/// スキル領域クロップから前処理バリエーションを生成する。元の大きさの5種に加え、クロップを
/// <see cref="ScaledFactor"/>倍に拡大した画像から同じ5種を作る(計10種)。
/// </summary>
public static class ImageVariantFactory
{
    /// <param name="Name">バリエーション名(拡大したものは末尾に"_x1.5")。</param>
    /// <param name="Image">前処理後の画像(BGR)。呼び出し元がDisposeする。</param>
    /// <param name="LvXThreshold">OCR結果を左(スキル名)・右(Lv)に分けるX座標。<paramref name="Image"/>の座標で持つ。</param>
    /// <param name="CoordinateScale"><paramref name="Image"/>の座標を元のクロップの座標に戻すときに割る値(拡大率)。</param>
    public readonly record struct Variant(string Name, Mat Image, int LvXThreshold, double CoordinateScale = 1.0);

    /// <summary>
    /// 拡大した前処理の倍率。Windows.Media.Ocrは短いスキル名(「巧撃」等の2文字)とそのLvの行を、
    /// 元の大きさでは前処理5種のどれでも検出できないことがあるが、クロップを拡大すると検出できる
    /// (2026-11-07、狩猟後の鑑定BOX画面で確認。1.25〜2倍で検出され、1.5倍では前処理によって
    /// 名前・Lvの取れ方がばらつくため5種すべてを作る)。護石画面の全159枚で、元の5種だけの場合と
    /// 結果が変わったのは読み落としていた1枚のみだった。
    /// </summary>
    public const double ScaledFactor = 1.5;

    private const int TrimWidth = 60;
    private const int FullLvXThreshold = 300;
    private const int TrimmedLvXThreshold = 240;

    /// <summary>
    /// 元の大きさの5種、続けて拡大した5種を返す(元の5種が先。件数が同じ場合に元の5種の結果を
    /// 優先させるため、この順序に意味がある)。呼び出し元が各MatをDisposeすること。
    /// 事前条件: cropはnullでなく、空でない3チャネル(BGR)の画像。
    /// </summary>
    public static List<Variant> Create(Mat crop)
    {
        ArgumentNullException.ThrowIfNull(crop);
        if (crop.Empty() || crop.Type() != MatType.CV_8UC3)
            throw new ArgumentException("cropは空でない3チャネル(BGR)の画像である必要があります。", nameof(crop));

        var variants = CreateSet(crop, 1.0, "");
        using var scaled = new Mat();
        Cv2.Resize(crop, scaled, new Size(), ScaledFactor, ScaledFactor, InterpolationFlags.Cubic);
        variants.AddRange(CreateSet(scaled, ScaledFactor, "_x1.5"));
        return variants;
    }

    /// <summary>
    /// 画像から5種(原画/左60pxカット/Otsu二値化/カット+二値化/グレースケール)を作る。
    /// トリミング幅は画像上の60pxのまま、Lvの境界はscale倍にする(拡大後の画像の座標で持つため)。
    /// </summary>
    private static List<Variant> CreateSet(Mat image, double scale, string suffix)
    {
        int fullThreshold = (int)Math.Round(FullLvXThreshold * scale);
        int trimmedThreshold = (int)Math.Round(TrimmedLvXThreshold * scale);
        var variants = new List<Variant>(5);

        // (1) 原画
        variants.Add(new Variant("raw" + suffix, image.Clone(), fullThreshold, scale));

        // (2) 左60pxカット
        Mat? trimmedClone = null;
        if (image.Cols > TrimWidth)
        {
            using var trimmedSub = image.ColRange(TrimWidth, image.Cols);
            trimmedClone = trimmedSub.Clone();
            variants.Add(new Variant("trim60" + suffix, trimmedClone, trimmedThreshold, scale));
        }

        // (3) Otsu二値化
        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        using var binarized = new Mat();
        Cv2.Threshold(gray, binarized, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        var binBgr = new Mat();
        Cv2.CvtColor(binarized, binBgr, ColorConversionCodes.GRAY2BGR);
        variants.Add(new Variant("otsu" + suffix, binBgr, fullThreshold, scale));

        // (4) トリミング + 二値化
        if (trimmedClone is not null)
        {
            using var grayT = new Mat();
            Cv2.CvtColor(trimmedClone, grayT, ColorConversionCodes.BGR2GRAY);
            using var binT = new Mat();
            Cv2.Threshold(grayT, binT, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            var binTBgr = new Mat();
            Cv2.CvtColor(binT, binTBgr, ColorConversionCodes.GRAY2BGR);
            variants.Add(new Variant("trim60_otsu" + suffix, binTBgr, trimmedThreshold, scale));
        }

        // (5) グレースケールのみ
        var grayBgr = new Mat();
        Cv2.CvtColor(gray, grayBgr, ColorConversionCodes.GRAY2BGR);
        variants.Add(new Variant("gray" + suffix, grayBgr, fullThreshold, scale));

        return variants;
    }
}
