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
    /// <param name="imagePath">画像ファイルのパス。</param>
    /// <param name="languageTag">OCRに使う言語タグ（既定は日本語）。</param>
    public static async Task<OcrResult> RecognizeAsync(string imagePath, string languageTag = "ja")
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language(languageTag))
            ?? throw new InvalidOperationException($"OCR言語パック'{languageTag}'が利用できません。");

        using var stream = await FileRandomAccessStream.OpenAsync(imagePath, FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        return await engine.RecognizeAsync(bitmap);
    }
}
