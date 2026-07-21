using CharmChecker.Core.Model;
using Xunit;

namespace CharmChecker.Tests;

public class CharmTheoreticalValueCheckerTests
{
    [Fact]
    public void MaxSkillsAndFrontierSlot_ReturnsTrue()
    {
        // ＫＯ術Lv3・ひるみ軽減Lv3はいずれも自身の最大Lv（groupを問わず）、
        // スロット[1,1]+武器1はレアリティ横断でどのパターンにも支配されない最大構成
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 3), new("ひるみ軽減", 3)],
            ArmorSlots = [1, 1, 0],
            WeaponSlots = [1, 0, 0],
            Rarity = 8,
        };

        Assert.True(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void SkillBelowOwnMax_ReturnsFalse()
    {
        // ＫＯ術はLv3まで存在するため、Lv1では自身の最大に達していない
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 1), new("ひるみ軽減", 3)],
            ArmorSlots = [1, 1, 0],
            WeaponSlots = [1, 0, 0],
            Rarity = 8,
        };

        Assert.False(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void SlotDominatedByAnotherPattern_ReturnsFalse()
    {
        // スキルは自身の最大Lvだが、スロットが武器1のみ（防具1つ分少ない）で
        // [1,1]+武器1に支配される
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 3), new("ひるみ軽減", 3)],
            ArmorSlots = [0, 0, 0],
            WeaponSlots = [1, 0, 0],
            Rarity = 8,
        };

        Assert.False(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void Rare7Slot11_IsDominatedByRare8SameCombo_ReturnsFalse()
    {
        // RARE7のスロット[1,1]（武器なし）は、同じスキルグループ構成[3,7]を共有する
        // RARE8の[1,1]+武器1（防具は同じで武器スロットが純増）に支配される
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 3), new("ひるみ軽減", 3)],
            ArmorSlots = [1, 1, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = 7,
        };

        Assert.False(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void Rare7Slot20_NotDominatedByRare8_ReturnsTrue()
    {
        // RARE7のスロット[2,0]（防具Lv2×1）は、同じ組み合わせ[3,7]のRARE8側
        // （[W1,0]/[W1,1]/[W1,1,1]、いずれも防具は最大Lv1×2まで）のどれにも
        // 防具・武器の両面で上回られないため、理論値になりうる
        // （防具2スロット vs 武器スロットは種類が異なり単純比較できないため、
        // RARE5・6の[2,1]（無関係な組み合わせ）とは比較しない）
        var charm = new Charm
        {
            Skills = [new("貫通弾・竜の矢強化", 1), new("ひるみ軽減", 3)],
            ArmorSlots = [2, 0, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = 7,
        };

        Assert.True(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void SkillWithNoHigherLevelAnywhere_DoesNotIncorrectlyFailMaxCheck()
    {
        // 超会心はLv1にしか存在しないが、所属するgroup4には会心撃【属性】Lv2が混在する。
        // 「所属グループの最大Lv」基準で判定すると、超会心Lv1が自身の最大に
        // 達していないと誤判定されfalseを返してしまう回帰を防ぐ。
        // 自身の最大Lv基準では単独スキルは組み合わせテーブルに一致せずnullを返すべきで、
        // falseにはならない
        var charm = new Charm
        {
            Skills = [new("超会心", 1)],
            ArmorSlots = [0, 0, 0],
            WeaponSlots = [0, 0, 0],
        };

        Assert.Null(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void ImpossibleSlotConfiguration_ReturnsNull()
    {
        // スキルは自身の最大Lvで組み合わせにも一致するが、防具スロットLv3は
        // このスキルグループ構成[3,7]のどの組み合わせパターンにも実在しない値。
        // 「支配されなければtrue」という判定だけだと、テーブルに存在しない
        // 極端な値ほど何にも支配されず誤ってtrueになってしまうため、
        // 実在するパターンとの完全一致を先に要求してnullを返す
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 3), new("ひるみ軽減", 3)],
            ArmorSlots = [3, 0, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = 7,
        };

        Assert.Null(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void NoSkills_ReturnsNull()
    {
        var charm = new Charm
        {
            Skills = [],
            ArmorSlots = [1, 0, 0],
            WeaponSlots = [0, 0, 0],
        };

        Assert.Null(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }

    [Fact]
    public void UnknownSkill_ReturnsNull()
    {
        var charm = new Charm
        {
            Skills = [new("存在しないスキル", 1)],
            ArmorSlots = [1, 0, 0],
            WeaponSlots = [0, 0, 0],
        };

        Assert.Null(CharmTheoreticalValueChecker.IsTheoretical(charm));
    }
}
