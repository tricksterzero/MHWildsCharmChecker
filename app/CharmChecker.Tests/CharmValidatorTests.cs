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

    [Fact]
    public void ErrorMessages_ReturnsOnlyErrorsInOriginalOrder()
    {
        CharmIssue[] issues =
        [
            new(CharmIssueSeverity.Error, "エラー1"),
            new(CharmIssueSeverity.Warning, "警告"),
            new(CharmIssueSeverity.Error, "エラー2"),
        ];
        Assert.Equal(new[] { "エラー1", "エラー2" }, CharmValidator.ErrorMessages(issues));
    }

    [Fact]
    public void ErrorMessages_OnlyWarnings_ReturnsEmpty()
    {
        Assert.Empty(CharmValidator.ErrorMessages([new CharmIssue(CharmIssueSeverity.Warning, "警告")]));
    }

    [Fact]
    public void ErrorMessages_NoIssues_ReturnsEmpty()
    {
        // 境界値: 問題が0件
        Assert.Empty(CharmValidator.ErrorMessages([]));
    }

    [Fact]
    public void ErrorMessages_FromValidate_MatchesHasError()
    {
        // 候補にないスキル名(Warning)と、存在しないLv(Error)が混ざった検査結果から、Errorの文だけを取り出す
        var issues = CharmValidator.Validate(Make([("存在しないスキル", 1), ("ＫＯ術", 9)], [1, 0, 0]));
        var messages = CharmValidator.ErrorMessages(issues);
        Assert.Equal(new[] { "「ＫＯ術」にLv9は存在しません。" }, messages);
        Assert.Equal(CharmValidator.HasError(issues), messages.Count > 0);
    }

    [Fact]
    public void ErrorMessages_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CharmValidator.ErrorMessages(null!));
    }
}
