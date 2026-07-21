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
    public void SkillWithNoHigherLevelAnywhere_StillCountsAsMax()
    {
        // 超会心はLv1にしか存在しないため、Lv1でも「自身の最大」を満たす
        // （所属グループの集合最大Lvが2であっても、超会心自身は対象外）
        var groups = RarityInference.LoadSkillGroups();
        var chokaishin = groups.Where(g => g.Name == "超会心").ToList();
        Assert.Single(chokaishin);
        Assert.Equal(1, chokaishin[0].Level);
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
