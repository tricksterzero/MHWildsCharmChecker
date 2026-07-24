using OpenCvSharp;

namespace CharmChecker.Core.Image;

/// <summary>
/// 21:9等のウルトラワイド設定でキャプチャされたスクリーンショット(上下に黒帯付き、
/// 2560x1440キャンバスに21:9映像がレターボックスされたもの)のコンテンツ境界を検出する。
///
/// 実測(2026-07-24, 装備の確認・売却画面3枚)で、21:9キャプチャはゲーム内UIが右上コーナーを
/// 基準に、コンテンツ高さ/1440の比率で等方的に縮小配置されていることを確認済み
/// (例: 16:9でのアンカー右端距離478px・Y132pxに対し、21:9(コンテンツ高1097px、scale=0.762)では
/// 478*0.762=364px・132*0.762=101px(黒帯171pxを加えた画像全体Y=272px)とほぼ一致)。
///
/// このscaleは、SkillReadingPipeline.CropSkillAreaのような固定px幾何オフセットの補正にのみ
/// 使う。フルスクリーンOCR(アンカー・護石名等のテキスト検出)は、テキスト自体の座標を
/// そのまま拾う設計のため生画像で行うべきで、事前に画像全体を拡大するとかえって特定パネルの
/// テキスト認識が崩れるケースが実験で確認された(全画面を1.3125倍→Cubic補間した画像で、
/// 元は正しく読めていた右パネルの「フォースショット」「爆破やられ耐性」が文字化けした)。
/// そのため、このクラスはMatを加工せず境界とscaleのみを返す。
/// </summary>
public static class LetterboxNormalizer
{
    public const int ReferenceHeight = 1440;

    /// <summary>
    /// 行の平均輝度がこの値以下なら黒帯(レターボックス)の一部とみなす。
    /// </summary>
    private const double BlackRowMeanThreshold = 8.0;

    /// <summary>
    /// 上下の黒帯を検出し、コンテンツの行範囲と基準解像度1440に対するスケール係数
    /// (contentHeight/1440)を返す。黒帯が無い通常の16:9画像ではTop=0・Bottom=画像高さ-1・
    /// Scale=1.0になる。
    /// </summary>
    public static (int Top, int Bottom, double Scale) DetectContentBounds(Mat src)
    {
        var (top, bottom) = FindContentRowBounds(src);
        int contentHeight = bottom - top + 1;
        double scale = (double)contentHeight / ReferenceHeight;
        return (top, bottom, scale);
    }

    private static (int Top, int Bottom) FindContentRowBounds(Mat src)
    {
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        int h = gray.Rows;
        int top = 0;
        while (top < h && RowMean(gray, top) <= BlackRowMeanThreshold)
            top++;

        int bottom = h - 1;
        while (bottom > top && RowMean(gray, bottom) <= BlackRowMeanThreshold)
            bottom--;

        return (top, bottom);
    }

    private static double RowMean(Mat gray, int row)
    {
        using var rowMat = gray.Row(row);
        return Cv2.Mean(rowMat).Val0;
    }
}
