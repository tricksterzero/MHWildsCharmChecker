namespace CharmChecker.Core.Skill;

public static class LvParser
{
    /// <summary>
    /// "Lv3", "LV1", "しV2" 等のOCR文字列からレベル数値を抽出する。
    /// </summary>
    public static int? Parse(string text)
    {
        var t = text.Trim()
            .Replace("し", "L")
            .Replace("l", "L")
            .Replace("ー", "")
            .ToUpperInvariant();

        if (!t.StartsWith("LV", StringComparison.Ordinal))
            return null;

        var rest = t[2..].Replace("I", "1");
        var digits = new string(rest.TakeWhile(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
            return null;

        return int.TryParse(digits, out var value) ? value : null;
    }
}
