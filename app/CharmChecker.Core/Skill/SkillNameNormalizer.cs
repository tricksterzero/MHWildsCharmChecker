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

        // 「力」がカタカナの「カ」に誤認識されるOCRの癖に対応（正規名側に「カ」を含むスキルは
        // 存在しないため、変換後のテキストが正規名自体と衝突する心配はない。例:
        // 「火事場カ」→「火事場力」。ただしOCR誤読テキストが偶然別の正規名と部分一致する
        // 可能性まで排除するものではない）
        var textKanaFixed = text.Replace('カ', '力');
        var textStripped = StripDakuten(text);
        var textStrippedKanaFixed = StripDakuten(textKanaFixed);

        // 素の部分一致・カナ誤認フォールバック・濁点フォールバック・両方の組み合わせを、
        // スキルごとに(長い名前から順に)まとめて試す。フォールバック別に全スキルを1周ずつ
        // 試す(旧実装)と、「防御」⊂「防御力ＤＯＷＮ耐性」のような包含関係を持つ名前の組で、
        // 短い名前が素の部分一致で先に確定してしまい、フォールバックを要する長い名前の方が
        // 優先されるべき場面でも短い名前が採用されてしまう不具合があった。
        foreach (var skill in sortedSkills)
        {
            if (text.Contains(skill, StringComparison.Ordinal)
                || textKanaFixed.Contains(skill, StringComparison.Ordinal)
                || textStripped.Contains(StripDakuten(skill), StringComparison.Ordinal)
                || textStrippedKanaFixed.Contains(StripDakuten(skill), StringComparison.Ordinal))
            {
                return skill;
            }
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
