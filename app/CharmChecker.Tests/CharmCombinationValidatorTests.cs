using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>
/// 読み取った護石が構成表(charm-combinations.json)のいずれかの組み合わせに当てはまるかの判定。
/// スクショの読み取りで、スキルの読み落としや2つの護石の取り違えによる「実在しない護石」を
/// 除外するために使う(2026-11-07)。テストデータは実際のスクショで確認した護石の構成。
/// </summary>
public class CharmCombinationValidatorTests
{
    private static Charm MakeCharm(int? rarity, (string Name, int Lv)[] skills, int[] armor, int[] weapon) => new()
    {
        Rarity = rarity,
        Skills = skills.Select(s => new CharmSkill(s.Name, s.Lv)).ToList(),
        ArmorSlots = armor.ToList(),
        WeaponSlots = weapon.ToList(),
    };

    [Fact]
    public void MatchesAnyCombination_ValidRare8Charm_ReturnsTrue()
    {
        // 栄世の護石 高速変形3/属性変換1 武器1+防具1(20261102104710_1.jpg)
        var charm = MakeCharm(8, [("高速変形", 3), ("属性変換", 1)], [1, 0, 0], [1, 0, 0]);
        Assert.True(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_SingleSkill_ReturnsFalse()
    {
        // 構成表の組み合わせはすべてスキル2つ以上。巧撃を読み落とした結果(20261102110031_1.jpg)
        var charm = MakeCharm(8, [("達人芸", 1)], [1, 0, 0], [1, 0, 0]);
        Assert.False(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_SkillsMatchButSlotsDoNot_ReturnsFalse()
    {
        // 秘歴の護石 弾導強化2/スタミナ奪取1/逆恨み1 の実物はLv3スロット1つ(20660C1.JPG)。
        // Lv1と読み違えた[1,0,0]はこのスキル構成の組み合わせに存在しない
        var charm = MakeCharm(7, [("弾導強化", 2), ("スタミナ奪取", 1), ("逆恨み", 1)], [1, 0, 0], [0, 0, 0]);
        Assert.False(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_SameSkillsWithCorrectSlot_ReturnsTrue()
    {
        var charm = MakeCharm(7, [("弾導強化", 2), ("スタミナ奪取", 1), ("逆恨み", 1)], [3, 0, 0], [0, 0, 0]);
        Assert.True(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_RarityKnown_OnlyThatRarityIsConsidered()
    {
        // 史伝の護石(RARE6)の構成。RARE6としては当てはまるが、RARE8(武器スロット必須)としては当てはまらない
        var skills = new[] { ("攻撃", 2), ("逆襲", 1), ("早食い", 2) };
        Assert.True(CharmCombinationValidator.MatchesAnyCombination(MakeCharm(6, skills, [1, 1, 0], [0, 0, 0])));
        Assert.False(CharmCombinationValidator.MatchesAnyCombination(MakeCharm(8, skills, [1, 1, 0], [0, 0, 0])));
    }

    [Fact]
    public void MatchesAnyCombination_RarityUnknown_AnyRarityIsConsidered()
    {
        var charm = MakeCharm(null, [("攻撃", 2), ("逆襲", 1), ("早食い", 2)], [1, 1, 0], [0, 0, 0]);
        Assert.True(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_SkillLevelNotInSkillGroups_ReturnsFalse()
    {
        var charm = MakeCharm(8, [("高速変形", 9), ("属性変換", 1)], [1, 0, 0], [1, 0, 0]);
        Assert.False(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_KensanCharm_IsOutsideTableAndReturnsTrue()
    {
        // 研鑽は希望の護石(予約特典)固有で構成表の対象外のため、照合せずに通す
        var charm = MakeCharm(5, [("研鑽", 1)], [0, 0, 0], [0, 0, 0]);
        Assert.True(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_NoSkills_ReturnsFalse()
    {
        var charm = MakeCharm(8, [], [1, 0, 0], [1, 0, 0]);
        Assert.False(CharmCombinationValidator.MatchesAnyCombination(charm));
    }

    [Fact]
    public void MatchesAnyCombination_NullArguments_Throw()
    {
        var charm = MakeCharm(8, [("高速変形", 3), ("属性変換", 1)], [1, 0, 0], [1, 0, 0]);
        Assert.Throws<ArgumentNullException>(() => CharmCombinationValidator.MatchesAnyCombination(null!));
        Assert.Throws<ArgumentNullException>(() => CharmCombinationValidator.MatchesAnyCombination(
            charm, null!, CharmCombinationTable.LoadCombinations()));
        Assert.Throws<ArgumentNullException>(() => CharmCombinationValidator.MatchesAnyCombination(
            charm, CharmCombinationTable.LoadSkillGroups(), null!));
    }
}
