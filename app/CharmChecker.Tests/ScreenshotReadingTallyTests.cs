using CharmChecker.Core.Model;
using CharmChecker.Core.Screenshot;

namespace CharmChecker.Tests;

/// <summary>
/// スクショ一括読み取りの結果の集計(ScreenshotReadingTally)のテスト。文言と数え方は、2026-11-12にMainWindowから移す前の
/// 画面の表示・error.logの記録と同じ。
/// </summary>
public class ScreenshotReadingTallyTests
{
    private static readonly Charm SampleCharm = new() { Skills = [new("攻撃", 1)] };

    private static ScreenshotOutcome Outcome(ScreenshotOutcomeKind kind) => kind switch
    {
        ScreenshotOutcomeKind.Detected => new(kind, SampleCharm),
        ScreenshotOutcomeKind.Error => new(kind, Error: new InvalidDataException("壊れた画像")),
        _ => new(kind),
    };

    private static string[] Files(int count) => Enumerable.Range(1, count).Select(i => $@"C:\shots\{i}.jpg").ToArray();

    [Fact]
    public void Create_EachKind_CountedAndLoggedInFileOrder()
    {
        ScreenshotOutcomeKind[] kinds =
        [
            ScreenshotOutcomeKind.NotCharmPanel,
            ScreenshotOutcomeKind.Detected,
            ScreenshotOutcomeKind.NoValidSkill,
            ScreenshotOutcomeKind.NoCharmName,
            ScreenshotOutcomeKind.DecorationEquipped,
            ScreenshotOutcomeKind.SlotDetectionFailed,
            ScreenshotOutcomeKind.InconsistentWithCombinations,
            ScreenshotOutcomeKind.SlotPanelMismatch,
            ScreenshotOutcomeKind.Error,
        ];
        var tally = ScreenshotReadingTally.Create(Files(kinds.Length), kinds.Select(Outcome).ToArray());

        Assert.Equal(9, tally.Processed);
        Assert.False(tally.Cancelled);
        var detected = Assert.Single(tally.Detected);
        Assert.Same(SampleCharm, detected.Charm);
        Assert.Equal("2.jpg", detected.FileName);
        Assert.Equal(4, tally.Failed); // スキルなし・護石名なし・スロット検出失敗・例外
        Assert.Equal(1, tally.DecorationSkipped);
        Assert.Equal(1, tally.InconsistentSkipped);
        Assert.Equal(1, tally.PanelMismatchSkipped);

        Assert.Equal(new[]
        {
            "3.jpg: 護石パネルは検出できたがスキルを1件も読み取れませんでした。",
            "4.jpg: 護石名を読み取れず武器スロット有無が不明なため読み取りをスキップしました。",
            "5.jpg: スロットに装飾品が装着されているため読み取りをスキップしました。",
            "6.jpg: スロットアイコンを検出できなかったため読み取りをスキップしました。",
            "7.jpg: 読み取ったスキル・スロットが護石の組み合わせ表のどれにも当てはまらないため、読み取りをスキップしました。",
            "8.jpg: スキル欄とスロット欄が別の護石パネルのものと判定されたため、読み取りをスキップしました。",
            "9.jpg",
        }, tally.LogEntries.Select(e => e.Message));
        Assert.All(tally.LogEntries.Take(6), e => Assert.Null(e.Exception));
        Assert.IsType<InvalidDataException>(tally.LogEntries[6].Exception);
    }

    [Fact]
    public void Create_EveryKindIsHandled()
    {
        // 結果の種類を増やしたときに集計の対応を書き漏らすと、ここで例外になる
        foreach (var kind in Enum.GetValues<ScreenshotOutcomeKind>())
            _ = ScreenshotReadingTally.Create(Files(1), [Outcome(kind)]);
    }

    [Fact]
    public void Create_UnprocessedFiles_MeanCancelled()
    {
        // 中断で処理しなかったファイルの結果はnull
        var tally = ScreenshotReadingTally.Create(Files(3), [Outcome(ScreenshotOutcomeKind.Detected), null, null]);
        Assert.Equal(1, tally.Processed);
        Assert.Equal(3, tally.TargetCount);
        Assert.True(tally.Cancelled);
    }

    [Fact]
    public void SummaryText_Cancelled()
    {
        var tally = ScreenshotReadingTally.Create(Files(4),
            [Outcome(ScreenshotOutcomeKind.Detected), Outcome(ScreenshotOutcomeKind.Error), Outcome(ScreenshotOutcomeKind.DecorationEquipped), null]);
        Assert.Equal("3 / 4枚まで処理（中断）、1枚から護石を検出（1枚で処理エラー）（1枚は装飾品装着のため除外）",
            tally.SummaryText(allFilesCount: 10));
    }

    [Fact]
    public void SummaryText_Completed_AllNotes()
    {
        var tally = ScreenshotReadingTally.Create(Files(5),
        [
            Outcome(ScreenshotOutcomeKind.Detected),
            Outcome(ScreenshotOutcomeKind.NoValidSkill),
            Outcome(ScreenshotOutcomeKind.DecorationEquipped),
            Outcome(ScreenshotOutcomeKind.InconsistentWithCombinations),
            Outcome(ScreenshotOutcomeKind.SlotPanelMismatch),
        ]);
        Assert.Equal("10枚中 新規5枚を処理、1枚から護石を検出（1枚で処理エラー）（1枚は装飾品装着のため除外）"
            + "（1枚は読み取り結果が組み合わせ表に当てはまらないため除外）（1枚はスキル欄とスロット欄の位置が合わないため除外）",
            tally.SummaryText(allFilesCount: 10));
    }

    [Fact]
    public void SummaryText_Completed_ZeroCountsOmitted()
    {
        var tally = ScreenshotReadingTally.Create(Files(2), [Outcome(ScreenshotOutcomeKind.NotCharmPanel), Outcome(ScreenshotOutcomeKind.Detected)]);
        Assert.Equal("2枚中 新規2枚を処理、1枚から護石を検出", tally.SummaryText(allFilesCount: 2));
    }

    [Fact]
    public void Create_Empty_NotCancelled()
    {
        // 境界値: 対象が0枚
        var tally = ScreenshotReadingTally.Create([], []);
        Assert.Equal(0, tally.Processed);
        Assert.False(tally.Cancelled);
        Assert.Empty(tally.LogEntries);
        Assert.Equal("0枚中 新規0枚を処理、0枚から護石を検出", tally.SummaryText(allFilesCount: 0));
    }

    [Fact]
    public void SummaryText_AllFilesFewerThanTargets_Throws()
    {
        var tally = ScreenshotReadingTally.Create(Files(2), [null, null]);
        Assert.Throws<ArgumentOutOfRangeException>(() => tally.SummaryText(allFilesCount: 1));
    }

    [Fact]
    public void Create_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ScreenshotReadingTally.Create(null!, []));
        Assert.Throws<ArgumentNullException>(() => ScreenshotReadingTally.Create([], null!));
        Assert.Throws<ArgumentException>(() => ScreenshotReadingTally.Create(Files(2), [null]));
        Assert.Throws<ArgumentException>(() => ScreenshotReadingTally.Create(Files(1), [new ScreenshotOutcome(ScreenshotOutcomeKind.Detected)]));
        Assert.Throws<ArgumentException>(() => ScreenshotReadingTally.Create(Files(1), [new ScreenshotOutcome(ScreenshotOutcomeKind.Error)]));
    }
}
