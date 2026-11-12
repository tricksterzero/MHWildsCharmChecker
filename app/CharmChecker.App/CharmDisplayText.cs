using CharmChecker.Core.Model;

namespace CharmChecker.App;

/// <summary>
/// 画面に出す護石の表示文字列(護石一覧・読み取り結果の一覧・詳細パネル)を作る。どれも副作用なし。
/// 事前条件: 引数はnullでない(満たさなければArgumentNullException)。
/// 武器スロットは保存された順のまま、防具スロットは大きい順に並べて表示する。
/// </summary>
internal static class CharmDisplayText
{
    /// <summary>RAREの数字。不明なら「-」。</summary>
    public static string Rarity(Charm charm)
    {
        ArgumentNullException.ThrowIfNull(charm);
        return charm.Rarity.HasValue ? charm.Rarity.Value.ToString() : "-";
    }

    /// <summary>「名前 LvN」を<paramref name="separator"/>でつないだもの。スキルが0個なら「（なし）」。</summary>
    public static string Skills(Charm charm, string separator)
    {
        ArgumentNullException.ThrowIfNull(charm);
        ArgumentNullException.ThrowIfNull(separator);
        return charm.Skills.Count > 0
            ? string.Join(separator, charm.Skills.Select(s => $"{s.Name} Lv{s.Lv}"))
            : "（なし）";
    }

    /// <summary>一覧用の1行の表示(例:「武1 - 防2 - 防1」)。スロットがなければ「なし」。</summary>
    public static string Slots(Charm charm)
    {
        ArgumentNullException.ThrowIfNull(charm);
        var parts = WeaponLevels(charm).Select(lv => $"武{lv}")
            .Concat(ArmorLevels(charm).Select(lv => $"防{lv}"))
            .ToList();
        return parts.Count > 0 ? string.Join(" - ", parts) : "なし";
    }

    /// <summary>詳細パネル用の表示(例:「武器: Lv1」「防具: Lv2, Lv1」の2行)。武器は1つ目だけ。スロットがなければ「なし」。</summary>
    public static string SlotsDetail(Charm charm)
    {
        ArgumentNullException.ThrowIfNull(charm);
        var weaponLvs = WeaponLevels(charm);
        var armorLvs = ArmorLevels(charm);
        var lines = new List<string>();
        if (weaponLvs.Count > 0)
            lines.Add($"武器: Lv{weaponLvs[0]}");
        if (armorLvs.Count > 0)
            lines.Add($"防具: {string.Join(", ", armorLvs.Select(v => $"Lv{v}"))}");
        return lines.Count > 0 ? string.Join("\n", lines) : "なし";
    }

    private static List<int> WeaponLevels(Charm charm) => charm.WeaponSlots.Where(v => v > 0).ToList();

    private static List<int> ArmorLevels(Charm charm) => charm.ArmorSlotsDescending.Where(v => v > 0).ToList();
}
