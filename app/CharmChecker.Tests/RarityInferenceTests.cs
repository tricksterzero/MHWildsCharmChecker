using CharmChecker.Core.Model;
using Xunit;

namespace CharmChecker.Tests;

public class RarityInferenceTests
{
    [Fact]
    public void WeaponSlot_ReturnsRare8()
    {
        var charm = new Charm
        {
            Skills = [new("攻撃", 2), new("ＫＯ術", 1)],
            WeaponSlots = [1, 0, 0],
            ArmorSlots = [0, 0, 0],
        };
        Assert.Equal(8, RarityInference.Infer(charm));
    }

    [Fact]
    public void Rare5_Group1_Group6_Group6()
    {
        // Group 1 skill + Group 6 skills → RARE 5
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 1), new("アイテム使用強化", 2), new("ボマー", 2)],
            ArmorSlots = [3, 0, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(5, RarityInference.Infer(charm));
    }

    [Fact]
    public void Rare6_Group2_Group6()
    {
        // Group 2 + Group 6 → RARE 6
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 2), new("アイテム使用強化", 2), new("ボマー", 2)],
            ArmorSlots = [2, 1, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(6, RarityInference.Infer(charm));
    }

    [Fact]
    public void Rare6_Group1_Group1_Group7()
    {
        // Group 1 + Group 1 + Group 7 → RARE 6
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 1), new("攻撃", 1), new("アイテム使用強化", 3)],
            ArmorSlots = [1, 1, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(6, RarityInference.Infer(charm));
    }

    [Fact]
    public void Rare7_Group3_ArmorOnly()
    {
        // Group 3 skill → RARE 7 (armor slots only, not 8)
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 3), new("アイテム使用強化", 2), new("アイテム使用強化", 1)],
            ArmorSlots = [2, 0, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(7, RarityInference.Infer(charm));
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
        Assert.Null(RarityInference.Infer(charm));
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
        Assert.Null(RarityInference.Infer(charm));
    }

    [Fact]
    public void ResourcesLoadCorrectly()
    {
        var groups = RarityInference.LoadSkillGroups();
        Assert.True(groups.Count > 200);

        var combos = RarityInference.LoadCombinations();
        Assert.True(combos.Count > 20);
    }
}
