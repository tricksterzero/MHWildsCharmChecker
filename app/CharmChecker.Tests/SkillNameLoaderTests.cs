using CharmChecker.Core.Skill;

namespace CharmChecker.Tests;

public class SkillNameLoaderTests
{
    private static string FindJsonPath() => Path.Combine(TestPaths.FindResourcesDir(), "skill-decoration-map.json");

    [Fact]
    public void Load_Returns120Skills()
    {
        var names = SkillNameLoader.Load(FindJsonPath());
        Assert.Equal(120, names.Count);
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
    public void Load_ContainsExtraSkills(string skillName)
    {
        var names = SkillNameLoader.Load(FindJsonPath());
        Assert.Contains(skillName, names);
    }

    [Fact]
    public void Load_DoesNotContainOtomoHaisai()
    {
        var names = SkillNameLoader.Load(FindJsonPath());
        Assert.DoesNotContain("オトモへの采配", names);
    }

    /// <summary>jsonを一時ファイルに書き、それを読むと<see cref="InvalidOperationException"/>になることを確かめる(一時ファイルは消す)。</summary>
    private static void AssertLoadThrows(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        try
        {
            Assert.Throws<InvalidOperationException>(() => SkillNameLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_EmptyDecorationSkillName_Throws()
    {
        AssertLoadThrows("""
            { "decorations": [ { "skills": { "": {} } } ], "extra_skills": [] }
            """);
    }

    [Fact]
    public void Load_NullExtraSkill_Throws()
    {
        AssertLoadThrows("""
            { "decorations": [], "extra_skills": [null] }
            """);
    }

    [Fact]
    public void Load_MissingExtraSkillsProperty_Throws()
    {
        AssertLoadThrows("""
            { "decorations": [] }
            """);
    }
}
