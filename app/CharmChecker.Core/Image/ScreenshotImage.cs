using OpenCvSharp;

namespace CharmChecker.Core.Image;

/// <summary>スクショのファイルを、読み取りに使うBGRの画像として読み込む。</summary>
internal static class ScreenshotImage
{
    /// <summary>
    /// ファイルを3チャネル(BGR)の画像として読み込む。呼び出し元がDisposeする。
    /// 事前条件: imagePathは空でない。壊れた画像や画像でないファイルではImReadが空の画像を返し、そのままだと
    /// 後段でOpenCVの分かりにくい例外("!_src.empty()")になるため、<see cref="InvalidDataException"/>を投げる。
    /// 事後条件: 戻り値は空でない3チャネル(BGR)の画像。
    /// </summary>
    public static Mat Read(string imagePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(imagePath);

        var img = Cv2.ImRead(imagePath);
        if (img.Empty())
        {
            img.Dispose();
            throw new InvalidDataException($"画像を読み込めませんでした: {imagePath}");
        }
        return img;
    }

    /// <summary>
    /// 読み取りに渡された画像が空でない3チャネル(BGR)か確かめる。満たさなければ例外を投げる。
    /// </summary>
    internal static void ThrowIfNotBgr(Mat img, string paramName)
    {
        ArgumentNullException.ThrowIfNull(img, paramName);
        if (img.Empty() || img.Type() != MatType.CV_8UC3)
            throw new ArgumentException("画像は空でない3チャネル(BGR)である必要があります。", paramName);
    }
}
