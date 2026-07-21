namespace CharmChecker.Core.Model;

public static class CharmTheoreticalValueChecker
{
    public static bool? IsTheoretical(Charm charm) =>
        IsTheoretical(charm, RarityInference.LoadSkillGroups(), RarityInference.LoadCombinations());

    public static bool? IsTheoretical(
        Charm charm,
        IReadOnlyList<SkillGroupEntry> skillGroups,
        IReadOnlyList<CharmCombination> combinations)
    {
        if (charm.Skills.Count == 0)
            return null;

        foreach (var skill in charm.Skills)
        {
            int? maxLevel = null;
            foreach (var entry in skillGroups)
            {
                if (entry.Name != skill.Name)
                    continue;
                if (maxLevel is null || entry.Level > maxLevel)
                    maxLevel = entry.Level;
            }

            if (maxLevel is null)
                return null;
            if (skill.Lv != maxLevel)
                return false;
        }

        var possibleGroupsPerSkill = RarityInference.ResolvePossibleGroups(charm, skillGroups);
        if (possibleGroupsPerSkill is null)
            return null;

        var matched = combinations.FirstOrDefault(c =>
            c.SkillGroups.Length == charm.Skills.Count &&
            RarityInference.MatchesGroupPattern(possibleGroupsPerSkill, c.SkillGroups));
        if (matched is null)
            return null;

        // スロットの優劣は、同じスキルグループ構成を持つ組み合わせの間でのみ比較する
        // （例: RARE7とRARE8は同じスキルグループ構成を共有し、RARE8はRARE7に武器スロットを
        // 追加しただけの関係にあるため比較可能。一方、無関係な組み合わせ同士は
        // 防具スロットと武器スロットの価値を単純比較できないため対象に含めない）
        var comparableSlots = combinations
            .Where(c => c.SkillGroups.SequenceEqual(matched.SkillGroups))
            .SelectMany(c => c.Slots);

        return !IsSlotPatternDominated(charm, comparableSlots);
    }

    private static bool IsSlotPatternDominated(Charm charm, IEnumerable<CharmCombinationSlot> slots)
    {
        var armor = Canonicalize(charm.ArmorSlots);
        var weapon = Canonicalize(charm.WeaponSlots);

        foreach (var slot in slots)
        {
            if (Dominates(Canonicalize(slot.Armor), Canonicalize(slot.Weapon), armor, weapon))
                return true;
        }

        return false;
    }

    private static int[] Canonicalize(IReadOnlyList<int> slots)
    {
        var sorted = slots.OrderByDescending(v => v).ToArray();
        var result = new int[3];
        for (int i = 0; i < sorted.Length && i < 3; i++)
            result[i] = sorted[i];
        return result;
    }

    private static bool Dominates(int[] aArmor, int[] aWeapon, int[] bArmor, int[] bWeapon)
    {
        bool greaterOrEqualInAll = true;
        bool strictlyGreaterInSome = false;

        foreach (var (a, b) in aArmor.Zip(bArmor).Concat(aWeapon.Zip(bWeapon)))
        {
            if (a < b) greaterOrEqualInAll = false;
            if (a > b) strictlyGreaterInSome = true;
        }

        return greaterOrEqualInAll && strictlyGreaterInSome;
    }
}
