using CharmChecker.Core.Skill;

namespace CharmChecker.Tests;

public class LvParserTests
{
    [Theory]
    [InlineData("Lv3", 3)]
    [InlineData("LV1", 1)]
    [InlineData("Lv10", 10)]
    public void StandardFormats(string input, int expected)
    {
        Assert.Equal(expected, LvParser.Parse(input));
    }

    [Theory]
    [InlineData("しV2", 2)]
    [InlineData("lv3", 3)]
    [InlineData("LVI", 1)]
    [InlineData("Lvー3", 3)]
    public void OcrMisreadCorrection(string input, int expected)
    {
        Assert.Equal(expected, LvParser.Parse(input));
    }

    [Theory]
    [InlineData("攻撃")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("LV")]
    [InlineData("LVSKILL2")]
    [InlineData("LV2147483648")]
    [InlineData("LV１")]
    public void InvalidReturnsNull(string input)
    {
        Assert.Null(LvParser.Parse(input));
    }

    [Theory]
    [InlineData("LV1個2", 1)]
    [InlineData("LV1/2", 1)]
    public void TrailingGarbage_OnlyLeadingDigitsUsed(string input, int expected)
    {
        Assert.Equal(expected, LvParser.Parse(input));
    }
}
