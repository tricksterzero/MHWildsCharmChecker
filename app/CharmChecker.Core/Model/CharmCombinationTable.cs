using System.Text.Json;

namespace CharmChecker.Core.Model;

public record SkillGroupEntry(string Name, int Level, int[] Groups);

public record CharmCombinationSlot(int[] Armor, int[] Weapon, double? Weight = null);

public record CharmCombination(int Rarity, int[] SkillGroups, CharmCombinationSlot[] Slots);

/// <summary>
/// 護石の構成表(skill-groups.jsonのスキルのグループ分けと、charm-combinations.jsonの組み合わせ)の読み込み・検査と、
/// 護石との照合。RARE推定(<see cref="RarityInference"/>)・理論値判定・構成確率・構成表との照合が共通に使う。
/// 2026-11-12にRarityInferenceから移した(処理の内容は変えていない)。
/// </summary>
public static class CharmCombinationTable
{
    /// <summary>研鑽は希望の護石(予約特典)固有のスキルで、構成表(charm-combinations.json)の対象外。</summary>
    internal const string KensanSkillName = "研鑽";

    private static readonly Lazy<IReadOnlyList<SkillGroupEntry>> _skillGroups =
        new(() => ParseSkillGroups(EmbeddedResources.ReadText("skill-groups.json")));

    private static readonly Lazy<IReadOnlyList<CharmCombination>> _combinations =
        new(() => ParseCombinations(EmbeddedResources.ReadText("charm-combinations.json")));

    public static IReadOnlyList<SkillGroupEntry> LoadSkillGroups() => _skillGroups.Value;

    public static IReadOnlyList<CharmCombination> LoadCombinations() => _combinations.Value;

    /// <summary>
    /// 護石のスキル(名前とLvから引けるグループ)が当てはまる組み合わせを、<paramref name="combinations"/>の順のまま返す
    /// (スロットは見ない。副作用なし)。スキルの順序は問わない(<see cref="MatchesGroupPattern"/>)。
    /// skill-groupsに無いスキル・Lvを含む場合は空。
    /// 事前条件: 引数はnullでない(満たさなければ、戻り値を列挙する前の呼び出し時点で例外を投げる)。
    /// グループの解決は呼び出し時に1度だけ行い、組み合わせとの照合は戻り値を列挙するときに行う。
    /// </summary>
    internal static IEnumerable<CharmCombination> MatchingCombinations(
        Charm charm, IReadOnlyList<SkillGroupEntry> skillGroups, IEnumerable<CharmCombination> combinations)
    {
        ArgumentNullException.ThrowIfNull(charm);
        ArgumentNullException.ThrowIfNull(skillGroups);
        ArgumentNullException.ThrowIfNull(combinations);

        var possibleGroupsPerSkill = ResolvePossibleGroups(charm, skillGroups);
        if (possibleGroupsPerSkill is null)
            return [];
        return combinations.Where(c => MatchesGroupPattern(possibleGroupsPerSkill, c.SkillGroups));
    }

    private static List<int[]>? ResolvePossibleGroups(Charm charm, IReadOnlyList<SkillGroupEntry> skillGroups)
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

    /// <summary>
    /// 護石のスキル入力順序と組み合わせパターンの宣言順序は一致するとは限らない
    /// （charm-combinations.jsonのskillGroups配列自体、グループ番号昇順にすらなっていない
    /// エントリが29件中14件あり、順序に意味があるとは言えないため）。
    /// 各パターン位置に、まだ割り当てていないスキルを1つずつ対応付けられるかを
    /// バックトラッキングで探索する（一対一割り当て、同じスキルを2位置に使い回さない）。
    /// </summary>
    internal static bool MatchesGroupPattern(List<int[]> possibleGroups, int[] pattern)
    {
        if (possibleGroups.Count != pattern.Length) return false;
        return TryAssign(possibleGroups, pattern, new bool[possibleGroups.Count], 0);
    }

    private static bool TryAssign(List<int[]> possibleGroups, int[] pattern, bool[] used, int patternIndex)
    {
        if (patternIndex == pattern.Length) return true;

        for (int skillIndex = 0; skillIndex < possibleGroups.Count; skillIndex++)
        {
            if (used[skillIndex] || !possibleGroups[skillIndex].Contains(pattern[patternIndex]))
                continue;

            used[skillIndex] = true;
            if (TryAssign(possibleGroups, pattern, used, patternIndex + 1))
                return true;
            used[skillIndex] = false;
        }

        return false;
    }

