using CharmChecker.Core.SlotIcon;

namespace CharmChecker.Tests;

public class SlotValidationTests
{
    private static (List<int> Armor, List<int> Weapon) Valid(int[] armor, int[] weapon)
    {
        var result = SlotValidation.TryValidate(armor, weapon);
        Assert.NotNull(result);
        return result.Value;
    }

    [Fact]
    public void ValidArmorSlots_Unchanged()
    {
        var (armor, weapon) = Valid([3, 1, 0], [0, 0, 0]);
        Assert.Equal([3, 1, 0], armor);
        Assert.Equal([0, 0, 0], weapon);
    }

    [Theory]
    [InlineData(3)] // 防3-防3: 2個目は最大Lv1
    [InlineData(2)] // 境界値: 防3-防2
    public void ArmorSlot2_AboveLv1_IsInvalid(int secondLv)
    {
        Assert.Null(SlotValidation.TryValidate([3, secondLv, 0], [0, 0, 0]));
    }

    [Fact]
    public void ArmorSlot2_Lv1_Kept()
    {
        var (armor, _) = Valid([2, 1, 0], [0, 0, 0]);
        Assert.Equal([2, 1, 0], armor);
    }

    [Fact]
    public void UnsortedInput_SortedAndValidated()
    {
        // 入力が未ソートでもソートしてから確かめる
        var (armor, _) = Valid([1, 3, 0], [0, 0, 0]);
        Assert.Equal([3, 1, 0], armor);
    }

    [Fact]
    public void ArmorSlot1_Lv3_IsValid_Lv4_IsInvalid()
    {
        // 境界値: 防具の1個目は最大Lv3
        Assert.NotNull(SlotValidation.TryValidate([3, 0, 0], [0, 0, 0]));
        Assert.Null(SlotValidation.TryValidate([4, 0, 0], [0, 0, 0]));
    }

    [Fact]
    public void ThreeArmorSlots_IsInvalid()
    {
        // 防具の3個目は常に0(防3-防1-防1は現行ゲームに存在しない)
        Assert.Null(SlotValidation.TryValidate([3, 1, 1], [0, 0, 0]));
    }

    [Fact]
    public void WeaponSlot_Lv1_Valid()
    {
        var (_, weapon) = Valid([0, 0, 0], [1, 0, 0]);
        Assert.Equal([1, 0, 0], weapon);
    }

    [Fact]
    public void WeaponSlot_Lv2_IsInvalid()
    {
        Assert.Null(SlotValidation.TryValidate([0, 0, 0], [2, 0, 0]));
    }

    [Fact]
    public void WeaponSlot_TwoLv1_IsInvalid()
    {
        // 武器スロットは現行ゲームで1個まで
        Assert.Null(SlotValidation.TryValidate([0, 0, 0], [1, 1, 0]));
    }

    [Fact]
    public void AllEmpty_Unchanged()
    {
        var (armor, weapon) = Valid([0, 0, 0], [0, 0, 0]);
        Assert.Equal([0, 0, 0], armor);
        Assert.Equal([0, 0, 0], weapon);
    }

    [Fact]
    public void Rare8_WeaponAndArmor()
    {
        var (armor, weapon) = Valid([1, 1, 0], [1, 0, 0]);
        Assert.Equal([1, 1, 0], armor);
        Assert.Equal([1, 0, 0], weapon);
    }

    [Fact]
    public void ShortOrLongLists_PaddedOrValidated()
    {
        // 長さは問わず、結果は3個にそろえる。4個目以降に0以外があれば無効
        var (armor, weapon) = Valid([2], []);
        Assert.Equal([2, 0, 0], armor);
        Assert.Equal([0, 0, 0], weapon);
        Assert.Null(SlotValidation.TryValidate([2, 1, 0, 1], [0, 0, 0]));
    }

    [Fact]
    public void NegativeValue_IsInvalid()
    {
        Assert.Null(SlotValidation.TryValidate([-1, 0, 0], [0, 0, 0]));
        Assert.Null(SlotValidation.TryValidate([0, 0, 0], [-1, 0, 0]));
    }

    [Fact]
    public void NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SlotValidation.TryValidate(null!, [0, 0, 0]));
        Assert.Throws<ArgumentNullException>(() => SlotValidation.TryValidate([0, 0, 0], null!));
    }
}
