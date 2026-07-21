using System.Text.Json;

namespace CharmChecker.Core.Model;

public record SkillGroupEntry(string Name, int Level, int[] Groups);

public record CharmCombinationSlot(int[] Armor, int[] Weapon, double? Weight = null);

public record CharmCombination(int Rarity, int[] SkillGroups, CharmCombinationSlot[] Slots);

public static class RarityInference
{
    private static readonly Lazy<IReadOnlyList<SkillGroupEntry>> _skillGroups =
        new(() => LoadEmbeddedResource<SkillGroupEntry[]>("skill-groups.json", ParseSkillGroups));

    private static readonly Lazy<IReadOnlyList<CharmCombination>> _combinations =
        new(() => LoadEmbeddedResource<CharmCombination[]>("charm-combinations.json", ParseCombinations));

    public static IReadOnlyList<SkillGroupEntry> LoadSkillGroups() => _skillGroups.Value;

    public static IReadOnlyList<CharmCombination> LoadCombinations() => _combinations.Value;

    public static int? Infer(Charm charm)
    {
        var skillGroups = LoadSkillGroups();
        var combinations = LoadCombinations();
        return Infer(charm, skillGroups, combinations);
    }

    public static int? Infer(
        Charm charm,
        IReadOnlyList<SkillGroupEntry> skillGroups,
        IReadOnlyList<CharmCombination> combinations)
    {
        if (charm.WeaponSlots.Any(v => v > 0))
            return 8;

        if (charm.Skills.Any(s => s.Name == "研鑽"))
            return 5;

        if (charm.Skills.Count == 0)
            return null;

        var possibleGroupsPerSkill = ResolvePossibleGroups(charm, skillGroups);
        if (possibleGroupsPerSkill is null)
            return null;

        var matchedRarities = new HashSet<int>();
        foreach (var combo in combinations)
        {
            if (combo.SkillGroups.Length != charm.Skills.Count)
                continue;
            if (MatchesGroupPattern(possibleGroupsPerSkill, combo.SkillGroups))
                matchedRarities.Add(combo.Rarity);
        }

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

    internal static List<int[]>? ResolvePossibleGroups(Charm charm, IReadOnlyList<SkillGroupEntry> skillGroups)
    {
        var possibleGroupsPerSkill = new List<int[]>();
        foreach (var skill in charm.Skills)
        {
            var entry = skillGroups.FirstOrDefault(e =>
                e.Name == skill.Name && e.Level == skill.Lv);
            if (entry is null)
                return null;
            possibleGroupsPerSkill.Add(entry.Groups);
        }
        return possibleGroupsPerSkill;
    }

    internal static bool MatchesGroupPattern(List<int[]> possibleGroups, int[] pattern)
    {
        if (possibleGroups.Count != pattern.Length) return false;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (!possibleGroups[i].Contains(pattern[i]))
                return false;
        }
        return true;
    }

    private static T LoadEmbeddedResource<T>(string resourceName, Func<string, T> parser)
    {
        var asm = typeof(RarityInference).Assembly;
        using var stream = asm.GetManifestResourceStream($"CharmChecker.Core.Resources.{resourceName}")
            ?? throw new InvalidOperationException($"埋め込みリソース '{resourceName}' が見つかりません。");
        using var reader = new StreamReader(stream);
        return parser(reader.ReadToEnd());
    }

    internal static SkillGroupEntry[] ParseSkillGroups(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.EnumerateArray().Select(e => new SkillGroupEntry(
            e.GetProperty("name").GetString()!,
            e.GetProperty("level").GetInt32(),
            e.GetProperty("groups").EnumerateArray().Select(g => g.GetInt32()).ToArray()
        )).ToArray();

        var duplicates = entries
            .GroupBy(e => (e.Name, e.Level))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Name}(Lv{g.Key.Level})")
            .ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"skill-groups.json に (name, level) の重複があります: {string.Join(", ", duplicates)}");

        return entries;
    }

    private static CharmCombination[] ParseCombinations(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => new CharmCombination(
            e.GetProperty("rarity").GetInt32(),
            e.GetProperty("skillGroups").EnumerateArray().Select(g => g.GetInt32()).ToArray(),
            e.GetProperty("slots").EnumerateArray().Select(s => new CharmCombinationSlot(
                s.GetProperty("armor").EnumerateArray().Select(a => a.GetInt32()).ToArray(),
                s.GetProperty("weapon").EnumerateArray().Select(w => w.GetInt32()).ToArray(),
                s.TryGetProperty("weight", out var w) ? w.GetDouble() : null
            )).ToArray()
        )).ToArray();
    }
}
