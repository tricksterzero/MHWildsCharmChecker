namespace CharmChecker.Core;

/// <summary>
/// CharmChecker.Coreに埋め込んだリソース(CharmChecker.Core.csprojのEmbeddedResourceで取り込んだresources/*.json)を読む。
/// </summary>
public static class EmbeddedResources
{
    /// <summary>
    /// 埋め込みリソース「CharmChecker.Core.Resources.<paramref name="fileName"/>」の中身をテキストとして返す。
    /// 事前条件: fileNameは空・空白でない(満たさなければArgumentException)。
    /// 見つからなければInvalidOperationExceptionを投げる。
    /// </summary>
    public static string ReadText(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var asm = typeof(EmbeddedResources).Assembly;
        using var stream = asm.GetManifestResourceStream($"CharmChecker.Core.Resources.{fileName}")
            ?? throw new InvalidOperationException($"埋め込みリソース '{fileName}' が見つかりません。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
