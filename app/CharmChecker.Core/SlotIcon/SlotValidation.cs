using CharmChecker.Core.Model;

namespace CharmChecker.Core.SlotIcon;

public static class SlotValidation
{
    // 防具スロット: 降順ソート後、2個目はLv0-1、3個目以降は常に0
    private static readonly int[] ArmorSlotMaxByPosition = [3, 1, 0];

    // 武器スロット: Lv0-1のみ（現行ゲームでLv2/3は存在しない）
    private const int WeaponSlotMax = 1;

    // 武器スロット: 現行ゲームでは実質1個までしか存在しない
    private const int WeaponSlotCountMax = 1;

    /// <summary>
    /// スロットLvが現行ゲームにありうる形かを確かめ、ありうれば防具は降順に並べて、防具・武器それぞれ
    /// 3個のリストにして返す(副作用なし)。ありえない値(防具の2個目がLv2以上、防具が3個以上、武器がLv2以上や
    /// 2個以上、負の値)が1つでもあればnullを返す。
    /// 呼び出し元は2つ: スクショの読み取り(ScreenshotCharmReader。nullなら読み取りの誤りとみなし護石ごと除外する)と、
    /// 手動入力・CSV・JSONの検査(<see cref="CharmValidator"/>。nullならError)。
    /// (2026-11-08。それまでスクショではありえない値だけを捨てて残りで保存しており、例えば[2,1]を[2,2]と
    /// 読み違えると[2,0]として保存されていた)
    /// 事前条件: 引数はnullでない(長さは問わない)。
    /// </summary>
    public static (List<int> ArmorSlots, List<int> WeaponSlots)? TryValidate(
        IReadOnlyList<int> armorSlots, IReadOnlyList<int> weaponSlots)
    {
        ArgumentNullException.ThrowIfNull(armorSlots);
        ArgumentNullException.ThrowIfNull(weaponSlots);

        if (armorSlots.Any(v => v < 0) || weaponSlots.Any(v => v < 0))
            return null;

        var sortedArmor = armorSlots.Where(v => v > 0).OrderByDescending(v => v).ToList();
        for (int i = 0; i < sortedArmor.Count; i++)
        {
            int max = i < ArmorSlotMaxByPosition.Length ? ArmorSlotMaxByPosition[i] : 0;
            if (sortedArmor[i] > max)
                return null;
        }

        var weapon = weaponSlots.Where(v => v > 0).ToList();
        if (weapon.Count > WeaponSlotCountMax || weapon.Any(v => v > WeaponSlotMax))
            return null;

        return (PadToSlotCount(sortedArmor), PadToSlotCount(weapon));
    }

    private static List<int> PadToSlotCount(List<int> values)
    {
        var result = new List<int>(values);
        while (result.Count < Charm.SlotCount)
            result.Add(0);
        return result;
    }
}
