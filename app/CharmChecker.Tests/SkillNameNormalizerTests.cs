using CharmChecker.Core.Skill;

namespace CharmChecker.Tests;

public class SkillNameNormalizerTests
{
    private static readonly IReadOnlyList<string> Skills = new[]
    {
        "スタミナ急速回復", "龍属性攻撃強化", "氷属性攻撃強化",
        "ファーストショット", "ガード性能", "攻撃", "見切り", "渾身", "集中"
    };

    [Fact]
    public void ExactMatch()
    {
        Assert.Equal("攻撃", SkillNameNormalizer.Normalize("攻撃", Skills));
    }

    [Fact]
    public void PartialMatch_LeadingGarbage()
    {
        Assert.Equal("見切り", SkillNameNormalizer.Normalize("x見切り", Skills));
    }

    [Fact]
    public void DakutenFallback()
    {
        // "カート性能" → 濁点除去で "カート性能" vs "カート性能" → "ガード性能"
        Assert.Equal("ガード性能", SkillNameNormalizer.Normalize("カード性能", Skills));
    }

    [Fact]
    public void LongerMatchPreferred()
    {
        // "龍属性攻撃強化" contains "攻撃" but longer match should win
        Assert.Equal("龍属性攻撃強化", SkillNameNormalizer.Normalize("龍属性攻撃強化", Skills));
    }

    [Fact]
    public void EmptyReturnsNull()
    {
        Assert.Null(SkillNameNormalizer.Normalize("", Skills));
        Assert.Null(SkillNameNormalizer.Normalize("  ", Skills));
    }

    [Fact]
    public void UnknownReturnsNull()
    {
        Assert.Null(SkillNameNormalizer.Normalize("存在しないスキル", Skills));
    }
}
