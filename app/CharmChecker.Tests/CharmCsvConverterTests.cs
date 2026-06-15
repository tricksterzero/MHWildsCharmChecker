using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

public class CharmCsvConverterTests
{
    [Fact]
    public void ToLine_FullCharm_Produces12Columns()
    {
        var charm = new Charm
        {
            Skills = [new("攻撃", 4), new("見切り", 3), new("弱点特効", 1)],
            ArmorSlots = [3, 2, 0],
            WeaponSlots = [1, 0, 0],
        };

        var line = CharmCsvConverter.ToLine(charm);
        var cols = line.Split(',');

        Assert.Equal(12, cols.Length);
        Assert.Equal("攻撃,4,見切り,3,弱点特効,1,3,2,0,1,0,0", line);
    }

    [Fact]
    public void ToLine_FewerSkills_PadsWithEmpty()
    {
        var charm = new Charm
        {
            Skills = [new("攻撃", 2)],
            ArmorSlots = [1, 0, 0],
            WeaponSlots = [0, 0, 0],
        };

        var line = CharmCsvConverter.ToLine(charm);
        Assert.Equal("攻撃,2,,0,,0,1,0,0,0,0,0", line);
    }

    [Fact]
    public void ToLine_NoSkills_AllZero()
    {
        var charm = new Charm();

        var line = CharmCsvConverter.ToLine(charm);
        Assert.Equal(",0,,0,,0,0,0,0,0,0,0", line);
    }

    [Fact]
    public void ParseLine_FullCharm_ReturnsCorrectModel()
    {
        var line = "攻撃,4,見切り,3,弱点特効,1,3,2,0,1,0,0";
        var charm = CharmCsvConverter.ParseLine(line);

        Assert.Equal(3, charm.Skills.Count);
        Assert.Equal(new CharmSkill("攻撃", 4), charm.Skills[0]);
        Assert.Equal(new CharmSkill("見切り", 3), charm.Skills[1]);
        Assert.Equal(new CharmSkill("弱点特効", 1), charm.Skills[2]);
        Assert.Equal([3, 2, 0], charm.ArmorSlots);
        Assert.Equal([1, 0, 0], charm.WeaponSlots);
        Assert.Equal(CharmSource.CsvImport, charm.Source);
    }

    [Fact]
    public void ParseLine_EmptySkills_SkipsEmpties()
    {
        var line = "攻撃,2,,0,,0,1,0,0,0,0,0";
        var charm = CharmCsvConverter.ParseLine(line);

        Assert.Single(charm.Skills);
        Assert.Equal(new CharmSkill("攻撃", 2), charm.Skills[0]);
    }

    [Fact]
    public void ParseLine_WrongColumnCount_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => CharmCsvConverter.ParseLine("a,b,c"));
    }

    [Fact]
    public void Roundtrip_PreservesData()
    {
        var original = new Charm
        {
            Skills = [new("攻撃", 4), new("見切り", 3)],
            ArmorSlots = [3, 1, 0],
            WeaponSlots = [2, 0, 0],
        };

        var line = CharmCsvConverter.ToLine(original);
        var restored = CharmCsvConverter.ParseLine(line);

        Assert.Equal(original.Skills, restored.Skills);
        Assert.Equal(original.ArmorSlots, restored.ArmorSlots);
        Assert.Equal(original.WeaponSlots, restored.WeaponSlots);
    }

    [Fact]
    public void ParseText_MultipleLines_ReturnsAll()
    {
        var text = "攻撃,4,見切り,3,,0,3,2,0,1,0,0\n弱点特効,2,,0,,0,0,0,0,0,0,0";
        var charms = CharmCsvConverter.ParseText(text);

        Assert.Equal(2, charms.Count);
        Assert.Equal(2, charms[0].Skills.Count);
        Assert.Single(charms[1].Skills);
    }

    [Fact]
    public void ParseText_InvalidLine_ThrowsWithLineNumber()
    {
        var text = "攻撃,4,見切り,3,,0,3,2,0,1,0,0\nbad,line";
        var ex = Assert.Throws<FormatException>(() => CharmCsvConverter.ParseText(text));
        Assert.Contains("行 2", ex.Message);
    }

    [Fact]
    public void ToText_MultipleCharms_JoinsWithNewline()
    {
        var charms = new[]
        {
            new Charm { Skills = [new("攻撃", 4)], ArmorSlots = [3, 0, 0], WeaponSlots = [0, 0, 0] },
            new Charm { Skills = [new("見切り", 2)], ArmorSlots = [1, 0, 0], WeaponSlots = [0, 0, 0] },
        };

        var text = CharmCsvConverter.ToText(charms);
        var lines = text.Split(Environment.NewLine);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("攻撃,4", lines[0]);
        Assert.StartsWith("見切り,2", lines[1]);
    }
}
