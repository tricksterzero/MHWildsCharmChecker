using System.IO;
using System.Text.Json;

namespace CharmChecker.App;

public static class GameSkillOrder
{
    private static IReadOnlyList<string>? _cached;

    public static IReadOnlyList<string> Order
    {
        get
        {
            if (_cached is not null) return _cached;
            var asm = typeof(CharmChecker.Core.Skill.SkillNameLoader).Assembly;
            using var stream = asm.GetManifestResourceStream("CharmChecker.Core.Resources.skill-order.json")
                ?? throw new InvalidOperationException("埋め込みリソース 'skill-order.json' が見つかりません。");
            using var reader = new StreamReader(stream);
            _cached = JsonSerializer.Deserialize<List<string>>(reader.ReadToEnd()) ?? [];
            return _cached;
        }
    }
}
