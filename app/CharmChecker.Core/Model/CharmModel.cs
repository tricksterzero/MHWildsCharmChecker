using System.Collections.ObjectModel;

namespace CharmChecker.Core.Model;

public enum CharmSource
{
    Screenshot,
    CsvImport,
    Manual,
}

public enum GameVersion
{
    Wilds,
    Ascendance,
}

public readonly record struct CharmSkill(string Name, int Lv);

/// <summary>
/// 護石1個。作った後は変更できない(値を変えるときは<see cref="WithRarity"/>などで新しいインスタンスを作る)。
/// 不変条件(どの入口で作っても必ず満たす。各プロパティのinitで検査し、満たさなければArgumentException):
/// - Skills: 3個まで。各スキルの名前は空白でなく、Lvは1以上、名前は重複しない(序数比較)
/// - ArmorSlots・WeaponSlots: それぞれ3個で、値は0〜3。3個未満は0で埋め、3個を超える分は0なら切り詰める
/// - Rarity: nullか5〜8
/// ゲームとしてありうるか(スキル名・Lvの実在、スロットの形など)は<see cref="CharmValidator"/>で別に確かめる。
/// (2026-11-08。それまでは検査がなく、値も後から書き換えられたため、入口ごとに前提がばらばらだった)
/// </summary>
public sealed class Charm
{
    public const int MaxSkills = 3;
    public const int SlotCount = 3;
    public const int MaxSlotLevel = 3;
    public const int MinRarity = 5;
    public const int MaxRarity = 8;

    private static readonly ReadOnlyCollection<int> EmptySlots = Array.AsReadOnly(new int[SlotCount]);

    private readonly ReadOnlyCollection<CharmSkill> _skills = Array.AsReadOnly(Array.Empty<CharmSkill>());
    private readonly ReadOnlyCollection<int> _armorSlots = EmptySlots;
    private readonly ReadOnlyCollection<int> _weaponSlots = EmptySlots;
    private readonly int? _rarity;

    public IReadOnlyList<CharmSkill> Skills
    {
        get => _skills;
        init => _skills = ValidateSkills(value);
    }

    public IReadOnlyList<int> ArmorSlots
    {
        get => _armorSlots;
        init => _armorSlots = NormalizeSlots(value, nameof(ArmorSlots));
    }

    public IReadOnlyList<int> WeaponSlots
    {
        get => _weaponSlots;
        init => _weaponSlots = NormalizeSlots(value, nameof(WeaponSlots));
    }

    public int? Rarity
    {
        get => _rarity;
        init => _rarity = ValidateRarity(value);
    }

    public CharmSource Source { get; init; }
    public DateTime SourceTimestamp { get; init; }
    public GameVersion Version { get; init; }

    /// <summary>RAREだけを変えた新しいインスタンスを返す(副作用なし)。rarityはnullか5〜8。</summary>
    public Charm WithRarity(int? rarity) => new()
    {
        Skills = _skills,
        ArmorSlots = _armorSlots,
        WeaponSlots = _weaponSlots,
        Rarity = rarity,
        Source = Source,
        SourceTimestamp = SourceTimestamp,
        Version = Version,
    };

    /// <summary>RAREの値として不変条件を満たすか(nullか5〜8)。</summary>
    public static bool IsValidRarity(int? rarity) => rarity is null or (>= MinRarity and <= MaxRarity);

    private static ReadOnlyCollection<CharmSkill> ValidateSkills(IReadOnlyList<CharmSkill> value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(Skills));
        if (value.Count > MaxSkills)
            throw new ArgumentException($"スキルは{MaxSkills}個までです({value.Count}個)。");
        foreach (var skill in value)
        {
            if (string.IsNullOrWhiteSpace(skill.Name))
                throw new ArgumentException("スキル名が空です。");
            if (skill.Lv < 1)
                throw new ArgumentException($"スキル「{skill.Name}」のLvが1未満です(Lv{skill.Lv})。");
        }
        var duplicates = value.GroupBy(s => s.Name, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException($"同じスキルが複数あります: {string.Join(", ", duplicates)}");
        return Array.AsReadOnly(value.ToArray());
    }

    private static ReadOnlyCollection<int> NormalizeSlots(IReadOnlyList<int> value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        // 利用者に見せる理由の文に、.NETが付ける英語の"(Parameter '...')"が混ざらないよう、引数名は渡さない
        var label = paramName == nameof(WeaponSlots) ? "武器" : "防具";
        if (value.Any(v => v is < 0 or > MaxSlotLevel))
            throw new ArgumentException($"{label}スロットの値は0〜{MaxSlotLevel}です: [{string.Join(",", value)}]");
        if (value.Skip(SlotCount).Any(v => v != 0))
            throw new ArgumentException($"{label}スロットは{SlotCount}個までです: [{string.Join(",", value)}]");
        var normalized = new int[SlotCount];
        for (int i = 0; i < SlotCount && i < value.Count; i++)
            normalized[i] = value[i];
        return Array.AsReadOnly(normalized);
    }

    private static int? ValidateRarity(int? value)
    {
        if (!IsValidRarity(value))
            throw new ArgumentException($"RAREはnullか{MinRarity}〜{MaxRarity}です(RARE{value})。");
        return value;
    }
}
