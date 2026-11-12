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

    [Fact]
    public void AreIdentical_DifferentSkillLevel_ReturnsFalse()
    {
        Assert.False(DuplicateChecker.AreIdentical(
            MakeCharm([("攻撃", 4)], [3, 0, 0]),
            MakeCharm([("攻撃", 3)], [3, 0, 0])));
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
    public void Check_NoDuplicates_NorInferiors_ReturnsEmpty()
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
    public void Check_IdenticalCharms_GroupedTogether()
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
    public void Check_ThreeIdentical_SingleGroup()
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
    public void Check_StrictlySuperior_SkillLevel()
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
    public void Check_StrictlySuperior_SlotLevel()
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
    public void Check_StrictlySuperior_WeaponSlot()
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
    public void Check_DifferentSkillNames_Incomparable()
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
    public void Check_MixedComparison_Incomparable()
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
    public void Check_DuplicatesExcludedFromInferiorCheck()
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
    public void Check_MultipleSkills_Superior()
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
    public void Check_MultipleSuperiors_AllListed()
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
    public void Check_DifferentSkillCount_Incomparable()
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

    // 同じスクショから取り込み済みの護石を除く(全画像の再スキャンで読み直した結果を追加するとき。2026-11-12)
    private static readonly DateTime ShotTime = new(2026, 6, 16, 10, 39, 23, DateTimeKind.Local);

    private static Charm Shot(Charm charm, DateTime timestamp, CharmSource source = CharmSource.Screenshot) => new()
    {
        Skills = charm.Skills,
        ArmorSlots = charm.ArmorSlots,
        WeaponSlots = charm.WeaponSlots,
        Source = source,
        SourceTimestamp = timestamp,
    };

    [Fact]
    public void ExcludeAlreadyImportedScreenshots_SameShotSameCharm_Skipped()
    {
        var existing = Shot(MakeCharm([("攻撃", 4)], [3, 1, 0]), ShotTime);
        var reread = Shot(MakeCharm([("攻撃", 4)], [1, 3, 0]), ShotTime);

        var (toAdd, skipped) = DuplicateChecker.ExcludeAlreadyImportedScreenshots([existing], [reread]);

        Assert.Empty(toAdd);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void ExcludeAlreadyImportedScreenshots_SameCharmFromOtherShot_Added()
    {
        // 同じ構成の護石を別のスクショで撮った場合(別の護石の可能性がある)は除かない。1ティック違いでも別のスクショとみなす
        var existing = Shot(MakeCharm([("攻撃", 4)]), ShotTime);
        var otherShot = Shot(MakeCharm([("攻撃", 4)]), ShotTime.AddTicks(1));

        var (toAdd, skipped) = DuplicateChecker.ExcludeAlreadyImportedScreenshots([existing], [otherShot]);

        Assert.Equal(new[] { otherShot }, toAdd);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ExcludeAlreadyImportedScreenshots_SameShotButEditedOrNotScreenshot_Added()
    {
        // 既存がスクショ由来でない、または構成が違う(取り込んだ後に編集した等)場合は除かない
        var csvExisting = Shot(MakeCharm([("攻撃", 4)]), ShotTime, CharmSource.CsvImport);
        var edited = Shot(MakeCharm([("攻撃", 3)]), ShotTime);
        var reread = Shot(MakeCharm([("攻撃", 4)]), ShotTime);

        var (toAdd, skipped) = DuplicateChecker.ExcludeAlreadyImportedScreenshots([csvExisting, edited], [reread]);

        Assert.Equal(new[] { reread }, toAdd);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ExcludeAlreadyImportedScreenshots_IdenticalWithinIncoming_AllAdded_KeepsOrder()
    {
        // 読み取り結果どうしで同一の護石は除かない(同じ構成の護石を実際に複数持っている場合がある)
        var a = Shot(MakeCharm([("攻撃", 4)]), ShotTime);
        var b = Shot(MakeCharm([("攻撃", 4)]), ShotTime.AddSeconds(5));
        var c = Shot(MakeCharm([("見切り", 2)]), ShotTime.AddSeconds(10));

        var (toAdd, skipped) = DuplicateChecker.ExcludeAlreadyImportedScreenshots([], [a, b, c]);

        Assert.Equal(new[] { a, b, c }, toAdd);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ExcludeAlreadyImportedScreenshots_TimestampSurvivesJsonRoundtrip()
    {
        // 既存の護石はcharms.jsonから読んだもの。ファイルの更新日時(100ナノ秒単位)が保存・読み込みで変わらないことを確かめる
        var original = Shot(MakeCharm([("攻撃", 4)]), ShotTime.AddTicks(1234567));
        var loaded = CharmJsonSerializer.Parse(CharmJsonSerializer.Serialize([original])).Charms.Single();

        var (toAdd, skipped) = DuplicateChecker.ExcludeAlreadyImportedScreenshots([loaded], [original]);

        Assert.Empty(toAdd);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void ExcludeAlreadyImportedScreenshots_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => DuplicateChecker.ExcludeAlreadyImportedScreenshots(null!, []));
        Assert.Throws<ArgumentNullException>(() => DuplicateChecker.ExcludeAlreadyImportedScreenshots([], null!));
    }
}
