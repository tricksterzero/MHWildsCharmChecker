namespace CharmChecker.Core.SlotIcon;

public static class SlotValidation
{
    // 防具スロット: 降順ソート後、2個目はLv0-1、3個目は常に0
    private static readonly int[] ArmorSlotMaxByPosition = [3, 1, 0];

    // 武器スロット: Lv0-1のみ（現行ゲームでLv2/3は存在しない）
    private const int WeaponSlotMax = 1;

    // 武器スロット: 現行ゲームでは実質1個までしか存在しない
    private const int WeaponSlotCountMax = 1;

    public static (List<int> ArmorSlots, List<int> WeaponSlots) Validate(
        List<int> armorSlots, List<int> weaponSlots)
    {
        var sortedArmor = armorSlots.OrderByDescending(v => v).ToList();
        var validArmor = new List<int> { 0, 0, 0 };
        int validIdx = 0;
        for (int i = 0; i < sortedArmor.Count && validIdx < 3; i++)
        {
            var lv = sortedArmor[i];
            if (lv <= 0) continue;
            if (lv <= ArmorSlotMaxByPosition[validIdx])
            {
                validArmor[validIdx] = lv;
                validIdx++;
            }
            // 制約違反のスロットは無視（ゴミ検出として破棄）
        }

        var validWeapon = new List<int> { 0, 0, 0 };
        int weaponIdx = 0;
        foreach (var lv in weaponSlots.Where(v => v > 0))
        {
            if (weaponIdx >= WeaponSlotCountMax) break;
            if (lv <= WeaponSlotMax)
            {
                validWeapon[weaponIdx] = lv;
                weaponIdx++;
            }
        }

        return (validArmor, validWeapon);
    }
}
