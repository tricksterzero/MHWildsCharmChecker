using CharmChecker.Core.Model;
using CharmChecker.Core.Screenshot;

namespace CharmChecker.Tests;

/// <summary>スクショ読み取りの対象ファイルの選び方(差分読み込み)のテスト。</summary>
public class ScreenshotFileSelectorTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0);

    private static Charm ScreenshotCharm(DateTime timestamp, CharmSource source = CharmSource.Screenshot) =>
        new() { Skills = [new CharmSkill("攻撃", 1)], ArmorSlots = [1, 0, 0], WeaponSlots = [0, 0, 0], Source = source, SourceTimestamp = timestamp };

    private static readonly (string, DateTime)[] Files =
    [
        ("c.jpg", T0.AddMinutes(2)),
        ("a.jpg", T0),
        ("b.jpg", T0.AddMinutes(1)),
    ];

    [Fact]
    public void NoExistingCharms_ReturnsAllSortedByTime()
    {
        Assert.Equal(["a.jpg", "b.jpg", "c.jpg"], ScreenshotFileSelector.SelectTargets(Files, [], rescanAll: false));
    }

    [Fact]
    public void ExistingScreenshotCharm_ReturnsOnlyNewerFiles()
    {
        // 境界値: 取り込み済みの最新日時と同じ日時のファイルは対象外(より新しいものだけ)
        var existing = new[] { ScreenshotCharm(T0.AddMinutes(1)) };
        Assert.Equal(["c.jpg"], ScreenshotFileSelector.SelectTargets(Files, existing, rescanAll: false));
    }

    [Fact]
    public void NonScreenshotCharms_AreIgnoredForWatermark()
    {
        var existing = new[] { ScreenshotCharm(T0.AddMinutes(5), CharmSource.CsvImport) };
        Assert.Equal(3, ScreenshotFileSelector.SelectTargets(Files, existing, rescanAll: false).Count);
    }

    [Fact]
    public void RescanAll_ReturnsAllEvenIfOlder()
    {
        var existing = new[] { ScreenshotCharm(T0.AddMinutes(5)) };
        Assert.Equal(["a.jpg", "b.jpg", "c.jpg"], ScreenshotFileSelector.SelectTargets(Files, existing, rescanAll: true));
    }

    [Fact]
    public void SameTimestamp_KeepsInputOrder()
    {
        (string, DateTime)[] files = [("y.jpg", T0), ("x.jpg", T0)];
        Assert.Equal(["y.jpg", "x.jpg"], ScreenshotFileSelector.SelectTargets(files, [], rescanAll: false));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ScreenshotFileSelector.SelectTargets(null!, [], false));
        Assert.Throws<ArgumentNullException>(() => ScreenshotFileSelector.SelectTargets(Files, null!, false));
    }
}
