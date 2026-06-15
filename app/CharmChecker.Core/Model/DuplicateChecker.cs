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

    private static string IdentityKey(Charm charm)
    {
        var skillPart = string.Join("|",
            charm.Skills
                .Select(s => $"{s.Name}:{s.Lv}")
                .OrderBy(s => s));

        var armorPart = string.Join(",", SortDesc(charm.ArmorSlots));
        var weaponPart = string.Join(",", SortDesc(charm.WeaponSlots));

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
        bool aGteB = SlotsGte(a.ArmorSlots, b.ArmorSlots)
                     && SlotsGte(a.WeaponSlots, b.WeaponSlots);
        bool bGteA = SlotsGte(b.ArmorSlots, a.ArmorSlots)
                     && SlotsGte(b.WeaponSlots, a.WeaponSlots);

        if (aGteB && bGteA) return Ordering.Eq;
        if (aGteB) return Ordering.Gt;
        if (bGteA) return Ordering.Lt;
        return Ordering.Incomparable;
    }

    private static bool SlotsGte(List<int> a, List<int> b)
    {
        var sa = SortDesc(a);
        var sb = SortDesc(b);
        int len = Math.Max(sa.Count, sb.Count);
        for (int i = 0; i < len; i++)
        {
            int va = i < sa.Count ? sa[i] : 0;
            int vb = i < sb.Count ? sb[i] : 0;
            if (va < vb) return false;
        }
        return true;
    }

    private static List<int> SortDesc(List<int> slots)
    {
        var sorted = new List<int>(slots);
        sorted.Sort((a, b) => b.CompareTo(a));
        return sorted;
    }

    private enum Ordering { Eq, Gt, Lt, Incomparable }
}
