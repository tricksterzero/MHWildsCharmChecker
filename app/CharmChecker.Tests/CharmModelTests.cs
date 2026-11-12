using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>護石(Charm)の不変条件のテスト。</summary>
public class CharmModelTests
{
    [Fact]
    public void Default_SatisfiesInvariants()
    {
        var charm = new Charm();
        Assert.Empty(charm.Skills);
        Assert.Equal([0, 0, 0], charm.ArmorSlots);
        Assert.Equal([0, 0, 0], charm.WeaponSlots);
        Assert.Null(charm.Rarity);
    }

    [Fact]
    public void Skills_UpToThree_Accepted_FourRejected()
    {
        // 境界値: スキルは3個まで
        _ = new Charm { Skills = [new("a", 1), new("b", 1), new("c", 1)] };
        Assert.Throws<ArgumentException>(() => new Charm { Skills = [new("a", 1), new("b", 1), new("c", 1), new("d", 1)] });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Skills_BlankName_Rejected(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Charm { Skills = [new(name!, 1)] });
    }

    [Fact]
    public void Skills_DefaultCharmSkill_Rejected()
    {
        // default(CharmSkill)は名前null・Lv0
        Assert.Throws<ArgumentException>(() => new Charm { Skills = [default] });
    }

    [Theory]
    [InlineData(0, false)] // 境界値: Lvは1以上
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void Skills_LevelAtLeastOne(int lv, bool valid)
    {
        if (valid)
            _ = new Charm { Skills = [new("攻撃", lv)] };
        else
            Assert.Throws<ArgumentException>(() => new Charm { Skills = [new("攻撃", lv)] });
    }

    [Fact]
    public void Skills_DuplicateName_Rejected()
    {
        Assert.Throws<ArgumentException>(() => new Charm { Skills = [new("攻撃", 4), new("攻撃", 2)] });
    }

    [Fact]
    public void Slots_ShorterThanThree_PaddedWithZero()
    {
        var charm = new Charm { ArmorSlots = [2], WeaponSlots = [] };
        Assert.Equal([2, 0, 0], charm.ArmorSlots);
        Assert.Equal([0, 0, 0], charm.WeaponSlots);
    }

    [Fact]
    public void Slots_LongerThanThree_TrailingZerosTrimmed_NonZeroRejected()
    {
        Assert.Equal([1, 1, 0], new Charm { ArmorSlots = [1, 1, 0, 0] }.ArmorSlots);
        Assert.Throws<ArgumentException>(() => new Charm { ArmorSlots = [1, 1, 0, 1] });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)] // 境界値: スロットの値は0〜3
    public void Slots_ValueOutOfRange_Rejected(int value)
    {
        Assert.Throws<ArgumentException>(() => new Charm { ArmorSlots = [value, 0, 0] });
        Assert.Throws<ArgumentException>(() => new Charm { WeaponSlots = [value, 0, 0] });
    }

    [Fact]
    public void Slots_ValueThree_Accepted()
    {
        Assert.Equal([3, 0, 0], new Charm { ArmorSlots = [3, 0, 0] }.ArmorSlots);
    }

    [Theory]
    [InlineData(4, false)] // 境界値: RAREはnullか5〜8
    [InlineData(5, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void Rarity_Range(int rarity, bool valid)
    {
        if (valid)
            Assert.Equal(rarity, new Charm { Rarity = rarity }.Rarity);
        else
            Assert.Throws<ArgumentException>(() => new Charm { Rarity = rarity });
    }

    [Fact]
    public void NullCollections_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Charm { Skills = null! });
        Assert.Throws<ArgumentNullException>(() => new Charm { ArmorSlots = null! });
        Assert.Throws<ArgumentNullException>(() => new Charm { WeaponSlots = null! });
    }

    [Fact]
    public void Collections_CannotBeModifiedThroughCastOrSourceList()
    {
        // 渡したリストを後から書き換えても護石は変わらず、公開されたリストを元の型に戻しても書き換えられない
        var skills = new List<CharmSkill> { new("攻撃", 1) };
        var armor = new List<int> { 1, 0, 0 };
        var charm = new Charm { Skills = skills, ArmorSlots = armor };
        skills.Add(new CharmSkill("見切り", 1));
        armor[0] = 3;
        Assert.Single(charm.Skills);
        Assert.Equal(1, charm.ArmorSlots[0]);

        Assert.False(charm.ArmorSlots is List<int>);
        Assert.False(charm.Skills is List<CharmSkill>);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)charm.ArmorSlots)[0] = 3);
    }

    [Fact]
    public void WithRarity_ReturnsNewInstanceAndKeepsOthers()
    {
        var timestamp = new DateTime(2026, 11, 8);
        var charm = new Charm
        {
            Skills = [new("攻撃", 1)],
            ArmorSlots = [1, 0, 0],
            Source = CharmSource.Screenshot,
            SourceTimestamp = timestamp,
            Version = GameVersion.Wilds,
        };
        var updated = charm.WithRarity(6);

        Assert.NotSame(charm, updated);
        Assert.Null(charm.Rarity);
        Assert.Equal(6, updated.Rarity);
        Assert.Equal(charm.Skills, updated.Skills);
        Assert.Equal(charm.ArmorSlots, updated.ArmorSlots);
        Assert.Equal(CharmSource.Screenshot, updated.Source);
        Assert.Equal(timestamp, updated.SourceTimestamp);
        Assert.Throws<ArgumentException>(() => charm.WithRarity(9));
    }

    [Fact]
    public void SlotsDescending_Default_AllZero()
    {
        var charm = new Charm();
        Assert.Equal([0, 0, 0], charm.ArmorSlotsDescending);
        Assert.Equal([0, 0, 0], charm.WeaponSlotsDescending);
    }

    [Fact]
    public void SlotsDescending_SortedLargestFirst_OriginalOrderKept()
    {
        var charm = new Charm { ArmorSlots = [0, 1, 2], WeaponSlots = [0, 1, 0] };
        Assert.Equal([2, 1, 0], charm.ArmorSlotsDescending);
        Assert.Equal([1, 0, 0], charm.WeaponSlotsDescending);
        Assert.Equal([0, 1, 2], charm.ArmorSlots);
        Assert.Equal([0, 1, 0], charm.WeaponSlots);
    }

    [Fact]
    public void SlotsDescending_ShorterInput_PaddedToSlotCount()
    {
        // 境界値: 渡したスロットが3個未満でも、降順のものは常に3個
        var charm = new Charm { ArmorSlots = [1], WeaponSlots = [] };
        Assert.Equal([1, 0, 0], charm.ArmorSlotsDescending);
        Assert.Equal([0, 0, 0], charm.WeaponSlotsDescending);
    }

    [Fact]
    public void SlotsDescending_CannotBeModifiedThroughCast()
    {
        var charm = new Charm { ArmorSlots = [0, 2, 1] };
        Assert.Throws<NotSupportedException>(() => ((IList<int>)charm.ArmorSlotsDescending)[0] = 3);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)charm.WeaponSlotsDescending)[0] = 1);
    }

    [Fact]
    public void SlotsDescending_KeptByWithRarity()
    {
        var updated = new Charm { ArmorSlots = [0, 1, 2], WeaponSlots = [0, 1, 0] }.WithRarity(8);
        Assert.Equal([2, 1, 0], updated.ArmorSlotsDescending);
        Assert.Equal([1, 0, 0], updated.WeaponSlotsDescending);
    }
}
