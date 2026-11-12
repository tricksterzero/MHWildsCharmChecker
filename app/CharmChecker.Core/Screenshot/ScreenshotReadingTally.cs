using CharmChecker.Core.Model;

namespace CharmChecker.Core.Screenshot;

/// <summary>error.logに書く1件。<see cref="Exception"/>があるときは例外の内容も書く。</summary>
public sealed record ScreenshotLogEntry(string Message, Exception? Exception = null);

/// <summary>
/// スクショ一括読み取りの結果(<see cref="ScreenshotCharmReader.ProcessAllAsync"/>の戻り値)の集計。副作用なし。
/// error.logへの書き込みは呼び出し側が<see cref="LogEntries"/>をファイル順に書く(ErrorLoggerはスレッドセーフではなく、
/// error.logの記録順・一覧への追加順もファイル順に保つため)。2026-11-12にMainWindowから移した(文言と数え方は変えていない)。
/// </summary>
public sealed class ScreenshotReadingTally
{
    private enum Category { None, Detected, Failed, DecorationSkipped, InconsistentSkipped, PanelMismatchSkipped }

    /// <summary>読み取りの対象にしたファイルの数。</summary>
    public int TargetCount { get; }

    /// <summary>処理したファイルの数(中断で処理しなかったものを除く)。</summary>
    public int Processed { get; }

    /// <summary>護石を検出したファイルの護石とファイル名(ファイル順)。</summary>
    public IReadOnlyList<(Charm Charm, string FileName)> Detected { get; }

    /// <summary>処理エラー(スキルなし・護石名なし・スロット検出失敗・例外)の数。</summary>
    public int Failed { get; }

    /// <summary>装飾品ありで除外した数。</summary>
    public int DecorationSkipped { get; }

    /// <summary>構成表に当てはまらないため除外した数。</summary>
    public int InconsistentSkipped { get; }

    /// <summary>スキル欄とスロット欄の位置が合わないため除外した数。</summary>
    public int PanelMismatchSkipped { get; }

    /// <summary>error.logに書く内容(ファイル順)。</summary>
    public IReadOnlyList<ScreenshotLogEntry> LogEntries { get; }

    /// <summary>中断により処理しなかったファイルがあるか。</summary>
    public bool Cancelled => Processed < TargetCount;

    private ScreenshotReadingTally(
        int targetCount, int processed, IReadOnlyList<(Charm, string)> detected, int failed,
        int decorationSkipped, int inconsistentSkipped, int panelMismatchSkipped, IReadOnlyList<ScreenshotLogEntry> logEntries)
    {
        TargetCount = targetCount;
        Processed = processed;
        Detected = detected;
        Failed = failed;
        DecorationSkipped = decorationSkipped;
        InconsistentSkipped = inconsistentSkipped;
        PanelMismatchSkipped = panelMismatchSkipped;
        LogEntries = logEntries;
    }

    /// <summary>
    /// 読み取り結果を集計する。<paramref name="outcomes"/>は<paramref name="files"/>と同じ順で、中断で処理しなかったファイルはnull。
    /// 事前条件: 引数はnullでなく件数が同じ。Detectedの結果は護石を、Errorの結果は例外を持つ。
    /// 想定していない種類の結果があればArgumentOutOfRangeExceptionを投げる(集計の対応の書き漏らしを黙って通さないため)。
    /// </summary>
    public static ScreenshotReadingTally Create(IReadOnlyList<string> files, IReadOnlyList<ScreenshotOutcome?> outcomes)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (files.Count != outcomes.Count)
            throw new ArgumentException($"filesとoutcomesの件数が違います({files.Count}件と{outcomes.Count}件)。", nameof(outcomes));

        var detected = new List<(Charm, string)>();
        var logEntries = new List<ScreenshotLogEntry>();
        int processed = 0, failed = 0, decorationSkipped = 0, inconsistentSkipped = 0, panelMismatchSkipped = 0;

        for (int i = 0; i < outcomes.Count; i++)
        {
            if (outcomes[i] is not { } outcome) continue;
            processed++;
            var fileName = Path.GetFileName(files[i]);

            var (category, entry) = Classify(outcome, fileName);
            if (entry is not null)
                logEntries.Add(entry);
            switch (category)
            {
                case Category.Detected: detected.Add((outcome.Charm!, fileName)); break;
                case Category.Failed: failed++; break;
                case Category.DecorationSkipped: decorationSkipped++; break;
                case Category.InconsistentSkipped: inconsistentSkipped++; break;
                case Category.PanelMismatchSkipped: panelMismatchSkipped++; break;
            }
        }

