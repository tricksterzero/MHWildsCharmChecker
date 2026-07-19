using System.Text.Json;

namespace CharmChecker.Core.Skill;

public static class SkillNameLoader
{
    /// <summary>
    /// 埋め込みリソースの skill-decoration-map.json からスキル名一覧を読み込み、長い名前順で返す。
    /// </summary>
    public static IReadOnlyList<string> LoadFromEmbeddedResource()
    {
        var asm = typeof(SkillNameLoader).Assembly;
        using var stream = asm.GetManifestResourceStream("CharmChecker.Core.Resources.skill-decoration-map.json")
            ?? throw new InvalidOperationException("埋め込みリソース 'skill-decoration-map.json' が見つかりません。");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return ParseJson(json);
    }

    /// <summary>
    /// skill-decoration-map.json からスキル名一覧を読み込み、長い名前順で返す。
    /// </summary>
    public static IReadOnlyList<string> Load(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        return ParseJson(json);
    }

    private static IReadOnlyList<string> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var names = new HashSet<string>();

        foreach (var deco in root.GetProperty("decorations").EnumerateArray())
        {
            foreach (var skill in deco.GetProperty("skills").EnumerateObject())
            {
                AddSkillName(names, skill.Name);
            }
        }

        if (!root.TryGetProperty("extra_skills", out var extras))
            throw new InvalidOperationException("skill-decoration-map.json に 'extra_skills' プロパティがありません。");

        foreach (var item in extras.EnumerateArray())
        {
            AddSkillName(names, item.GetString());
        }

        return names.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).ToList();
    }

    private static void AddSkillName(HashSet<string> names, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("skill-decoration-map.json に無効なスキル名(null/空文字/空白)があります。");
        names.Add(name);
    }
}
