using CharmChecker.Core.Skill;

namespace CharmChecker.Tests;

public class SkillNameLoaderTests
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
    public void Load_Returns121Skills()
    {
        var names = SkillNameLoader.Load(FindJsonPath());
        Assert.Equal(121, names.Count);
    }

    [Fact]
    public void Load_SortedByLengthDescending()
    {
        var names = SkillNameLoader.Load(FindJsonPath());
        for (int i = 1; i < names.Count; i++)
        {
            Assert.True(names[i - 1].Length >= names[i].Length,
                $"'{names[i - 1]}'({names[i - 1].Length}) の後に '{names[i]}'({names[i].Length}) があり、長さ降順になっていない");
        }
    }

    [Theory]
    [InlineData("属性吸収")]
    [InlineData("属性変換")]
    [InlineData("属性やられ耐性")]
    [InlineData("オトモへの采配")]
    public void Load_ContainsExtraSkills(string skillName)
    {
        var names = SkillNameLoader.Load(FindJsonPath());
        Assert.Contains(skillName, names);
    }
}
