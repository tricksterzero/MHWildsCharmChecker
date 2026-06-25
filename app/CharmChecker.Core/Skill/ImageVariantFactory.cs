using OpenCvSharp;

namespace CharmChecker.Core.Skill;

/// <summary>
/// スキル領域クロップから5種の前処理バリエーションを生成する。
/// </summary>
public static class ImageVariantFactory
{
    public readonly record struct Variant(string Name, Mat Image, int LvXThreshold);

    /// <summary>
    /// 5種のバリエーションを生成して返す。呼び出し元が各MatをDisposeすること。
    /// </summary>
    public static List<Variant> Create(Mat crop)
    {
        var variants = new List<Variant>(5);

        // (1) 原画
        variants.Add(new Variant("raw", crop.Clone(), 300));

        // (2) 左60pxカット
        Mat? trimmedClone = null;
        if (crop.Cols > 60)
        {
            using var trimmedSub = crop.ColRange(60, crop.Cols);
            trimmedClone = trimmedSub.Clone();
            variants.Add(new Variant("trim60", trimmedClone, 240));
        }

        // (3) Otsu二値化
        using var gray = new Mat();
        Cv2.CvtColor(crop, gray, ColorConversionCodes.BGR2GRAY);
        using var binarized = new Mat();
        Cv2.Threshold(gray, binarized, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        var binBgr = new Mat();
        Cv2.CvtColor(binarized, binBgr, ColorConversionCodes.GRAY2BGR);
        variants.Add(new Variant("otsu", binBgr, 300));

        // (4) トリミング + 二値化
        if (trimmedClone is not null)
        {
            using var grayT = new Mat();
            Cv2.CvtColor(trimmedClone, grayT, ColorConversionCodes.BGR2GRAY);
            using var binT = new Mat();
            Cv2.Threshold(grayT, binT, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            var binTBgr = new Mat();
            Cv2.CvtColor(binT, binTBgr, ColorConversionCodes.GRAY2BGR);
            variants.Add(new Variant("trim60_otsu", binTBgr, 240));
        }

        // (5) グレースケールのみ
        var grayBgr = new Mat();
        Cv2.CvtColor(gray, grayBgr, ColorConversionCodes.GRAY2BGR);
        variants.Add(new Variant("gray", grayBgr, 300));

        return variants;
    }
}
