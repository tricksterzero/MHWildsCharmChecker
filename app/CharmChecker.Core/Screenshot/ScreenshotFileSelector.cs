using CharmChecker.Core.Model;

namespace CharmChecker.Core.Screenshot;

/// <summary>スクショ読み取りの対象ファイルを選ぶ(差分読み込み)。</summary>
public static class ScreenshotFileSelector
{
    /// <summary>
    /// 読み取りの対象を、ファイルの更新日時の古い順(同じ日時は渡された順)に返す(副作用なし)。
    /// <paramref name="rescanAll"/>がfalseなら、取り込み済みのスクショ由来の護石の最新日時より新しいファイルだけを返す
    /// (ウォーターマーク方式。失敗したファイルより後の日時のファイルが1つでも取り込まれると、失敗したファイルは
    /// 以後対象外になるため、全件を読み直す<paramref name="rescanAll"/>を用意している)。
    /// 事前条件: 引数はnullでない。
    /// </summary>
    public static List<string> SelectTargets(
        IReadOnlyList<(string Path, DateTime LastWriteTime)> files, IEnumerable<Charm> existingCharms, bool rescanAll)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(existingCharms);

        var latestScreenshot = rescanAll
            ? DateTime.MinValue
            : existingCharms
                .Where(c => c.Source == CharmSource.Screenshot)
                .Select(c => c.SourceTimestamp)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

        return files
            .Where(f => rescanAll || f.LastWriteTime > latestScreenshot)
            .OrderBy(f => f.LastWriteTime)
            .Select(f => f.Path)
            .ToList();
    }
}
