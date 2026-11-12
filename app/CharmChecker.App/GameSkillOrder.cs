using System.Text.Json;
using CharmChecker.Core;

namespace CharmChecker.App;

public static class GameSkillOrder
{
    private static readonly Lazy<IReadOnlyList<string>> _cached =
        new(() => ParseJson(EmbeddedResources.ReadText("skill-order.json")));

    public static IReadOnlyList<string> Order => _cached.Value;

    internal static IReadOnlyList<string> ParseJson(string json)
    {
        var names = JsonSerializer.Deserialize<List<string>>(json)
            ?? throw new InvalidOperationException("skill-order.json のルートがnullです。");

        var seen = new HashSet<string>();
        var duplicates = new List<string>();
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("skill-order.json に無効なスキル名(null/空文字/空白)があります。");
            if (!seen.Add(name))
                duplicates.Add(name);
        }
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"skill-order.json にスキル名の重複があります: {string.Join(", ", duplicates)}");

        return names;
    }
}