        return new ScreenshotReadingTally(files.Count, processed, detected, failed,
            decorationSkipped, inconsistentSkipped, panelMismatchSkipped, logEntries);
    }

    /// <summary>1件の結果を、数える区分とerror.logに書く内容(書かないならnull)に振り分ける(副作用なし)。</summary>
    private static (Category, ScreenshotLogEntry?) Classify(ScreenshotOutcome outcome, string fileName) => outcome.Kind switch
    {
        ScreenshotOutcomeKind.NotCharmPanel => (Category.None, null),
        ScreenshotOutcomeKind.Detected => outcome.Charm is null
            ? throw new ArgumentException($"{fileName}: Detectedの結果に護石がありません。", nameof(outcome))
            : (Category.Detected, null),
        // 護石パネル自体は検出できたがスキル名/Lvが1件も読み取れなかったケース。
        // 「対象外スクショだった」のか「護石だが読み取り失敗」なのかをログで区別できるようにする。
        ScreenshotOutcomeKind.NoValidSkill => (Category.Failed,
            new($"{fileName}: 護石パネルは検出できたがスキルを1件も読み取れませんでした。")),
        ScreenshotOutcomeKind.NoCharmName => (Category.Failed,
            new($"{fileName}: 護石名を読み取れず武器スロット有無が不明なため読み取りをスキップしました。")),
        // 装飾品装着済みスロットはレベル判定が信頼できないため(CLAUDE.md参照)、
        // 護石全体を読み取り対象から除外する。エラーではなく仕様上の除外のため
        // Failedとは別に数え、ユーザーに区別して提示する。
        ScreenshotOutcomeKind.DecorationEquipped => (Category.DecorationSkipped,
            new($"{fileName}: スロットに装飾品が装着されているため読み取りをスキップしました。")),
        ScreenshotOutcomeKind.SlotDetectionFailed => (Category.Failed,
            new($"{fileName}: スロットアイコンを検出できなかったため読み取りをスキップしました。")),
        // 実在しない構成の読み取り結果(スキルの読み落とし等)を保存しないための除外。
        // 装飾品装着と同じく、処理エラーとは別に数えてユーザーに区別して提示する。
        ScreenshotOutcomeKind.InconsistentWithCombinations => (Category.InconsistentSkipped,
            new($"{fileName}: 読み取ったスキル・スロットが護石の組み合わせ表のどれにも当てはまらないため、読み取りをスキップしました。")),
        // 2つの護石が混ざった結果(スキル欄とスロット欄が別のパネル)を保存しないための除外。
        // 組み合わせ表との照合と同じく、処理エラーとは別に数える。
        ScreenshotOutcomeKind.SlotPanelMismatch => (Category.PanelMismatchSkipped,
            new($"{fileName}: スキル欄とスロット欄が別の護石パネルのものと判定されたため、読み取りをスキップしました。")),
        ScreenshotOutcomeKind.Error => outcome.Error is null
            ? throw new ArgumentException($"{fileName}: Errorの結果に例外がありません。", nameof(outcome))
            : (Category.Failed, new(fileName, outcome.Error)),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind, "想定していない読み取り結果の種類です。"),
    };

    /// <summary>
    /// 読み取り画面に出す集計文。中断したときは処理した枚数、完了したときはフォルダ内の画像の枚数と対象の枚数を示し、
    /// 0件でない区分だけを後ろに足す。
    /// 事前条件: allFilesCount(フォルダ内の画像の枚数)はTargetCount以上。
    /// </summary>
    public string SummaryText(int allFilesCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(allFilesCount, TargetCount);

        var summary = Cancelled
            ? $"{Processed} / {TargetCount}枚まで処理（中断）、{Detected.Count}枚から護石を検出"
            : $"{allFilesCount}枚中 新規{TargetCount}枚を処理、{Detected.Count}枚から護石を検出";
        if (Failed > 0)
            summary += $"（{Failed}枚で処理エラー）";
        if (DecorationSkipped > 0)
            summary += $"（{DecorationSkipped}枚は装飾品装着のため除外）";
        if (InconsistentSkipped > 0)
            summary += $"（{InconsistentSkipped}枚は読み取り結果が組み合わせ表に当てはまらないため除外）";
        if (PanelMismatchSkipped > 0)
            summary += $"（{PanelMismatchSkipped}枚はスキル欄とスロット欄の位置が合わないため除外）";
        return summary;
    }
}
