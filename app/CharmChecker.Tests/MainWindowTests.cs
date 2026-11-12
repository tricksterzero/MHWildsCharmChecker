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

    // CSVの取り込み結果の末尾に付ける警告の文(2026-11-12にExecuteImport_Clickから切り出した。文言は変えていない)
    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "（1件にスキル候補一覧にないスキル名があります）")]
    [InlineData(12, "（12件にスキル候補一覧にないスキル名があります）")]
    public void CsvImportWarningNote_ByCount(int warningCount, string expected)
    {
        Assert.Equal(expected, CharmChecker.App.MainWindow.CsvImportWarningNote(warningCount));
    }

    [Fact]
    public void CsvImportWarningNote_NegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CharmChecker.App.MainWindow.CsvImportWarningNote(-1));
    }

    // 読み取り結果を一覧に追加したときの文(2026-11-12、同じスクショから取り込み済みの護石を追加しないようにしたときに追加)
    [Theory]
    [InlineData(3, 0, "\n3件を護石一覧に追加しました。")]
    [InlineData(0, 0, "\n0件を護石一覧に追加しました。")]
    [InlineData(1, 5, "\n1件を護石一覧に追加しました。（同じスクショから取り込み済みの5件は追加していません）")]
    [InlineData(0, 2, "\n0件を護石一覧に追加しました。（同じスクショから取り込み済みの2件は追加していません）")]
    public void ReadingResultAddedNote_ByCounts(int added, int skipped, string expected)
    {
        Assert.Equal(expected, CharmChecker.App.MainWindow.ReadingResultAddedNote(added, skipped));
    }

    [Fact]
    public void ReadingResultAddedNote_NegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CharmChecker.App.MainWindow.ReadingResultAddedNote(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CharmChecker.App.MainWindow.ReadingResultAddedNote(0, -1));
    }

    // JSONの取り込みの確認ダイアログに書く補足(2026-11-12にImportLocalDataMenu_Clickから切り出した。文言は変えていない)
    [Fact]
    public void JsonImportNotes_NothingExcludedNoWarning_ReturnsEmpty()
    {
        Assert.Equal("", CharmChecker.App.MainWindow.JsonImportNotes([], 0));
    }

    [Fact]
    public void JsonImportNotes_Excluded_ShowsCountAndFirstReason()
    {
        Assert.Equal(
            "\n不正な形式のため取り込まない: 2件（例: 1件目: 理由A。理由は error.log に記録しました）",
            CharmChecker.App.MainWindow.JsonImportNotes(["1件目: 理由A", "3件目: 理由B"], 0));
    }

    [Fact]
    public void JsonImportNotes_Warning_ShowsCount()
    {
        Assert.Equal(
            "\nスキル候補一覧にないスキル名を含む: 3件（取り込みます）",
            CharmChecker.App.MainWindow.JsonImportNotes([], 3));
    }

    [Fact]
    public void JsonImportNotes_ExcludedAndWarning_ExcludedComesFirst()
    {
        Assert.Equal(
            "\n不正な形式のため取り込まない: 1件（例: 1件目: 理由A。理由は error.log に記録しました）"
            + "\nスキル候補一覧にないスキル名を含む: 1件（取り込みます）",
            CharmChecker.App.MainWindow.JsonImportNotes(["1件目: 理由A"], 1));
    }

    [Fact]
    public void JsonImportNotes_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => CharmChecker.App.MainWindow.JsonImportNotes(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CharmChecker.App.MainWindow.JsonImportNotes([], -1));
    }
}
