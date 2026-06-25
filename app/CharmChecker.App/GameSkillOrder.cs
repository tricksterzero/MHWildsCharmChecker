using System.IO;
using System.Text.Json;

namespace CharmChecker.App;

public static class GameSkillOrder
{
    private static readonly Lazy<IReadOnlyList<string>> _cached = new(() =>
    {
        var asm = typeof(CharmChecker.Core.Skill.SkillNameLoader).Assembly;
        using var stream = asm.GetManifestResourceStream("CharmChecker.Core.Resources.skill-order.json")
            ?? throw new InvalidOperationException("埋め込みリソース 'skill-order.json' が見つかりません。");
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<List<string>>(reader.ReadToEnd()) ?? [];
    });

    public static IReadOnlyList<string> Order => _cached.Value;
}
