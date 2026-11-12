using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

public class DuplicateCheckerTests
{
    private static Charm MakeCharm(
        (string name, int lv)[]? skills = null,
        int[]? armorSlots = null,
        int[]? weaponSlots = null)
    {
        return new Charm
        {
            Skills = (skills ?? []).Select(s => new CharmSkill(s.name, s.lv)).ToList(),
            ArmorSlots = armorSlots?.ToList() ?? [0, 0, 0],
            WeaponSlots = weaponSlots?.ToList() ?? [0, 0, 0],
        };
    }

    [Fact]
    public void AreIdentical_SameSkillsInDifferentOrderAndSortedSlots_ReturnsTrue()
    {
        Assert.True(DuplicateChecker.AreIdentical(
            MakeCharm([("攻撃", 4), ("見切り", 2)], [3, 1, 0], [1, 0, 0]),
            MakeCharm([("見切り", 2), ("攻撃", 4)], [1, 3, 0], [1, 0, 0])));
    }

    [Theory]
    [InlineData(3)]  // Lv違い
    public void AreIdentical_DifferentSkillLevel_ReturnsFalse(int lv)
    {
        Assert.False(DuplicateChecker.AreIdentical(
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
            MakeCharm([("攻撃", lv)], [3, 0, 0])));
    }

    [Fact]
    public void AreIdentical_DifferentSlots_ReturnsFalse()
    {
        Assert.False(DuplicateChecker.AreIdentical(
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
            MakeCharm([("攻撃", 4)], [3, 1, 0])));
        Assert.False(DuplicateChecker.AreIdentical(
            MakeCharm([("攻撃", 4)], [1, 0, 0], [0, 0, 0]),
            MakeCharm([("攻撃", 4)], [0, 0, 0], [1, 0, 0])));
    }

