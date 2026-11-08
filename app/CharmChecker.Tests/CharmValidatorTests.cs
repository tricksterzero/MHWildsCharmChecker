using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>護石がゲームとしてありうるかの検査(全入口共通)のテスト。</summary>
public class CharmValidatorTests
{
    private static Charm Make((string Name, int Lv)[] skills, int[]? armor = null, int[]? weapon = null) => new()
    {
        Skills = skills.Select(s => new CharmSkill(s.Name, s.Lv)).ToList(),
        ArmorSlots = armor ?? [0, 0, 0],
        WeaponSlots = weapon ?? [0, 0, 0],
    };

    [Fact]
    public void ValidCharm_NoIssues()
    {
        Assert.Empty(CharmValidator.Validate(Make([("ＫＯ術", 3), ("アイテム使用強化", 2)], [2, 0, 0])));
    }

    [Fact]
    public void UnknownSkillName_IsWarning()
    {
        var issues = CharmValidator.Validate(Make([("存在しないスキル", 1)], [1, 0, 0]));
        var issue = Assert.Single(issues);
        Assert.Equal(CharmIssueSeverity.Warning, issue.Severity);
        Assert.False(CharmValidator.HasError(issues));
    }

    [Fact]
    public void KnownSkillWithNonexistentLevel_IsError()
    {
        // ＫＯ術にLv9はない
        var issues = CharmValidator.Validate(Make([("ＫＯ術", 9)], [1, 0, 0]));
        Assert.True(CharmValidator.HasError(issues));
    }

    [Fact]
    public void NoSkills_IsError()
    {
        Assert.True(CharmValidator.HasError(CharmValidator.Validate(Make([], [1, 0, 0]))));
    }

    [Theory]
    [InlineData(new[] { 3, 3, 0 }, new[] { 0, 0, 0 })] // 防具の2個目が最大Lv1を超える
    [InlineData(new[] { 1, 1, 1 }, new[] { 0, 0, 0 })] // 防具3個
    [InlineData(new[] { 1, 0, 0 }, new[] { 1, 1, 0 })] // 武器2個
    [InlineData(new[] { 1, 0, 0 }, new[] { 2, 0, 0 })] // 武器Lv2
    public void ImpossibleSlotShape_IsError(int[] armor, int[] weapon)
    {
        Assert.True(CharmValidator.HasError(CharmValidator.Validate(Make([("ＫＯ術", 3)], armor, weapon))));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CharmValidator.Validate(null!, []));
        Assert.Throws<ArgumentNullException>(() => CharmValidator.Validate(Make([("ＫＯ術", 3)]), null!));
    }
}
