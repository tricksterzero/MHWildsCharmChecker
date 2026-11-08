using CharmChecker.Core.Model;
using CharmChecker.Core.Screenshot;

namespace CharmChecker.Tests;

/// <summary>スクショ読み取りのうち、画像を使わない部分(枠のLvの振り分け・引数の検査)のテスト。</summary>
public class ScreenshotCharmReaderTests
{
    [Fact]
    public void AssignSlots_WithWeaponSlot_FirstFrameIsWeapon()
    {
        // 栄世の護石: 1つ目の枠が武器スロット、残りが防具スロット
        var result = ScreenshotCharmReader.AssignSlots([1, 1, 1], hasWeaponSlot: true);
        Assert.NotNull(result);
        Assert.Equal([1, 1, 0], result.Value.ArmorSlots);
        Assert.Equal([1, 0, 0], result.Value.WeaponSlots);
    }

    [Fact]
    public void AssignSlots_WithoutWeaponSlot_AllFramesAreArmorAndSorted()
    {
        var result = ScreenshotCharmReader.AssignSlots([1, 3], hasWeaponSlot: false);
        Assert.NotNull(result);
        Assert.Equal([3, 1, 0], result.Value.ArmorSlots);
        Assert.Equal([0, 0, 0], result.Value.WeaponSlots);
    }

    [Fact]
    public void AssignSlots_ThreeArmorFrames_IsInvalid()
    {
        // 境界値: 武器スロットのない護石で枠が3つ(防具3個)は現行ゲームにない
        Assert.Null(ScreenshotCharmReader.AssignSlots([1, 1, 1], hasWeaponSlot: false));
    }

    [Fact]
    public void AssignSlots_FourFrames_IsInvalid()
    {
        Assert.Null(ScreenshotCharmReader.AssignSlots([1, 1, 1, 1], hasWeaponSlot: true));
    }

    [Fact]
    public void AssignSlots_NoFrames_ReturnsAllZero()
    {
        var result = ScreenshotCharmReader.AssignSlots([], hasWeaponSlot: true);
        Assert.NotNull(result);
        Assert.Equal([0, 0, 0], result.Value.ArmorSlots);
        Assert.Equal([0, 0, 0], result.Value.WeaponSlots);
    }

    [Fact]
    public void AssignSlots_NullLevels_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ScreenshotCharmReader.AssignSlots(null!, hasWeaponSlot: false));
    }

    [Fact]
    public async Task ProcessAllAsync_ParallelismBelowOne_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ScreenshotCharmReader.ProcessAllAsync(
            [], [], [], parallelism: 0, progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAllAsync_EmptyFiles_ReturnsEmpty()
    {
        var result = await ScreenshotCharmReader.ProcessAllAsync(
            [], [], Array.Empty<CharmTypeInfo>(), parallelism: 1, progress: null, CancellationToken.None);
        Assert.Empty(result);
    }
}
