using CharmChecker.App;
using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>画面に出す護石の表示文字列(CharmDisplayText)のテスト。書式は変更前の画面の表示と同じ。</summary>
public class CharmDisplayTextTests
{
    private static Charm Make((string Name, int Lv)[] skills, int[]? armor = null, int[]? weapon = null, int? rarity = null) => new()
    {
        Skills = skills.Select(s => new CharmSkill(s.Name, s.Lv)).ToList(),
        ArmorSlots = armor ?? [0, 0, 0],
        WeaponSlots = weapon ?? [0, 0, 0],
        Rarity = rarity,
    };

    [Fact]
    public void Rarity_KnownOrUnknown()
    {
        Assert.Equal("7", CharmDisplayText.Rarity(Make([("攻撃", 1)], rarity: 7)));
        Assert.Equal("-", CharmDisplayText.Rarity(Make([("攻撃", 1)])));
    }

    [Fact]
    public void Skills_JoinedWithSeparator()
    {
        var charm = Make([("攻撃", 2), ("見切り", 1)]);
        Assert.Equal("攻撃 Lv2 / 見切り Lv1", CharmDisplayText.Skills(charm, " / "));
        Assert.Equal("攻撃 Lv2\n見切り Lv1", CharmDisplayText.Skills(charm, "\n"));
    }

    [Fact]
    public void Skills_None_ShowsPlaceholder()
    {
        // 境界値: スキルが0個
        Assert.Equal("（なし）", CharmDisplayText.Skills(Make([]), " / "));
    }

    [Fact]
    public void Slots_WeaponFirstThenArmorLargestFirst()
    {
        Assert.Equal("武1 - 防2 - 防1", CharmDisplayText.Slots(Make([], armor: [1, 2, 0], weapon: [1, 0, 0])));
        Assert.Equal("防3", CharmDisplayText.Slots(Make([], armor: [0, 0, 3])));
    }

    [Fact]
    public void Slots_None_ShowsPlaceholder()
    {
        Assert.Equal("なし", CharmDisplayText.Slots(Make([])));
    }

    [Fact]
    public void SlotsDetail_WeaponAndArmorLines()
    {
        Assert.Equal("武器: Lv1\n防具: Lv2, Lv1", CharmDisplayText.SlotsDetail(Make([], armor: [1, 2, 0], weapon: [1, 0, 0])));
        Assert.Equal("防具: Lv3", CharmDisplayText.SlotsDetail(Make([], armor: [3, 0, 0])));
        Assert.Equal("武器: Lv1", CharmDisplayText.SlotsDetail(Make([], weapon: [0, 1, 0])));
    }

    [Fact]
    public void SlotsDetail_None_ShowsPlaceholder()
    {
        Assert.Equal("なし", CharmDisplayText.SlotsDetail(Make([])));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CharmDisplayText.Rarity(null!));
        Assert.Throws<ArgumentNullException>(() => CharmDisplayText.Skills(null!, " / "));
        Assert.Throws<ArgumentNullException>(() => CharmDisplayText.Skills(Make([]), null!));
        Assert.Throws<ArgumentNullException>(() => CharmDisplayText.Slots(null!));
        Assert.Throws<ArgumentNullException>(() => CharmDisplayText.SlotsDetail(null!));
    }
}
