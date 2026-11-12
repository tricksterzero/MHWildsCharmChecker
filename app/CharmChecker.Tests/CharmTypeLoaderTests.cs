using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

public class CharmTypeLoaderTests
{
    private static string FindJsonPath() => Path.Combine(TestPaths.FindResourcesDir(), "charm-types.json");

    [Fact]
    public void Load_Returns5Types()
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        Assert.Equal(5, types.Count);
    }

    // 希望の護石(予約特典、研鑽Lv1+体力回復量UP Lv1、スロットなし、RARE5)は2026-11-12に追加。スクショの読み取りは
    // 当てはまらない護石名を除外するため、手元の画像で当てはまらなかった唯一の護石名を加えた
    [Theory]
    [InlineData("未解の護石", 5, false)]
    [InlineData("史伝の護石", 6, false)]
    [InlineData("秘歴の護石", 7, false)]
    [InlineData("栄世の護石", 8, true)]
    [InlineData("希望の護石", 5, false)]
    public void Load_ContainsKnownTypes(string name, int rarity, bool hasWeaponSlot)
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        Assert.Contains(types, t => t.Name == name && t.Rarity == rarity && t.HasWeaponSlot == hasWeaponSlot);
    }

    [Fact]
    public void Lookup_ExactName_ReturnsMatch()
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        var result = CharmTypeLoader.Lookup("栄世の護石", types);
        Assert.NotNull(result);
        Assert.Equal("栄世の護石", result.Name);
    }

    [Fact]
    public void Lookup_LeadingGarbage_MatchesByEndsWith()
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        var result = CharmTypeLoader.Lookup("x栄世の護石", types);
        Assert.NotNull(result);
        Assert.Equal("栄世の護石", result.Name);
    }

    [Fact]
    public void Lookup_UnknownName_ReturnsNull()
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        Assert.Null(CharmTypeLoader.Lookup("護石", types));
    }

    // 護石名の本体の読み違いは当てはまらない(スクショの読み取りはこれを除外する。武器スロット無しとして読み進めると、
    // 栄世の護石の武器スロットが防具スロットとして保存されるため。2026-11-12)
    [Theory]
    [InlineData("栄生の護石")]
    [InlineData("栄世の獲石")]
    public void Lookup_MisreadBody_ReturnsNull(string ocrCharmName)
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        Assert.Null(CharmTypeLoader.Lookup(ocrCharmName, types));
    }

    [Fact]
    public void Lookup_NullName_ReturnsNull()
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        Assert.Null(CharmTypeLoader.Lookup(null, types));
    }

    [Fact]
    public void ParseJson_EmptyName_Throws()
    {
        var json = """
            [ { "name": "", "rarity": 5, "hasWeaponSlot": false } ]
            """;
        Assert.Throws<InvalidOperationException>(() => CharmTypeLoader.ParseJson(json));
    }

    [Fact]
    public void ParseJson_DuplicateName_Throws()
    {
        var json = """
            [
                { "name": "栄世の護石", "rarity": 8, "hasWeaponSlot": true },
                { "name": "栄世の護石", "rarity": 7, "hasWeaponSlot": false }
            ]
            """;
        Assert.Throws<InvalidOperationException>(() => CharmTypeLoader.ParseJson(json));
    }
}
