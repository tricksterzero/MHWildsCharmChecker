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
        // 「カード性能」は濁点を除くと「カート性能」になり、「ガード性能」の濁点を除いた「カート性能」と一致する
        Assert.Equal("ガード性能", SkillNameNormalizer.Normalize("カード性能", Skills));
    }

    [Fact]
    public void LongerMatchPreferred()
    {
        // 「龍属性攻撃強化」は「攻撃」も含むが、長い名前が優先される。正規名そのものだと完全一致で先に
        // 決まってしまうため、先頭に余計な文字を付けて部分一致の処理を通す
        Assert.Equal("龍属性攻撃強化", SkillNameNormalizer.Normalize("x龍属性攻撃強化", Skills));
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

    [Fact]
    public void PartialMatch_UnsortedInput_LongerNameStillPreferred()
    {
        // 呼び出し元が長さ順ソートを怠っても、Normalize内部でソートし直し短い一致に惑わされない
        var unsorted = new[] { "攻撃", "龍属性攻撃強化" };
        Assert.Equal("龍属性攻撃強化", SkillNameNormalizer.Normalize("x龍属性攻撃強化", unsorted));
    }

    [Fact]
    public void DakutenFallback_Hiragana()
    {
        var skills = new[] { "飛び込み" };
        Assert.Equal("飛び込み", SkillNameNormalizer.Normalize("飛ひ込み", skills));
    }

    [Fact]
    public void HalfWidthAlphabet_NormalizedToFullWidth()
    {
        var skills = new[] { "ＫＯ術" };
        Assert.Equal("ＫＯ術", SkillNameNormalizer.Normalize("KO術", skills));
    }

    [Fact]
    public void KanaFallback_HijibaKa_MatchesHijibaChikara()
    {
        var skills = new[] { "火事場力" };
        Assert.Equal("火事場力", SkillNameNormalizer.Normalize("火事場カ", skills));
    }

    [Fact]
    public void KanaFallback_PrefersLongerNameOverContainedShorterName()
    {
        // 「防御」⊂「防御力ＤＯＷＮ耐性」という包含関係を持つ2つの正規名がある状態で、
        // OCRが「力」を「カ」に誤認識した場合。カナ誤認フォールバックを要する長い名前より、
        // 素の部分一致で先に見つかる短い名前(防御)を誤って優先してしまう回帰テスト
        // (フォールバック別に全スキルを1周ずつ試す実装だと、素の部分一致の周で
        // 「防御」が先に確定してしまいカナ誤認フォールバックの周に到達できなかった)
        var skills = new[] { "防御", "防御力ＤＯＷＮ耐性" };
        Assert.Equal("防御力ＤＯＷＮ耐性", SkillNameNormalizer.Normalize("防御カＤＯＷＮ耐性", skills));
    }
}
