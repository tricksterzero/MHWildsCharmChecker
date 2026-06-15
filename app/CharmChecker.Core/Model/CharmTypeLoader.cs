using System.Text.Json;

namespace CharmChecker.Core.Model;

public record CharmTypeInfo(string Name, int Rarity, bool HasWeaponSlot);

public static class CharmTypeLoader
{
    private static IReadOnlyList<CharmTypeInfo>? _cached;

    public static IReadOnlyList<CharmTypeInfo> LoadFromEmbeddedResource()
    {
        if (_cached is not null) return _cached;

        var asm = typeof(CharmTypeLoader).Assembly;
        using var stream = asm.GetManifestResourceStream("CharmChecker.Core.Resources.charm-types.json")
            ?? throw new InvalidOperationException("埋め込みリソース 'charm-types.json' が見つかりません。");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        _cached = ParseJson(json);
        return _cached;
    }

    public static IReadOnlyList<CharmTypeInfo> Load(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        return ParseJson(json);
    }

    /// <summary>
    /// OCRで抽出された護石名（先頭にゴミ文字が付く場合あり）から、既知の護石タイプを末尾一致で検索する。
    /// </summary>
    public static CharmTypeInfo? Lookup(string? ocrCharmName, IReadOnlyList<CharmTypeInfo> charmTypes)
    {
        if (ocrCharmName is null) return null;
        foreach (var ct in charmTypes)
        {
            if (ocrCharmName.EndsWith(ct.Name, StringComparison.Ordinal))
                return ct;
        }
        return null;
    }

    private static IReadOnlyList<CharmTypeInfo> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<CharmTypeInfo>();
        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var name = elem.GetProperty("name").GetString()!;
            var rarity = elem.GetProperty("rarity").GetInt32();
            var hasWeaponSlot = elem.GetProperty("hasWeaponSlot").GetBoolean();
            list.Add(new CharmTypeInfo(name, rarity, hasWeaponSlot));
        }
        return list;
    }
}
