using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

public class CharmTypeLoaderTests
{
    private static string FindJsonPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "resources", "charm-types.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("charm-types.json が見つかりません。");
    }

    [Fact]
    public void Load_Returns4Types()
    {
        var types = CharmTypeLoader.Load(FindJsonPath());
        Assert.Equal(4, types.Count);
    }

    [Theory]
    [InlineData("未解の護石", 5, false)]
    [InlineData("史伝の護石", 6, false)]
    [InlineData("秘歴の護石", 7, false)]
    [InlineData("栄世の護石", 8, true)]
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
