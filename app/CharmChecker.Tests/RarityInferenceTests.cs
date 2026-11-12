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
        // スキルグループ[3,6,5]はRARE7・RARE8の両方にあるが、武器スロットがないのでRARE7
        // (2026-11-08、同じスキルを2つ持つ実在しない護石だったため、グループ5を別のスキルに替えた)
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 3), new("アイテム使用強化", 2), new("ボマー", 1)],
            ArmorSlots = [2, 0, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(7, RarityInference.Infer(charm));
    }

    [Fact]
    public void Rare5_SkillOrderReversed_StillMatches()
    {
        // charm-combinations.jsonのRARE5にskillGroups[1,7](ＫＯ術Lv1→ひるみ軽減Lv3の順)は
        // 実在するが、逆順[7,1]のエントリは存在しない。護石のスキル入力順は意味を持たない
        // （データ自体、グループ番号昇順にすらなっていないエントリが多数ある）ため、
        // 逆順で保存されても同じレアリティに一致すべき回帰テスト
        var charm = new Charm
        {
            Skills = [new("ひるみ軽減", 3), new("ＫＯ術", 1)],
            ArmorSlots = [2, 1, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(5, RarityInference.Infer(charm));
    }

    [Fact]
    public void Rare7_DuplicateGroupSkills_AreNotReusedForBothPositions()
    {
        // skillGroups[4,1,1](超会心+group1スキル2つ)は、group1に37種のスキルが属すため
        // 「攻撃」と「見切り」という別々のスキルをそれぞれ別の位置に一対一で割り当てる必要がある。
        // 同じスキルを2位置に使い回すバグがあれば誤って不一致(null)になる回帰テスト
        var charm = new Charm
        {
            Skills = [new("超会心", 1), new("攻撃", 1), new("見切り", 1)],
            ArmorSlots = [2, 0, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(7, RarityInference.Infer(charm));
    }

    [Theory]
    [InlineData("回避性能", 3, "会心撃【属性】", 2)]
    [InlineData("会心撃【属性】", 2, "回避性能", 3)]
    public void Rare6_MultiGroupSkill_MatchesRegardlessOfOrder(
        string name1, int lv1, string name2, int lv2)
    {
        // 「会心撃【属性】」Lv2はgroups:[2,4]の複数グループ所属スキル。
        // skillGroups[2,9]との照合で、group2の割り当てにこのスキルを正しく使えるか、
        // かつ順序に関わらず一致するかの回帰テスト
        var charm = new Charm
        {
            Skills = [new(name1, lv1), new(name2, lv2)],
            ArmorSlots = [1, 1, 0],
            WeaponSlots = [0, 0, 0],
        };
        Assert.Equal(6, RarityInference.Infer(charm));
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
}
