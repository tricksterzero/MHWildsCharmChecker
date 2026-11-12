namespace CharmChecker.Core.Model;

/// <summary>
/// 護石のスキル・スロット構成が、構成表(charm-combinations.json)のいずれかの組み合わせに
/// 当てはまるかを判定する。スクショの読み取りで、スキルの読み落とし(例: 2スキルのうち1つしか
/// 読めない)やスロットLvの読み違いによる「実在しない護石」を保存せずに除外するために使う
/// (2026-11-07)。構成表の組み合わせはすべてスキル2つ以上のため、スキルが1つしか読めなかった
/// 結果は必ず当てはまらない。
/// </summary>
public static class CharmCombinationValidator
{
    public static bool MatchesAnyCombination(Charm charm) =>
        MatchesAnyCombination(charm, RarityInference.LoadSkillGroups(), RarityInference.LoadCombinations());

    /// <summary>
    /// 事前条件: 引数はいずれもnullでない。
    /// 事後条件(戻り値): 研鑽を含む護石は構成表の対象外のためtrue。それ以外は、Rarityが分かっていれば
    /// そのRARE、不明なら全RAREの組み合わせのうち、スキル(名前とLvから引けるグループ)とスロットの
    /// 両方が一致するものが1つでもあればtrue。スキルが無い、またはskill-groupsに無いスキル・Lvを
    /// 含む場合はfalse。副作用なし。
    /// </summary>
    public static bool MatchesAnyCombination(
        Charm charm,
        IReadOnlyList<SkillGroupEntry> skillGroups,
        IReadOnlyList<CharmCombination> combinations)
    {
        ArgumentNullException.ThrowIfNull(charm);
        ArgumentNullException.ThrowIfNull(skillGroups);
        ArgumentNullException.ThrowIfNull(combinations);

        if (charm.Skills.Any(s => s.Name == RarityInference.KensanSkillName))
            return true;
        if (charm.Skills.Count == 0)
            return false;

        var sameRarity = combinations.Where(c => charm.Rarity is null || c.Rarity == charm.Rarity);
        return RarityInference.MatchingCombinations(charm, skillGroups, sameRarity)
            .Any(c => RarityInference.FindMatchingSlot(c.Slots, charm) is not null);
    }
}
