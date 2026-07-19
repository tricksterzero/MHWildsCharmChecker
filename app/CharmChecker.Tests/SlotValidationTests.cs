using CharmChecker.Core.SlotIcon;
using Xunit;

namespace CharmChecker.Tests;

public class SlotValidationTests
{
    [Fact]
    public void ValidArmorSlots_Unchanged()
    {
        var (armor, weapon) = SlotValidation.Validate([3, 1, 0], [0, 0, 0]);
        Assert.Equal([3, 1, 0], armor);
        Assert.Equal([0, 0, 0], weapon);
    }

    [Fact]
    public void ArmorSlot2_Lv3_Rejected()
    {
        // 防3-防3 → 2個目の防3は制約違反（max Lv1）→ 防3のみ
        var (armor, _) = SlotValidation.Validate([3, 3, 0], [0, 0, 0]);
        Assert.Equal([3, 0, 0], armor);
    }

    [Fact]
    public void ArmorSlot2_Lv2_Rejected()
    {
        // 防3-防2 → 2個目の防2は制約違反（max Lv1）→ 防3のみ
        var (armor, _) = SlotValidation.Validate([3, 2, 0], [0, 0, 0]);
        Assert.Equal([3, 0, 0], armor);
    }

    [Fact]
    public void ArmorSlot2_Lv1_Kept()
    {
        var (armor, _) = SlotValidation.Validate([2, 1, 0], [0, 0, 0]);
        Assert.Equal([2, 1, 0], armor);
    }

    [Fact]
    public void UnsortedInput_SortedAndValidated()
    {
        // 入力が未ソートでもソートしてからバリデーション
        var (armor, _) = SlotValidation.Validate([1, 3, 0], [0, 0, 0]);
        Assert.Equal([3, 1, 0], armor);
    }

    [Fact]
    public void WeaponSlot_Lv1_Valid()
    {
        var (_, weapon) = SlotValidation.Validate([0, 0, 0], [1, 0, 0]);
        Assert.Equal([1, 0, 0], weapon);
    }

    [Fact]
    public void WeaponSlot_Lv2_Rejected()
    {
        var (_, weapon) = SlotValidation.Validate([0, 0, 0], [2, 0, 0]);
        Assert.Equal([0, 0, 0], weapon);
    }

    [Fact]
    public void AllEmpty_Unchanged()
    {
        var (armor, weapon) = SlotValidation.Validate([0, 0, 0], [0, 0, 0]);
        Assert.Equal([0, 0, 0], armor);
        Assert.Equal([0, 0, 0], weapon);
    }

    [Fact]
    public void Rare8_WeaponAndArmor()
    {
        var (armor, weapon) = SlotValidation.Validate([1, 1, 0], [1, 0, 0]);
        Assert.Equal([1, 1, 0], armor);
        Assert.Equal([1, 0, 0], weapon);
    }

    [Fact]
    public void ArmorSlot1_Lv3Lv2Lv1_RejectsLv2_KeepsFollowingLv1()
    {
        // 防3-防2-防1 → 2個目の防2は制約違反（max Lv1）で棄却、後続の防1を採用
        var (armor, _) = SlotValidation.Validate([3, 2, 1], [0, 0, 0]);
        Assert.Equal([3, 1, 0], armor);
    }

    [Fact]
    public void WeaponSlot_MultipleLv1_OnlyFirstKept()
    {
        // 武器スロットは現行ゲームで実質1個までしか存在しないため、2個目以降は棄却
        var (_, weapon) = SlotValidation.Validate([0, 0, 0], [1, 1, 0]);
        Assert.Equal([1, 0, 0], weapon);
    }
}
