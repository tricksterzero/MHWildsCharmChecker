using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>
/// CSV・JSONの取り込み時の検査(CharmImportScreening)のテスト。扱いは2026-11-12にMainWindowから移す前と同じ
/// (CSVはErrorが1件でもあれば全体を取り込まず、JSONはErrorの項目だけを除く)。
/// </summary>
public class CharmImportScreeningTests
{
    private static readonly IReadOnlyList<SkillGroupEntry> SkillGroups = CharmCombinationTable.LoadSkillGroups();

    private static Charm Make((string Name, int Lv)[] skills, int[] armor) => new()
    {
        Skills = skills.Select(s => new CharmSkill(s.Name, s.Lv)).ToList(),
        ArmorSlots = armor,
    };

    private static readonly Charm Valid = Make([("ＫＯ術", 3), ("アイテム使用強化", 2)], [2, 0, 0]);
    private static readonly Charm WithWarning = Make([("存在しないスキル", 1)], [1, 0, 0]);
    private static readonly Charm WithError = Make([("ＫＯ術", 9)], [1, 0, 0]);

    [Fact]
    public void ScreenCsv_NoErrors_AcceptsAllWithInferredRarityAndCountsWarnings()
    {
        var result = CharmImportScreening.ScreenCsv([Valid, WithWarning], SkillGroups);

        Assert.False(result.HasError);
        Assert.Null(result.ErrorLineNumber);
        Assert.Empty(result.ErrorMessages);
        Assert.Equal(2, result.Accepted.Count);
        Assert.Equal(RarityInference.WithInferredRarity(Valid).Rarity, result.Accepted[0].Rarity);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void ScreenCsv_AnyError_AcceptsNothingAndReportsFirstErrorLine()
    {
        var anotherError = Make([("ＫＯ術", 8)], [1, 0, 0]);
        var result = CharmImportScreening.ScreenCsv([Valid, WithError, anotherError], SkillGroups);

        Assert.True(result.HasError);
        Assert.Equal(2, result.ErrorLineNumber);
        Assert.Equal(new[] { "「ＫＯ術」にLv9は存在しません。" }, result.ErrorMessages);
        Assert.Empty(result.Accepted);
    }

    [Fact]
    public void ScreenCsv_Empty_NoError()
    {
        // 境界値: 取り込む行が0件
        var result = CharmImportScreening.ScreenCsv([], SkillGroups);
        Assert.False(result.HasError);
        Assert.Empty(result.Accepted);
        Assert.Equal(0, result.WarningCount);
    }

    [Fact]
    public void ScreenJson_ExcludesRejectedAndErrorItems_KeepsWarningItems()
    {
        var parseResult = new CharmJsonParseResult([Valid, WithError, WithWarning], [new CharmJsonRejection(0, "項目がnullです。")]);
        var result = CharmImportScreening.ScreenJson(parseResult, SkillGroups);

        Assert.Equal(2, result.Accepted.Count);
        Assert.Equal(RarityInference.WithInferredRarity(Valid).Rarity, result.Accepted[0].Rarity);
        Assert.Equal(WithWarning.Skills, result.Accepted[1].Skills);
        Assert.Equal(new[] { "1件目: 項目がnullです。", "「ＫＯ術」にLv9は存在しません。" }, result.ExcludedReasons);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void ScreenJson_Empty_NothingAcceptedOrExcluded()
    {
        // 境界値: 項目が0件
        var result = CharmImportScreening.ScreenJson(new CharmJsonParseResult([], []), SkillGroups);
        Assert.Empty(result.Accepted);
        Assert.Empty(result.ExcludedReasons);
        Assert.Equal(0, result.WarningCount);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CharmImportScreening.ScreenCsv(null!, SkillGroups));
        Assert.Throws<ArgumentNullException>(() => CharmImportScreening.ScreenCsv([], null!));
        Assert.Throws<ArgumentNullException>(() => CharmImportScreening.ScreenJson(null!, SkillGroups));
        Assert.Throws<ArgumentNullException>(() => CharmImportScreening.ScreenJson(new CharmJsonParseResult([], []), null!));
    }
}
