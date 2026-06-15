using System.Text.Json;

namespace CharmChecker.Core.Skill;

public static class SkillNameLoader
{
    /// <summary>
    /// skill-decoration-map.json からスキル名一覧を読み込み、長い名前順で返す。
    /// </summary>
    public static IReadOnlyList<string> Load(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var names = new HashSet<string>();

        foreach (var deco in root.GetProperty("decorations").EnumerateArray())
        {
            foreach (var skill in deco.GetProperty("skills").EnumerateObject())
            {
                names.Add(skill.Name);
            }
        }

        if (root.TryGetProperty("extra_skills", out var extras))
        {
            foreach (var item in extras.EnumerateArray())
            {
                names.Add(item.GetString()!);
            }
        }

        return names.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).ToList();
    }
}
