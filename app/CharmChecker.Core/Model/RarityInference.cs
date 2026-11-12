namespace CharmChecker.Core.Model;

/// <summary>
/// 護石のスキル・スロット構成からRAREを推定する。構成表の読み込みと照合は<see cref="CharmCombinationTable"/>が担う。
/// </summary>
public static class RarityInference
{
    public static int? Infer(Charm charm)
    {
        var skillGroups = CharmCombinationTable.LoadSkillGroups();
        var combinations = CharmCombinationTable.LoadCombinations();
        return Infer(charm, skillGroups, combinations);
    }

    public static int? Infer(
        Charm charm,
        IReadOnlyList<SkillGroupEntry> skillGroups,
        IReadOnlyList<CharmCombination> combinations)
    {
        if (charm.WeaponSlots.Any(v => v > 0))
            return 8;

        if (charm.Skills.Any(s => s.Name == CharmCombinationTable.KensanSkillName))
            return 5;

        if (charm.Skills.Count == 0)
            return null;

        var matchedRarities = CharmCombinationTable.MatchingCombinations(charm, skillGroups, combinations)
            .Select(c => c.Rarity)
            .ToHashSet();

        if (matchedRarities.Count == 1)
            return matchedRarities.First();

        if (matchedRarities.Contains(7) && matchedRarities.Contains(8))
        {
            matchedRarities.Remove(8);
            if (matchedRarities.Count == 1)
                return matchedRarities.First();
        }

        return null;
    }

    /// <summary>
    /// RAREが空の護石だけ、推定したRAREを入れた新しいインスタンスを返す(RAREがあればそのまま返す。副作用なし)。
    /// 事前条件: charmはnullでない。
    /// </summary>
    public static Charm WithInferredRarity(Charm charm)
    {
        ArgumentNullException.ThrowIfNull(charm);
        return charm.Rarity is null ? charm.WithRarity(Infer(charm)) : charm;
    }
}
