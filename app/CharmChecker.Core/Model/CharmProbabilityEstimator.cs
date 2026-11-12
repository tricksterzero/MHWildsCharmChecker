namespace CharmChecker.Core.Model;

public static class CharmProbabilityEstimator
{
    public static double? Estimate(Charm charm) =>
        Estimate(charm, CharmCombinationTable.LoadSkillGroups(), CharmCombinationTable.LoadCombinations());

    public static double? Estimate(
        Charm charm,
        IReadOnlyList<SkillGroupEntry> skillGroups,
        IReadOnlyList<CharmCombination> combinations)
    {
        if (charm.Rarity is not int rarity)
            return null;
        if (charm.Skills.Count == 0)
            return null;

        var rarityEntries = combinations.Where(c => c.Rarity == rarity).ToList();
        if (rarityEntries.Count == 0)
            return null;

        var matched = CharmCombinationTable.MatchingCombinations(charm, skillGroups, rarityEntries).FirstOrDefault();
        if (matched is null)
            return null;

        var matchedSlot = CharmCombinationTable.FindMatchingSlot(matched.Slots, charm);
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
}
