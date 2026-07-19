using CharmChecker.App;
using CharmChecker.Core.Skill;

namespace CharmChecker.Tests;

public class GameSkillOrderTests
{
    private static string FindJsonPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "resources", "skill-decoration-map.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("skill-decoration-map.json が見つかりません。");
    }

    [Fact]
    public void Order_MatchesNormalizedSkillNamesExactly()
    {
        var normalizedNames = new HashSet<string>(SkillNameLoader.Load(FindJsonPath()));
        var order = GameSkillOrder.Order;

        Assert.Equal(normalizedNames.Count, order.Count);
        foreach (var name in order)
            Assert.Contains(name, normalizedNames);
    }

    [Fact]
    public void Order_HasNoDuplicates()
    {
        var order = GameSkillOrder.Order;
        Assert.Equal(order.Count, order.Distinct().Count());
    }

    [Fact]
    public void ParseJson_NullRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GameSkillOrder.ParseJson("null"));
    }

    [Fact]
    public void ParseJson_EmptyName_Throws()
    {
        var json = """["攻撃", ""]""";
        Assert.Throws<InvalidOperationException>(() => GameSkillOrder.ParseJson(json));
    }

    [Fact]
    public void ParseJson_WhitespaceName_Throws()
    {
        var json = """["攻撃", "  "]""";
        Assert.Throws<InvalidOperationException>(() => GameSkillOrder.ParseJson(json));
    }

    [Fact]
    public void ParseJson_DuplicateName_Throws()
    {
        var json = """["攻撃", "攻撃"]""";
        Assert.Throws<InvalidOperationException>(() => GameSkillOrder.ParseJson(json));
    }

    [Fact]
    public void ParseJson_ValidNames_PreservesOrder()
    {
        var json = """["攻撃", "防御", "見切り"]""";
        var result = GameSkillOrder.ParseJson(json);
        Assert.Equal(["攻撃", "防御", "見切り"], result);
    }
}
