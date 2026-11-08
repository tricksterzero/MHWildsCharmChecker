using System.Text.Json;
using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>charms.jsonの形式と護石の相互変換のテスト。</summary>
public class CharmJsonSerializerTests
{
    private const string ValidItem =
        """{"skills":[{"name":"攻撃","lv":4}],"armorSlots":[3,0,0],"weaponSlots":[0,0,0],"rarity":7,"source":"Screenshot","sourceTimestamp":"2026-11-08T12:00:00","version":"Wilds"}""";

    [Fact]
    public void Roundtrip_PreservesData()
    {
        var charm = new Charm
        {
            Skills = [new("攻撃", 4), new("見切り", 1)],
            ArmorSlots = [3, 1, 0],
            WeaponSlots = [1, 0, 0],
            Rarity = 8,
            Source = CharmSource.Manual,
            SourceTimestamp = new DateTime(2026, 11, 8, 12, 0, 0),
            Version = GameVersion.Wilds,
        };

        var result = CharmJsonSerializer.Parse(CharmJsonSerializer.Serialize([charm]));

        Assert.Empty(result.Rejected);
        var parsed = Assert.Single(result.Charms);
        Assert.Equal(charm.Skills, parsed.Skills);
        Assert.Equal(charm.ArmorSlots, parsed.ArmorSlots);
        Assert.Equal(charm.WeaponSlots, parsed.WeaponSlots);
        Assert.Equal(charm.Rarity, parsed.Rarity);
        Assert.Equal(charm.Source, parsed.Source);
        Assert.Equal(charm.SourceTimestamp, parsed.SourceTimestamp);
    }

    [Fact]
    public void Serialize_KeepsExistingPropertyNames()
    {
        // 既存のcharms.jsonと互換を保つため、プロパティ名を変えていないことを確かめる
        var json = CharmJsonSerializer.Serialize([new Charm { Skills = [new("攻撃", 1)] }]);
        foreach (var name in new[] { "\"skills\"", "\"name\"", "\"lv\"", "\"armorSlots\"", "\"weaponSlots\"", "\"rarity\"", "\"source\"", "\"sourceTimestamp\"", "\"version\"" })
            Assert.Contains(name, json);
        Assert.Contains("攻撃", json); // 日本語をエスケープしない
    }

    [Fact]
    public void Parse_InvalidItems_RejectedWithoutDroppingOthers()
    {
        // 不変条件を満たさない項目(名前の重複・null・スロット値の範囲外・nullの項目)は例外にせず除外し、他の項目は読み込む
        var json = "[" + string.Join(",",
            ValidItem,
            """{"skills":[{"name":"攻撃","lv":4},{"name":"攻撃","lv":2}],"armorSlots":[1,0,0],"weaponSlots":[0,0,0]}""",
            """{"skills":[{"name":null,"lv":3}],"armorSlots":[0,0,0],"weaponSlots":[0,0,0]}""",
            """{"skills":[{"name":"攻撃","lv":1}],"armorSlots":[9,0,0],"weaponSlots":[0,0,0]}""",
            "null",
            """{"skills":null,"armorSlots":[0,0,0],"weaponSlots":[0,0,0]}""",
            ValidItem) + "]";

        var result = CharmJsonSerializer.Parse(json);

        Assert.Equal(2, result.Charms.Count);
        Assert.Equal([1, 2, 3, 4, 5], result.Rejected.Select(r => r.Index).ToArray());
        Assert.All(result.Rejected, r => Assert.False(string.IsNullOrWhiteSpace(r.Reason)));
    }

    [Fact]
    public void Parse_RarityOutOfRange_NormalizedToNull()
    {
        // RAREの範囲外は情報を失わない補正としてnullにする(呼び出し側で推定し直す)
        var json = """[{"skills":[{"name":"攻撃","lv":1}],"armorSlots":[1,0,0],"weaponSlots":[0,0,0],"rarity":12}]""";
        var charm = Assert.Single(CharmJsonSerializer.Parse(json).Charms);
        Assert.Null(charm.Rarity);
    }

    [Fact]
    public void Parse_ShortSlotLists_PaddedAndMissingSourceDefaultsToCsvImport()
    {
        var json = """[{"skills":[{"name":"攻撃","lv":1}],"armorSlots":[1]}]""";
        var charm = Assert.Single(CharmJsonSerializer.Parse(json).Charms);
        Assert.Equal([1, 0, 0], charm.ArmorSlots);
        Assert.Equal([0, 0, 0], charm.WeaponSlots);
        Assert.Equal(CharmSource.CsvImport, charm.Source);
    }

    [Fact]
    public void Parse_RootNullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(CharmJsonSerializer.Parse("null").Charms);
        Assert.Empty(CharmJsonSerializer.Parse("[]").Charms);
    }

    [Fact]
    public void Parse_NotJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => CharmJsonSerializer.Parse("{ this is not json"));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CharmJsonSerializer.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => CharmJsonSerializer.Serialize(null!));
    }
}
