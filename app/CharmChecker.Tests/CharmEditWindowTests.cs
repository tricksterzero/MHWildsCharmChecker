using CharmChecker.App;
using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

public class CharmEditWindowTests
{
    private static Charm MakeCharm(int? rarity, params (string Name, int Lv)[] skills) => new()
    {
        Skills = skills.Select(s => new CharmSkill(s.Name, s.Lv)).ToList(),
        ArmorSlots = [2, 1, 0],
        Rarity = rarity,
    };

    // 編集でスキル・スロットを変えていなければ元のRAREを残す(2026-11-12。候補にないスキル名を含む護石では
    // 推定し直すとnullになり、何も変えずにOKを押しただけでRAREが消えていた)
    [Fact]
    public void KeepRarityIfUnchanged_SameSkillsAndSlots_KeepsExistingRarity()
    {
        var existing = MakeCharm(7, ("未知のスキル", 1), ("攻撃", 2));
        var built = new Charm
        {
            Skills = [new("攻撃", 2), new("未知のスキル", 1)],
            ArmorSlots = [1, 2, 0],
            Rarity = null,
        };

        var result = CharmEditWindow.KeepRarityIfUnchanged(built, existing);

        Assert.Equal(7, result.Rarity);
        Assert.Equal(built.Skills, result.Skills);
        Assert.Equal(built.ArmorSlots, result.ArmorSlots);
    }

    [Fact]
    public void KeepRarityIfUnchanged_ExistingRarityNull_KeepsNull()
    {
        var existing = MakeCharm(null, ("攻撃", 2));
        var built = MakeCharm(5, ("攻撃", 2));

        Assert.Null(CharmEditWindow.KeepRarityIfUnchanged(built, existing).Rarity);
    }

    [Fact]
    public void KeepRarityIfUnchanged_SkillLevelChanged_ReturnsBuiltAsIs()
    {
        var existing = MakeCharm(7, ("攻撃", 2));
        var built = MakeCharm(null, ("攻撃", 1));

        Assert.Same(built, CharmEditWindow.KeepRarityIfUnchanged(built, existing));
    }

    [Fact]
    public void KeepRarityIfUnchanged_NewInput_ReturnsBuiltAsIs()
    {
        var built = MakeCharm(5, ("攻撃", 2));

        Assert.Same(built, CharmEditWindow.KeepRarityIfUnchanged(built, null));
    }

    [Fact]
    public void KeepRarityIfUnchanged_NullBuilt_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CharmEditWindow.KeepRarityIfUnchanged(null!, null));
    }

    // 名前とLvの片方だけが入った欄はOK時に拒否する(2026-11-12。Lvを選び忘れた欄が黙って捨てられていた)
    [Fact]
    public void HalfFilledSkillRowMessage_AllFilledOrEmpty_ReturnsNull()
    {
        var rows = new List<(string, int)> { ("攻撃", 2), ("", 0), ("見切り", 1) };

        Assert.Null(CharmEditWindow.HalfFilledSkillRowMessage(rows));
    }

    [Fact]
    public void HalfFilledSkillRowMessage_NoRows_ReturnsNull()
    {
        Assert.Null(CharmEditWindow.HalfFilledSkillRowMessage([]));
    }

    [Theory]
    [InlineData("攻撃", 0, 2)]
    [InlineData("", 1, 2)]
    public void HalfFilledSkillRowMessage_OneSideOnly_ReportsThatRow(string name, int lv, int expectedRow)
    {
        var rows = new List<(string, int)> { ("攻撃", 2), (name, lv), ("", 0) };

        var message = CharmEditWindow.HalfFilledSkillRowMessage(rows);

        Assert.NotNull(message);
        Assert.StartsWith($"スキル{expectedRow}の名前とLvの片方だけが入力されています。", message);
    }

    [Fact]
    public void HalfFilledSkillRowMessage_SeveralHalfFilled_ReportsFirst()
    {
        var rows = new List<(string, int)> { ("", 3), ("見切り", 0), ("", 0) };

        Assert.StartsWith("スキル1の", CharmEditWindow.HalfFilledSkillRowMessage(rows));
    }

    [Fact]
    public void HalfFilledSkillRowMessage_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CharmEditWindow.HalfFilledSkillRowMessage(null!));
        Assert.Throws<ArgumentNullException>(() => CharmEditWindow.HalfFilledSkillRowMessage([(null!, 1)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => CharmEditWindow.HalfFilledSkillRowMessage([("攻撃", -1)]));
    }
}
