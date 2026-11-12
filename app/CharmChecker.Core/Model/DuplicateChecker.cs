namespace CharmChecker.Core.Model;

public record DuplicateGroup(List<int> Indices);

public record InferiorEntry(int TargetIndex, List<int> SuperiorIndices);

public class DuplicateCheckResult
{
    public required List<DuplicateGroup> DuplicateGroups { get; init; }
    public required List<InferiorEntry> Inferiors { get; init; }
}

public static class DuplicateChecker
{
    public static DuplicateCheckResult Check(IReadOnlyList<Charm> charms)
    {
        var duplicateGroups = FindDuplicateGroups(charms);

        var duplicateIndices = new HashSet<int>(
            duplicateGroups.SelectMany(g => g.Indices));

        var inferiors = FindInferiors(charms, duplicateIndices);

        return new DuplicateCheckResult
        {
            DuplicateGroups = duplicateGroups,
            Inferiors = inferiors,
        };
    }

    private static List<DuplicateGroup> FindDuplicateGroups(IReadOnlyList<Charm> charms)
    {
        var groups = new Dictionary<string, List<int>>();

        for (int i = 0; i < charms.Count; i++)
        {
            var key = IdentityKey(charms[i]);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(i);
        }

        return groups.Values
            .Where(g => g.Count > 1)
            .Select(g => new DuplicateGroup(g))
            .ToList();
    }

    private static List<InferiorEntry> FindInferiors(
        IReadOnlyList<Charm> charms, HashSet<int> excludeIndices)
    {
        var inferiors = new List<InferiorEntry>();

        for (int i = 0; i < charms.Count; i++)
        {
            if (excludeIndices.Contains(i)) continue;

            var superiors = new List<int>();
            for (int j = 0; j < charms.Count; j++)
            {
                if (i == j || excludeIndices.Contains(j)) continue;
                if (IsStrictlySuperior(charms[j], charms[i]))
                    superiors.Add(j);
            }

            if (superiors.Count > 0)
                inferiors.Add(new InferiorEntry(i, superiors));
        }

        return inferiors;
    }

    /// <summary>
    /// 2つの護石が完全同一(スキルの名前とLvの組がすべて同じで、防具・武器スロットを降順に並べたものが同じ)かを返す
    /// (副作用なし)。重複チェックと、取り込み時の重複の読み飛ばしの両方がこの判定を使う。
    /// 事前条件: 引数はnullでない。
    /// </summary>
    public static bool AreIdentical(Charm a, Charm b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return IdentityKey(a) == IdentityKey(b);
    }

    /// <summary>
    /// 取り込む護石のうち、既存の護石とも、取り込むデータの中で先に出てきた護石とも同一(<see cref="AreIdentical"/>と同じ基準)で
    /// ないものを元の順のまま返し、読み飛ばした件数も返す(副作用なし)。取り込みの差分追加に使う。
    /// 事前条件: 引数はnullでない。事後条件: 返す護石の数と読み飛ばした件数の和は、incomingの件数と同じ。
    /// </summary>
    public static (IReadOnlyList<Charm> ToAdd, int Skipped) ExcludeIdentical(IEnumerable<Charm> existing, IEnumerable<Charm> incoming)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);

        var seenKeys = existing.Select(IdentityKey).ToHashSet();
        var toAdd = new List<Charm>();
        int skipped = 0;
        foreach (var charm in incoming)
        {
            if (seenKeys.Add(IdentityKey(charm)))
                toAdd.Add(charm);
            else
                skipped++;
        }
        return (toAdd, skipped);
    }

    private static string IdentityKey(Charm charm)
    {
        var skillPart = string.Join("|",
            charm.Skills
                .Select(s => $"{s.Name}:{s.Lv}")
                .OrderBy(s => s));

        var armorPart = string.Join(",", charm.ArmorSlotsDescending);
        var weaponPart = string.Join(",", charm.WeaponSlotsDescending);

        return $"{skillPart}#{armorPart}#{weaponPart}";
    }

    private static bool IsStrictlySuperior(Charm a, Charm b)
    {
        var skillCmp = CompareSkillLevels(a, b);
        if (skillCmp is Ordering.Lt or Ordering.Incomparable) return false;

        var slotCmp = CompareSlots(a, b);
        if (slotCmp is Ordering.Lt or Ordering.Incomparable) return false;

        if (skillCmp == Ordering.Eq && slotCmp == Ordering.Eq) return false;

        return true;
    }

    private static Ordering CompareSkillLevels(Charm a, Charm b)
    {
        // スキル名の重複はCharmの不変条件で起きないため、ToDictionaryは例外を投げない
        var mapA = a.Skills.ToDictionary(s => s.Name, s => s.Lv);
        var mapB = b.Skills.ToDictionary(s => s.Name, s => s.Lv);

        if (mapA.Count != mapB.Count) return Ordering.Incomparable;
        foreach (var name in mapA.Keys)
            if (!mapB.ContainsKey(name)) return Ordering.Incomparable;

        bool hasGt = false, hasLt = false;
        foreach (var (name, lvA) in mapA)
        {
            var lvB = mapB[name];
            if (lvA > lvB) hasGt = true;
            if (lvA < lvB) hasLt = true;
        }

        if (hasGt && !hasLt) return Ordering.Gt;
        if (hasLt && !hasGt) return Ordering.Lt;
        if (!hasGt && !hasLt) return Ordering.Eq;
        return Ordering.Incomparable;
    }

    private static Ordering CompareSlots(Charm a, Charm b)
    {
        bool aGteB = SlotsGte(a.ArmorSlotsDescending, b.ArmorSlotsDescending)
                     && SlotsGte(a.WeaponSlotsDescending, b.WeaponSlotsDescending);
        bool bGteA = SlotsGte(b.ArmorSlotsDescending, a.ArmorSlotsDescending)
                     && SlotsGte(b.WeaponSlotsDescending, a.WeaponSlotsDescending);

        if (aGteB && bGteA) return Ordering.Eq;
        if (aGteB) return Ordering.Gt;
        if (bGteA) return Ordering.Lt;
        return Ordering.Incomparable;
    }

    /// <summary>降順に並べたスロット(どちらもCharm.SlotCount個)を位置ごとに比べ、すべての位置でa以上ならtrue。</summary>
    private static bool SlotsGte(IReadOnlyList<int> aDescending, IReadOnlyList<int> bDescending) =>
        aDescending.Zip(bDescending).All(pair => pair.First >= pair.Second);

    private enum Ordering { Eq, Gt, Lt, Incomparable }
}
