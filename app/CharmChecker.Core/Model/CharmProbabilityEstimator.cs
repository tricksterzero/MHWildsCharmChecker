namespace CharmChecker.Core.Model;

public static class CharmProbabilityEstimator
{
    public static double? Estimate(Charm charm) =>
        Estimate(charm, RarityInference.LoadSkillGroups(), RarityInference.LoadCombinations());

    public static double? Estimate(
        Charm charm,
        IReadOnlyList<SkillGroupEntry> skillGroups,
        IReadOnlyList<CharmCombination> combinations)
    {
        if (charm.Rarity is not int rarity)
            return null;
        if (charm.Skills.Count == 0)
            return null;

        var possibleGroupsPerSkill = RarityInference.ResolvePossibleGroups(charm, skillGroups);
        if (possibleGroupsPerSkill is null)
            return null;

        var rarityEntries = combinations.Where(c => c.Rarity == rarity).ToList();
        if (rarityEntries.Count == 0)
            return null;

        var matched = rarityEntries.FirstOrDefault(c =>
            c.SkillGroups.Length == charm.Skills.Count &&
            RarityInference.MatchesGroupPattern(possibleGroupsPerSkill, c.SkillGroups));
        if (matched is null)
            return null;

        var matchedSlot = FindMatchingSlot(matched, charm);
        if (matchedSlot is null)
            return null;

        double tableProbability = 1.0 / rarityEntries.Count;
        double slotProbability = matchedSlot.Weight ?? 1.0 / matched.Slots.Length;

        double skillProbability = 1.0;
        foreach (var group in matched.SkillGroups)
        {
            int count = skillGroups.Count(e => e.Groups.Contains(group));
            if (count == 0)
                return null;
            skillProbability *= 1.0 / count;
        }

        return tableProbability * slotProbability * skillProbability;
    }

    // スロットパターンごとに配列長が異なる（例: RARE8は[1]と[1,1]が混在）ため、
    // パターンが宣言する個数だけ上位を切り出して比較し、残りが全て0であることも確認する
    private static CharmCombinationSlot? FindMatchingSlot(CharmCombination combo, Charm charm)
    {
        var armorSorted = charm.ArmorSlots.OrderByDescending(v => v).ToArray();
        var weaponSorted = charm.WeaponSlots.OrderByDescending(v => v).ToArray();

        return combo.Slots.FirstOrDefault(s =>
            armorSorted.Take(s.Armor.Length).SequenceEqual(s.Armor) &&
            armorSorted.Skip(s.Armor.Length).All(v => v == 0) &&
            weaponSorted.Take(s.Weapon.Length).SequenceEqual(s.Weapon) &&
            weaponSorted.Skip(s.Weapon.Length).All(v => v == 0));
    }
}
