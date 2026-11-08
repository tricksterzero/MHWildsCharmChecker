using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharmChecker.Core.Model;

/// <summary>JSONの1項目を護石にできなかったときの記録。Indexは配列の0始まりの位置。</summary>
public sealed record CharmJsonRejection(int Index, string Reason);

/// <summary>
/// JSONの読み込み結果。Charmsは不変条件を満たした護石(元の順)、Rejectedは満たさず除外した項目。
/// </summary>
public sealed record CharmJsonParseResult(IReadOnlyList<Charm> Charms, IReadOnlyList<CharmJsonRejection> Rejected);

/// <summary>
/// charms.json(と、データ管理の書き出し・取り込みのJSON)の形式と護石の相互変換。副作用なし。
/// 2026-11-08にMainWindowから移した(形式は変えていない)。
/// </summary>
public static class CharmJsonSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// JSONを護石の一覧にする。JSONとして読めなければ<see cref="JsonException"/>を投げる。
    /// 項目ごとに<see cref="Charm"/>の不変条件を満たさなければ、例外にせずRejectedへ入れて続ける
    /// (既存データを黙って捨てないよう、呼び出し側が件数を知らせ、元のファイルを残せるようにするため)。
    /// 情報を失わない補正だけは行う: RAREが5〜8の範囲外ならnullにする(呼び出し側で推定し直す)。
    /// スロットの個数が3未満なら0で埋める(<see cref="Charm"/>の正規化)。
    /// 事前条件: jsonはnullでない。
    /// </summary>
    public static CharmJsonParseResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var items = JsonSerializer.Deserialize<List<CharmJson?>>(json) ?? [];
        var charms = new List<Charm>();
        var rejected = new List<CharmJsonRejection>();
        for (int i = 0; i < items.Count; i++)
        {
            var error = TryConvert(items[i], out var charm);
            if (charm is not null)
                charms.Add(charm);
            else
                rejected.Add(new CharmJsonRejection(i, error!));
        }
        return new CharmJsonParseResult(charms, rejected);
    }

    /// <summary>護石の一覧をJSON文字列にする。事前条件: charmsはnullでない。</summary>
    public static string Serialize(IEnumerable<Charm> charms)
    {
        ArgumentNullException.ThrowIfNull(charms);

        var items = charms.Select(c => new CharmJson
        {
            Skills = c.Skills.Select(s => (SkillJson?)new SkillJson { Name = s.Name, Lv = s.Lv }).ToList(),
            ArmorSlots = c.ArmorSlots.ToList(),
            WeaponSlots = c.WeaponSlots.ToList(),
            Rarity = c.Rarity,
            Source = c.Source.ToString(),
            SourceTimestamp = c.SourceTimestamp,
            Version = c.Version.ToString(),
        }).ToList();
        return JsonSerializer.Serialize(items, WriteOptions);
    }

    /// <summary>1項目を護石にする。できればnull(エラーなし)を返してcharmを設定し、できなければ理由を返す。</summary>
    private static string? TryConvert(CharmJson? item, out Charm? charm)
    {
        charm = null;
        if (item is null)
            return "項目がnullです。";
        if (item.Skills is null || item.Skills.Any(s => s is null))
            return "skillsがnullか、nullの要素を含みます。";
        if (item.ArmorSlots is null || item.WeaponSlots is null)
            return "armorSlotsまたはweaponSlotsがnullです。";

        try
        {
            charm = new Charm
            {
                Skills = item.Skills.Select(s => new CharmSkill(s!.Name, s.Lv)).ToList(),
                ArmorSlots = item.ArmorSlots,
                WeaponSlots = item.WeaponSlots,
                Rarity = Charm.IsValidRarity(item.Rarity) ? item.Rarity : null,
                Source = Enum.TryParse<CharmSource>(item.Source, out var src) ? src : CharmSource.CsvImport,
                SourceTimestamp = item.SourceTimestamp,
                Version = Enum.TryParse<GameVersion>(item.Version, out var ver) ? ver : GameVersion.Wilds,
            };
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    private sealed class CharmJson
    {
        [JsonPropertyName("skills")]
        public List<SkillJson?>? Skills { get; set; } = [];
        [JsonPropertyName("armorSlots")]
        public List<int>? ArmorSlots { get; set; } = [];
        [JsonPropertyName("weaponSlots")]
        public List<int>? WeaponSlots { get; set; } = [];
        [JsonPropertyName("rarity")]
        public int? Rarity { get; set; }
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        [JsonPropertyName("sourceTimestamp")]
        public DateTime SourceTimestamp { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
    }

    private sealed class SkillJson
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("lv")]
        public int Lv { get; set; }
    }
}
