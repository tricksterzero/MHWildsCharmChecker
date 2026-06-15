namespace CharmChecker.Core.Model;

public enum CharmSource
{
    Screenshot,
    CsvImport,
    Manual,
}

public readonly record struct CharmSkill(string Name, int Lv);

public class Charm
{
    public List<CharmSkill> Skills { get; init; } = [];
    public List<int> ArmorSlots { get; init; } = [];
    public List<int> WeaponSlots { get; init; } = [];
    public CharmSource Source { get; init; }
    public DateTime SourceTimestamp { get; init; }
}