    [Fact]
    public void AreIdentical_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DuplicateChecker.AreIdentical(null!, MakeCharm()));
        Assert.Throws<ArgumentNullException>(() => DuplicateChecker.AreIdentical(MakeCharm(), null!));
    }

    [Fact]
    public void NoDuplicates_NorInferiors_ReturnsEmpty()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
            MakeCharm([("見切り", 2)], [1, 0, 0]),
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.Inferiors);
    }

    [Fact]
    public void IdenticalCharms_GroupedTogether()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [3, 1, 0]),
            MakeCharm([("見切り", 2)], [1, 0, 0]),
            MakeCharm([("攻撃", 4)], [1, 3, 0]),  // スロット順違いだが降順ソート後は同一
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Single(result.DuplicateGroups);
        Assert.Contains(0, result.DuplicateGroups[0].Indices);
        Assert.Contains(2, result.DuplicateGroups[0].Indices);
    }

    [Fact]
    public void ThreeIdentical_SingleGroup()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)]),
            MakeCharm([("攻撃", 4)]),
            MakeCharm([("攻撃", 4)]),
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Single(result.DuplicateGroups);
        Assert.Equal(3, result.DuplicateGroups[0].Indices.Count);
    }

    [Fact]
    public void StrictlySuperior_SkillLevel()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [1, 0, 0]),  // 上位
            MakeCharm([("攻撃", 2)], [1, 0, 0]),  // 下位
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Single(result.Inferiors);
        Assert.Equal(1, result.Inferiors[0].TargetIndex);
        Assert.Contains(0, result.Inferiors[0].SuperiorIndices);
    }

    [Fact]
    public void StrictlySuperior_SlotLevel()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [3, 1, 0]),  // 上位
            MakeCharm([("攻撃", 4)], [2, 0, 0]),  // 下位
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Single(result.Inferiors);
        Assert.Equal(1, result.Inferiors[0].TargetIndex);
    }

    [Fact]
    public void StrictlySuperior_WeaponSlot()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [1, 0, 0], [2, 0, 0]),  // 上位
            MakeCharm([("攻撃", 4)], [1, 0, 0], [1, 0, 0]),  // 下位
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Single(result.Inferiors);
        Assert.Equal(1, result.Inferiors[0].TargetIndex);
    }

    [Fact]
    public void DifferentSkillNames_Incomparable()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
            MakeCharm([("見切り", 4)], [1, 0, 0]),
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.Inferiors);
    }

    [Fact]
    public void MixedComparison_Incomparable()
    {
        // スキルは上位だがスロットは下位 → 比較不能
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [1, 0, 0]),
            MakeCharm([("攻撃", 2)], [3, 0, 0]),
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.Inferiors);
    }

    [Fact]
    public void DuplicatesExcludedFromInferiorCheck()
    {
        // 0,1は同一 → 重複グループへ。2は0,1より下位だが、重複除外されるので比較対象外
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
            MakeCharm([("攻撃", 2)], [1, 0, 0]),
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Single(result.DuplicateGroups);
        Assert.Empty(result.Inferiors);
    }

    [Fact]
    public void MultipleSkills_Superior()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4), ("見切り", 3)], [2, 0, 0]),  // 上位
            MakeCharm([("攻撃", 3), ("見切り", 2)], [1, 0, 0]),  // 下位
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Single(result.Inferiors);
        Assert.Equal(1, result.Inferiors[0].TargetIndex);
    }

    [Fact]
    public void MultipleSuperiors_AllListed()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4)], [3, 0, 0]),  // 上位A
            MakeCharm([("攻撃", 5)], [2, 0, 0]),  // 上位B
            MakeCharm([("攻撃", 2)], [1, 0, 0]),  // 下位
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Single(result.Inferiors);
        Assert.Equal(2, result.Inferiors[0].TargetIndex);
        Assert.Equal(2, result.Inferiors[0].SuperiorIndices.Count);
    }

    [Fact]
    public void DifferentSkillCount_Incomparable()
    {
        var charms = new[]
        {
            MakeCharm([("攻撃", 4), ("見切り", 3)], [1, 0, 0]),
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
        };

        var result = DuplicateChecker.Check(charms);

        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.Inferiors);
    }

    [Fact]
    public void ExcludeIdentical_SkipsIdenticalToExistingAndEarlierIncoming_KeepsOrder()
    {
        // 取り込みの差分追加: 既存と同一のもの、取り込むデータの中で先に出てきたものと同一のものを読み飛ばす
        var existing = MakeCharm([("攻撃", 4)], [3, 1, 0]);
        var sameAsExisting = MakeCharm([("攻撃", 4)], [1, 3, 0]);
        var first = MakeCharm([("見切り", 2)], [1, 0, 0]);
        var sameAsFirst = MakeCharm([("見切り", 2)], [0, 1, 0]);
        var second = MakeCharm([("見切り", 3)], [1, 0, 0]);

        var (toAdd, skipped) = DuplicateChecker.ExcludeIdentical([existing], [sameAsExisting, first, sameAsFirst, second]);

        Assert.Equal(new[] { first, second }, toAdd);
        Assert.Equal(2, skipped);
    }

    [Fact]
    public void ExcludeIdentical_NoExisting_AddsAllDistinct()
    {
        var a = MakeCharm([("攻撃", 4)]);
        var b = MakeCharm([("見切り", 2)]);
        var (toAdd, skipped) = DuplicateChecker.ExcludeIdentical([], [a, b]);
        Assert.Equal(new[] { a, b }, toAdd);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ExcludeIdentical_NoIncoming_ReturnsEmpty()
    {
        // 境界値: 取り込むものが0件
        var (toAdd, skipped) = DuplicateChecker.ExcludeIdentical([MakeCharm([("攻撃", 4)])], []);
        Assert.Empty(toAdd);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ExcludeIdentical_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => DuplicateChecker.ExcludeIdentical(null!, []));
        Assert.Throws<ArgumentNullException>(() => DuplicateChecker.ExcludeIdentical([], null!));
    }
}
