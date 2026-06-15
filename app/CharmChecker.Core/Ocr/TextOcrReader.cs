using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CharmChecker.Core.Ocr;

/// <summary>
/// Windows.Media.Ocr を使ったテキスト認識の薄いラッパー。
/// </summary>
public static class TextOcrReader
{
    /// <summary>
    /// 画像ファイルに対してOCRを実行し、認識結果を返す。
    /// </summary>
    public static async Task<OcrResult> RecognizeAsync(string imagePath, string languageTag = "ja")
    {
        var engine = CreateEngine(languageTag);

        using var stream = await FileRandomAccessStream.OpenAsync(imagePath, FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        return await engine.RecognizeAsync(bitmap);
    }

    /// <summary>
    /// BGRAバイト列に対してOCRを実行し、認識結果を返す。
    /// OpenCvSharpのMatから直接OCRする場合に使う。
    /// </summary>
    public static async Task<OcrResult> RecognizeBytesAsync(byte[] bgraBytes, int width, int height, string languageTag = "ja")
    {
        var engine = CreateEngine(languageTag);

        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(bgraBytes.AsBuffer());

        return await engine.RecognizeAsync(bitmap);
    }

    private static OcrEngine CreateEngine(string languageTag)
    {
        return OcrEngine.TryCreateFromLanguage(new Language(languageTag))
            ?? throw new InvalidOperationException($"OCR言語パック'{languageTag}'が利用できません。");
    }
}
