namespace CharmChecker.Tests;

public class MainWindowTests
{
    [Fact]
    public void FormatProbability_Null_ReturnsCannotCompute()
    {
        Assert.Equal("算出不可", CharmChecker.App.MainWindow.FormatProbability(null));
    }

    [Fact]
    public void FormatProbability_Zero_ReturnsCannotCompute()
    {
        Assert.Equal("算出不可", CharmChecker.App.MainWindow.FormatProbability(0));
    }

    [Theory]
    [InlineData(0.09996, "約10.0%")]
    [InlineData(0.9996, "約100%")]
    public void FormatProbability_RoundingCarriesOverMagnitude_KeepsThreeSignificantDigits(
        double probability, string expected)
    {
        // 丸め処理で桁上がりする場合(例: 9.996% → 単純に小数点2桁で丸めると10.00%になり
        // 有効数字4桁になってしまう)、桁上がり後の桁数で小数桁数を再計算する回帰テスト
        Assert.Equal(expected, CharmChecker.App.MainWindow.FormatProbability(probability));
    }

    [Fact]
    public void FormatProbability_TypicalValue_ThreeSignificantDigits()
    {
        Assert.Equal("約0.0156%", CharmChecker.App.MainWindow.FormatProbability(0.000156));
    }
}
