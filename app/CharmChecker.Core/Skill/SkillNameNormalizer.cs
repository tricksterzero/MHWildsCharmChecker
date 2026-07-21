namespace CharmChecker.Core.Skill;

public static class SkillNameNormalizer
{
    private static readonly Dictionary<char, char> DakutenMap = BuildDakutenMap();

    private static Dictionary<char, char> BuildDakutenMap()
    {
        const string from = "ガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポ"
                           + "がぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽ";
        const string to   = "カキクケコサシスセソタチツテトハヒフヘホハヒフヘホ"
                           + "かきくけこさしすせそたちつてとはひふへほはひふへほ";
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
        => Normalize(ocrText, knownSkills, null);

    public static string? Normalize(string ocrText, IReadOnlyList<string> knownSkills, HashSet<string>? knownSkillSet)
    {
        var text = ToFullWidthAscii(ocrText.Trim());
        if (text.Length == 0)
            return null;

        if (knownSkillSet is not null ? knownSkillSet.Contains(text) : knownSkills.Contains(text))
            return text;

        // 呼び出し元が長さ降順でない可能性があっても「長い名前優先」を守るため、ここで確定させる。
        var sortedSkills = knownSkills.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).ToList();

        foreach (var skill in sortedSkills)
        {
            if (text.Contains(skill, StringComparison.Ordinal))
                return skill;
        }

        // 「力」がカタカナの「カ」に誤認識されるOCRの癖に対応（正規名に「カ」を含むスキルは存在しないため、
        // 一方向の置換のみで安全に対応できる。例: 「火事場カ」→「火事場力」）
        var textKanaFixed = text.Replace('カ', '力');
        foreach (var skill in sortedSkills)
        {
            if (textKanaFixed.Contains(skill, StringComparison.Ordinal))
                return skill;
        }

        // 濁点フォールバック
        var textStripped = StripDakuten(text);
        foreach (var skill in sortedSkills)
        {
            if (textStripped.Contains(StripDakuten(skill), StringComparison.Ordinal))
                return skill;
        }

        // 濁点フォールバック + カ→力（両方の誤認識が重なるケースの保険）
        var textStrippedKanaFixed = StripDakuten(textKanaFixed);
        foreach (var skill in sortedSkills)
        {
            if (textStrippedKanaFixed.Contains(StripDakuten(skill), StringComparison.Ordinal))
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

    /// <summary>半角ASCII可視文字(!〜~)を全角に変換する。正規スキル名側の表記(ＫＯ術等)に合わせるため。</summary>
    private static string ToFullWidthAscii(string s)
    {
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '!' and <= '~')
                chars[i] = (char)(chars[i] + 0xFEE0);
        }
        return new string(chars);
    }
}