    /// <summary>
    /// スロットパターンごとに配列長が異なる（例: RARE8は[1]と[1,1]が混在）ため、
    /// パターンが宣言する個数だけ上位を切り出して比較し、残りが全て0であることも確認する。
    /// </summary>
    internal static CharmCombinationSlot? FindMatchingSlot(IEnumerable<CharmCombinationSlot> slots, Charm charm)
    {
        var armorSorted = charm.ArmorSlotsDescending;
        var weaponSorted = charm.WeaponSlotsDescending;

        return slots.FirstOrDefault(s =>
            armorSorted.Take(s.Armor.Length).SequenceEqual(s.Armor) &&
            armorSorted.Skip(s.Armor.Length).All(v => v == 0) &&
            weaponSorted.Take(s.Weapon.Length).SequenceEqual(s.Weapon) &&
            weaponSorted.Skip(s.Weapon.Length).All(v => v == 0));
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

    /// <summary>
    /// charm-combinations.jsonを読み込み、手で保守するデータの誤りを読み込み時に検出する
    /// (skill-groups.json・charm-types.jsonと同じく、誤りがあれば例外にする。2026-11-08追加)。
    /// 検査: RAREは5〜8、スキルグループは1〜3個、スロットのパターンは1個以上で各Lvは0〜3・各3個まで、
    /// 重み(weight)は1つの組み合わせの中で全部あるか全部ないかのどちらかで、ある場合は各値が正で
    /// 合計が1(誤差0.01まで)。一部にだけ重みがあると、構成確率の計算で重みと「1/パターン数」が混ざるため。
    /// </summary>
    internal static CharmCombination[] ParseCombinations(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var combinations = doc.RootElement.EnumerateArray().Select(e => new CharmCombination(
            e.GetProperty("rarity").GetInt32(),
            e.GetProperty("skillGroups").EnumerateArray().Select(g => g.GetInt32()).ToArray(),
            e.GetProperty("slots").EnumerateArray().Select(s => new CharmCombinationSlot(
                s.GetProperty("armor").EnumerateArray().Select(a => a.GetInt32()).ToArray(),
                s.GetProperty("weapon").EnumerateArray().Select(w => w.GetInt32()).ToArray(),
                s.TryGetProperty("weight", out var w) ? w.GetDouble() : null
            )).ToArray()
        )).ToArray();

        foreach (var c in combinations)
        {
            var label = $"RARE{c.Rarity} [{string.Join(",", c.SkillGroups)}]";
            if (c.Rarity is < Charm.MinRarity or > Charm.MaxRarity)
                throw new InvalidOperationException($"charm-combinations.json のRAREが{Charm.MinRarity}〜{Charm.MaxRarity}の範囲外です: {label}");
            if (c.SkillGroups.Length is < 1 or > Charm.MaxSkills)
                throw new InvalidOperationException($"charm-combinations.json のスキルグループの数が1〜{Charm.MaxSkills}ではありません: {label}");
            if (c.Slots.Length == 0)
                throw new InvalidOperationException($"charm-combinations.json にスロットのパターンがありません: {label}");
            if (c.Slots.Any(s => s.Armor.Length > Charm.SlotCount || s.Weapon.Length > Charm.SlotCount
                    || s.Armor.Concat(s.Weapon).Any(v => v is < 0 or > Charm.MaxSlotLevel)))
                throw new InvalidOperationException($"charm-combinations.json のスロットの値が不正です(各{Charm.SlotCount}個まで、Lvは0〜{Charm.MaxSlotLevel}): {label}");

            int weighted = c.Slots.Count(s => s.Weight is not null);
            if (weighted != 0 && weighted != c.Slots.Length)
                throw new InvalidOperationException($"charm-combinations.json で、重み(weight)が一部のスロットにだけあります: {label}");
            if (weighted > 0 && (c.Slots.Any(s => s.Weight <= 0) || Math.Abs(c.Slots.Sum(s => s.Weight!.Value) - 1.0) > 0.01))
                throw new InvalidOperationException($"charm-combinations.json の重み(weight)が正でないか、合計が1ではありません: {label}");
        }

        return combinations;
    }
}
