namespace CharmChecker.Core.Model;

/// <summary>CSVの取り込みの検査結果。Errorがあれば取り込む護石は空で、最初のErrorの行とその文を持つ。</summary>
public sealed class CsvImportScreening
{
    /// <summary>取り込む護石(RAREを推定したもの、元の順)。Errorがあれば空。</summary>
    public IReadOnlyList<Charm> Accepted { get; }

    /// <summary>警告(候補にないスキル名)のある行の数。Errorがあれば0。</summary>
    public int WarningCount { get; }

    /// <summary>最初にErrorがあった行(1始まり)。Errorがなければnull。</summary>
    public int? ErrorLineNumber { get; }

    /// <summary>最初にErrorがあった行のErrorの文。Errorがなければ空。</summary>
    public IReadOnlyList<string> ErrorMessages { get; }

    public bool HasError => ErrorLineNumber is not null;

    internal CsvImportScreening(IReadOnlyList<Charm> accepted, int warningCount, int? errorLineNumber, IReadOnlyList<string> errorMessages)
    {
        Accepted = accepted;
        WarningCount = warningCount;
        ErrorLineNumber = errorLineNumber;
        ErrorMessages = errorMessages;
    }
}

/// <summary>JSONの取り込みの検査結果。Errorの項目だけを除く。</summary>
public sealed class JsonImportScreening
{
    /// <summary>取り込む護石(RAREを推定したもの、元の順)。</summary>
    public IReadOnlyList<Charm> Accepted { get; }

    /// <summary>取り込まない項目の理由(不変条件を満たさず読めなかった項目、続いてErrorのある項目)。</summary>
    public IReadOnlyList<string> ExcludedReasons { get; }

    /// <summary>取り込む護石のうち、警告(候補にないスキル名)のあるものの数。</summary>
    public int WarningCount { get; }

    internal JsonImportScreening(IReadOnlyList<Charm> accepted, IReadOnlyList<string> excludedReasons, int warningCount)
    {
        Accepted = accepted;
        ExcludedReasons = excludedReasons;
        WarningCount = warningCount;
    }
}

/// <summary>
/// CSV・JSONから取り込む護石を、ゲームとしてありうるか(<see cref="CharmValidator"/>)で検査し、取り込むものを決める(副作用なし)。
/// 2026-11-12にMainWindowから移した(扱いは変えていない)。
/// </summary>
public static class CharmImportScreening
{
    /// <summary>
    /// CSVの取り込み: ゲームにありえない値(存在しないLv・スロットの形・スキル0個)は、形式エラーと同じく行番号付きで
    /// 全体を取り込まない。候補にないスキル名は警告として件数だけ知らせて取り込む。
    /// 事前条件: 引数はnullでない。
    /// </summary>
    public static CsvImportScreening ScreenCsv(IReadOnlyList<Charm> charms, IReadOnlyList<SkillGroupEntry> skillGroups)
    {
        ArgumentNullException.ThrowIfNull(charms);
        ArgumentNullException.ThrowIfNull(skillGroups);

        var issuesPerLine = charms.Select(c => CharmValidator.Validate(c, skillGroups)).ToList();
        int errorIndex = issuesPerLine.FindIndex(issues => CharmValidator.HasError(issues));
        if (errorIndex >= 0)
            return new CsvImportScreening([], 0, errorIndex + 1, CharmValidator.ErrorMessages(issuesPerLine[errorIndex]));

        int warningCount = issuesPerLine.Count(issues => issues.Count > 0);
        var accepted = charms.Select(RarityInference.WithInferredRarity).ToList();
        return new CsvImportScreening(accepted, warningCount, null, []);
    }

    /// <summary>
    /// JSONの取り込み: 新しく取り込むデータなので、不変条件の違反に加えてゲームにありえない値の項目も取り込まない
    /// (起動時の既存データの読み込みとは扱いが違う)。候補にないスキル名は警告として件数だけ知らせて取り込む。
    /// 事前条件: 引数はnullでない。
    /// </summary>
    public static JsonImportScreening ScreenJson(CharmJsonParseResult parseResult, IReadOnlyList<SkillGroupEntry> skillGroups)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(skillGroups);

        var excludedReasons = parseResult.Rejected.Select(r => $"{r.Index + 1}件目: {r.Reason}").ToList();
        var accepted = new List<Charm>();
        int warningCount = 0;
        foreach (var charm in parseResult.Charms)
        {
            var issues = CharmValidator.Validate(charm, skillGroups);
            if (CharmValidator.HasError(issues))
            {
                excludedReasons.Add(string.Join(" ", CharmValidator.ErrorMessages(issues)));
                continue;
            }
            if (issues.Count > 0) warningCount++;
            accepted.Add(RarityInference.WithInferredRarity(charm));
        }
        return new JsonImportScreening(accepted, excludedReasons, warningCount);
    }
}
