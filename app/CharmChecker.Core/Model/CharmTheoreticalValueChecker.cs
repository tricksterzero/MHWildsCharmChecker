namespace CharmChecker.Core.Model;

public static class CharmTheoreticalValueChecker
{
    public static bool? IsTheoretical(Charm charm) =>
        IsTheoretical(charm, CharmCombinationTable.LoadSkillGroups(), CharmCombinationTable.LoadCombinations());

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

        var matched = CharmCombinationTable.MatchingCombinations(charm, skillGroups, combinations).FirstOrDefault();
        if (matched is null)
            return null;

        // スロットの優劣は、同じスキルグループ構成を持つ組み合わせの間でのみ比較する
        // （例: RARE7とRARE8は同じスキルグループ構成を共有し、RARE8はRARE7に武器スロットを
        // 追加しただけの関係にあるため比較可能。一方、無関係な組み合わせ同士は
        // 防具スロットと武器スロットの価値を単純比較できないため対象に含めない）
        var comparableSlots = combinations
            .Where(c => c.SkillGroups.SequenceEqual(matched.SkillGroups))
            .SelectMany(c => c.Slots)
            .ToList();

        // 護石自身のスロット構成がテーブル上に実在するパターンかを先に確認する。
        // 実在しない構成（OCR誤読や手動入力の誤操作等）を、たまたま既存パターンに
        // 支配されないというだけで理論値と誤判定しないため
        if (CharmCombinationTable.FindMatchingSlot(comparableSlots, charm) is null)
            return null;

        return !IsSlotPatternDominated(charm, comparableSlots);
    }

    private static bool IsSlotPatternDominated(Charm charm, IEnumerable<CharmCombinationSlot> slots)
    {
        var armor = charm.ArmorSlotsDescending;
        var weapon = charm.WeaponSlotsDescending;

        foreach (var slot in slots)
        {
            if (Dominates(Canonicalize(slot.Armor), Canonicalize(slot.Weapon), armor, weapon))
                return true;
        }

        return false;
    }

    /// <summary>構成表のスロットのパターン(個数は3以下で可変)を、護石のスロットと同じ形(降順でCharm.SlotCount個)にする。</summary>
    private static int[] Canonicalize(IReadOnlyList<int> slots)
    {
        var sorted = slots.OrderByDescending(v => v).ToArray();
        var result = new int[Charm.SlotCount];
        for (int i = 0; i < sorted.Length && i < Charm.SlotCount; i++)
            result[i] = sorted[i];
        return result;
    }

    private static bool Dominates(
        IReadOnlyList<int> aArmor, IReadOnlyList<int> aWeapon, IReadOnlyList<int> bArmor, IReadOnlyList<int> bWeapon)
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
