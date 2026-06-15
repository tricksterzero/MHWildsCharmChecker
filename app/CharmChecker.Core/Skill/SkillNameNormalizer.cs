namespace CharmChecker.Core.Skill;

public static class SkillNameNormalizer
{
    private static readonly Dictionary<char, char> DakutenMap = BuildDakutenMap();

    private static Dictionary<char, char> BuildDakutenMap()
    {
        const string from = "ガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポ";
        const string to   = "カキクケコサシスセソタチツテトハヒフヘホハヒフヘホ";
        var map = new Dictionary<char, char>(from.Length);
        for (int i = 0; i < from.Length; i++)
            map[from[i]] = to[i];
        return map;
    }

    /// <summary>
    /// OCRテキストを既知スキル名リスト（長い順）と照合し、正規スキル名を返す。
    /// 部分一致(先頭ゴミ文字対応) + 濁点フォールバック。
    /// </summary>
    public static string? Normalize(string ocrText, IReadOnlyList<string> knownSkills)
    {
        var text = ocrText.Trim();
        if (text.Length == 0)
            return null;

        if (knownSkills.Contains(text))
            return text;

        foreach (var skill in knownSkills)
        {
            if (text.Contains(skill, StringComparison.Ordinal))
                return skill;
        }

        var textStripped = StripDakuten(text);
        foreach (var skill in knownSkills)
        {
            if (textStripped.Contains(StripDakuten(skill), StringComparison.Ordinal))
                return skill;
        }

        return null;
    }

    private static string StripDakuten(string s)
    {
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (DakutenMap.TryGetValue(chars[i], out var replacement))
                chars[i] = replacement;
        }
        return new string(chars);
    }
}
