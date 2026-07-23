namespace CharmChecker.Core.Model;

public static class CharmCsvConverter
{
    private const int ColumnCount = 12;
    private const int MaxSkills = 3;
    private const int MaxSlots = 3;

    public static string ToLine(Charm charm)
    {
        var cols = new string[ColumnCount];

        for (int i = 0; i < MaxSkills; i++)
        {
            if (i < charm.Skills.Count && charm.Skills[i].Name != "")
            {
                cols[i * 2] = NeutralizeCsvFormula(charm.Skills[i].Name);
                cols[i * 2 + 1] = charm.Skills[i].Lv.ToString();
            }
            else
            {
                cols[i * 2] = "";
                cols[i * 2 + 1] = "0";
            }
        }

        for (int i = 0; i < MaxSlots; i++)
        {
            cols[6 + i] = i < charm.ArmorSlots.Count ? charm.ArmorSlots[i].ToString() : "0";
            cols[9 + i] = i < charm.WeaponSlots.Count ? charm.WeaponSlots[i].ToString() : "0";
        }

        return string.Join(",", cols);
    }

    /// <summary>
    /// CSVインジェクション対策: スキル名の先頭が '='/'+'/'-'/'@'/タブ/CRの場合、
    /// Excel等のスプレッドシートアプリが数式として解釈しないよう先頭にアポストロフィを付与する。
    /// nullは（string.Joinがnull要素を空文字として扱う既存挙動と同じ結果になるよう）空文字を返す。
    /// </summary>
    private static string NeutralizeCsvFormula(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var first = value[0];
        if (first is '=' or '+' or '-' or '@' or '\t' or '\r')
            return "'" + value;

        return value;
    }

    public static Charm ParseLine(string line)
    {
        var cols = line.Split(',');
        if (cols.Length != ColumnCount)
            throw new FormatException($"列数が{ColumnCount}ではありません（{cols.Length}列）: {line}");

        var skills = new List<CharmSkill>();
        for (int i = 0; i < MaxSkills; i++)
        {
            var name = cols[i * 2].Trim();
            var lv = ParseInt(cols[i * 2 + 1].Trim(), line);
            if (name != "" && lv > 0)
            {
                skills.Add(new CharmSkill(name, lv));
            }
            else if (!(name == "" && lv == 0))
            {
                throw new FormatException(
                    $"スキル{i + 1}の名前とLvが不整合です(名前「{name}」,Lv{lv})。" +
                    $"空き欄は名前・Lvともに空欄/0にしてください: {line}");
            }
        }

        var armorSlots = new List<int>();
        for (int i = 0; i < MaxSlots; i++)
            armorSlots.Add(ParseSlotValue(cols[6 + i].Trim(), line));

        var weaponSlots = new List<int>();
        for (int i = 0; i < MaxSlots; i++)
            weaponSlots.Add(ParseSlotValue(cols[9 + i].Trim(), line));

        return new Charm
        {
            Skills = skills,
            ArmorSlots = armorSlots,
            WeaponSlots = weaponSlots,
            Source = CharmSource.CsvImport,
            SourceTimestamp = DateTime.Now,
        };
    }

    public static List<Charm> ParseText(string text)
    {
        var charms = new List<Charm>();
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            try
            {
                charms.Add(ParseLine(lines[i]));
            }
            catch (FormatException ex)
            {
                throw new FormatException($"行 {i + 1}: {ex.Message}", ex);
            }
        }
        return charms;
    }

    private static int ParseInt(string value, string line)
    {
        if (!int.TryParse(value, out var result))
            throw new FormatException($"数値に変換できません「{value}」: {line}");
        return result;
    }

    private static int ParseSlotValue(string value, string line)
    {
        var result = ParseInt(value, line);
        if (result < 0 || result > 3)
            throw new FormatException($"スロット値は0〜3である必要があります「{result}」: {line}");
        return result;
    }

    public static string ToText(IEnumerable<Charm> charms)
    {
        return string.Join(Environment.NewLine, charms.Select(ToLine));
    }
}
