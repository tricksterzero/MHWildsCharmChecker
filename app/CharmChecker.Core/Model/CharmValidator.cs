using CharmChecker.Core.SlotIcon;

namespace CharmChecker.Core.Model;

public enum CharmIssueSeverity
{
    /// <summary>取り込んでよいが、知らせる(候補にないスキル名)。</summary>
    Warning,
    /// <summary>新しく取り込まない(ゲームにありえない値)。</summary>
    Error,
}

public sealed record CharmIssue(CharmIssueSeverity Severity, string Message);

/// <summary>
/// 護石がゲームとしてありうるかを確かめる(<see cref="Charm"/>の不変条件とは別の検査)。手動入力(CharmEditWindow)と
/// CSV・JSONの取り込み(<see cref="CharmImportScreening"/>)がこの判定を使い、入口ごとに警告・エラーの扱いだけを変える(2026-11-08)。
/// スクショの読み取りはこれを呼ばず、構成表との照合(<see cref="CharmCombinationValidator"/>)とスロットの検査
/// (<see cref="SlotValidation.TryValidate"/>)で、ありえない読み取り結果を除外する。
/// </summary>
public static class CharmValidator
{
    public static IReadOnlyList<CharmIssue> Validate(Charm charm) =>
        Validate(charm, CharmCombinationTable.LoadSkillGroups());

    /// <summary>
    /// 事前条件: 引数はnullでない。事後条件: 問題がなければ空のリスト。副作用なし。
    /// - Warning: スキル名がskill-groupsにない(ゲームの更新で増えたスキルかもしれないため、取り込みは止めない)
    /// - Error: スキルが0個 / 既知のスキル名なのにそのLvがskill-groupsにない / スロットの形が現行ゲームにない
    ///   (<see cref="SlotValidation.TryValidate"/>がnull)
    /// </summary>
    public static IReadOnlyList<CharmIssue> Validate(Charm charm, IReadOnlyList<SkillGroupEntry> skillGroups)
    {
        ArgumentNullException.ThrowIfNull(charm);
        ArgumentNullException.ThrowIfNull(skillGroups);

        var issues = new List<CharmIssue>();
        if (charm.Skills.Count == 0)
            issues.Add(new(CharmIssueSeverity.Error, "スキルがありません。"));

        foreach (var skill in charm.Skills)
        {
            var levels = skillGroups.Where(g => g.Name == skill.Name).Select(g => g.Level).ToList();
            if (levels.Count == 0)
                issues.Add(new(CharmIssueSeverity.Warning, $"「{skill.Name}」はスキル候補一覧にありません。"));
            else if (!levels.Contains(skill.Lv))
                issues.Add(new(CharmIssueSeverity.Error, $"「{skill.Name}」にLv{skill.Lv}は存在しません。"));
        }

        if (SlotValidation.TryValidate(charm.ArmorSlots, charm.WeaponSlots) is null)
            issues.Add(new(CharmIssueSeverity.Error,
                $"スロットの形が現行ゲームにありません(防具[{string.Join(",", charm.ArmorSlots)}]・武器[{string.Join(",", charm.WeaponSlots)}])。"));

        return issues;
    }

    /// <summary>Errorが1つでもあればtrue。</summary>
    public static bool HasError(IReadOnlyList<CharmIssue> issues) =>
        issues.Any(i => i.Severity == CharmIssueSeverity.Error);

    /// <summary>
    /// Errorのメッセージを元の順のまま返す(Warningは含めない。Errorがなければ空。副作用なし)。
    /// 事前条件: issuesはnullでない。
    /// </summary>
    public static IReadOnlyList<string> ErrorMessages(IReadOnlyList<CharmIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return issues.Where(i => i.Severity == CharmIssueSeverity.Error).Select(i => i.Message).ToList();
    }
}
