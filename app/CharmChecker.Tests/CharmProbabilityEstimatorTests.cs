using CharmChecker.Core.Model;
using Xunit;

namespace CharmChecker.Tests;

public class CharmProbabilityEstimatorTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void Rare5_UniformAssumption_ComputesProductOfFractions()
    {
        // skillGroups [1,6,6] は RARE5 の6パターン中の1つ、スロット[3,0]は4パターン中の1つ、
        // グループ1は37種・グループ6は37種（skill-groups.jsonで確認済み）→ 等確率仮定の積
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 1), new("アイテム使用強化", 2), new("ボマー", 2)],
            ArmorSlots = [3, 0, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = 5,
        };

        double expected = (1.0 / 6) * (1.0 / 4) * (1.0 / 37) * (1.0 / 37) * (1.0 / 37);
        double? actual = CharmProbabilityEstimator.Estimate(charm);

        Assert.NotNull(actual);
        Assert.True(Math.Abs(expected - actual!.Value) < Tolerance);
    }

    [Fact]
    public void Rare8_UsesKnownSlotWeight_NotUniform()
    {
        // skillGroups [3,10] は RARE8 の8パターン中の1つ、スロット[W1,1,1]はデータマイン出典の
        // 重み17%（ゲーム内実測による裏付けはない、CLAUDE.md参照）、
        // グループ3は40種・グループ10は10種（skill-groups.jsonで確認済み）
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 3), new("力の解放", 1)],
            ArmorSlots = [1, 1, 0],
            WeaponSlots = [1, 0, 0],
            Rarity = 8,
        };

        double expected = (1.0 / 8) * 0.17 * (1.0 / 40) * (1.0 / 10);
        double? actual = CharmProbabilityEstimator.Estimate(charm);

        Assert.NotNull(actual);
        Assert.True(Math.Abs(expected - actual!.Value) < Tolerance);
    }

    [Fact]
    public void SkillOrderReversed_ProducesSameProbability()
    {
        // charm-combinations.jsonのRARE5にskillGroups[1,7](ＫＯ術Lv1→ひるみ軽減Lv3の順)は
        // 実在するが逆順[7,1]は存在しない。護石のスキル入力順は意味を持たないため、
        // 逆順で保存されても同じ確率が算出されるべき回帰テスト
        // (グループ1は37種・グループ7は34種、RARE5は6パターン中1つ、スロット[2,1]は4パターン中1つ)
        var charm = new Charm
        {
            Skills = [new("ひるみ軽減", 3), new("ＫＯ術", 1)],
            ArmorSlots = [2, 1, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = 5,
        };

        double expected = (1.0 / 6) * (1.0 / 4) * (1.0 / 37) * (1.0 / 34);
        double? actual = CharmProbabilityEstimator.Estimate(charm);

        Assert.NotNull(actual);
        Assert.True(Math.Abs(expected - actual!.Value) < Tolerance);
    }

    [Fact]
    public void UnknownRarity_ReturnsNull()
    {
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 1)],
            ArmorSlots = [1, 0, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = null,
        };

        Assert.Null(CharmProbabilityEstimator.Estimate(charm));
    }

    [Fact]
    public void NoMatchingSlotPattern_ReturnsNull()
    {
        // skillGroups [1,6,6] 自体は RARE5 に存在するが、スロット[1,0]はこの組み合わせの
        // 有効パターン([1,1] [2,0] [2,1] [3,0])に含まれない
        var charm = new Charm
        {
            Skills = [new("ＫＯ術", 1), new("アイテム使用強化", 2), new("ボマー", 2)],
            ArmorSlots = [1, 0, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = 5,
        };

        Assert.Null(CharmProbabilityEstimator.Estimate(charm));
    }

    [Fact]
    public void UnknownSkill_ReturnsNull()
    {
        var charm = new Charm
        {
            Skills = [new("存在しないスキル", 1)],
            ArmorSlots = [1, 0, 0],
            WeaponSlots = [0, 0, 0],
            Rarity = 5,
        };

        Assert.Null(CharmProbabilityEstimator.Estimate(charm));
    }
}
