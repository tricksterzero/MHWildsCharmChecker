using CharmChecker.Core.SlotIcon;

namespace CharmChecker.Tests;

/// <summary>スロット枠がスキル欄のアンカーと同じパネルにあるかの判定(許容範囲 150〜450px×UIの倍率)のテスト。</summary>
public class SlotPanelConsistencyTests
{
    private const double AnchorX = 2000.0;

    [Theory]
    [InlineData(150.0, true)]   // 下限ちょうど
    [InlineData(149.0, false)]  // 下限の外
    [InlineData(450.0, true)]   // 上限ちょうど
    [InlineData(451.0, false)]  // 上限の外
    [InlineData(300.0, true)]
    [InlineData(744.0, false)]  // アンカーが左パネル、スロットが右パネル(実測)
    [InlineData(-49.0, false)]  // スロット枠がアンカーより左(実測)
    public void AreFramesInSkillPanel_Scale1_Boundaries(double dx, bool expected)
    {
        Assert.Equal(expected, SlotPanelConsistency.AreFramesInSkillPanel([AnchorX + dx], AnchorX, 1.0));
    }

    [Theory]
    [InlineData(151.0, true)]
    [InlineData(149.0, false)]
    [InlineData(449.0, true)]
    [InlineData(451.0, false)]
    public void AreFramesInSkillPanel_LetterboxScale_RangeIsScaled(double dxAtReference, bool expected)
    {
        // 黒帯付き21:9(コンテンツ高1097px)ではUIが1097/1440倍に縮小されるため、範囲も同じ倍率で縮む
        const double scale = 1097.0 / 1440.0;
        Assert.Equal(expected,
            SlotPanelConsistency.AreFramesInSkillPanel([AnchorX + dxAtReference * scale], AnchorX, scale));
    }

    [Fact]
    public void AreFramesInSkillPanel_OneFrameOutside_ReturnsFalse()
    {
        Assert.False(SlotPanelConsistency.AreFramesInSkillPanel([AnchorX + 300, AnchorX + 460], AnchorX, 1.0));
        Assert.True(SlotPanelConsistency.AreFramesInSkillPanel([AnchorX + 300, AnchorX + 350], AnchorX, 1.0));
    }

    [Fact]
    public void AreFramesInSkillPanel_NoFrames_ReturnsTrue()
    {
        Assert.True(SlotPanelConsistency.AreFramesInSkillPanel([], AnchorX, 1.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AreFramesInSkillPanel_InvalidScale_Throws(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SlotPanelConsistency.AreFramesInSkillPanel([AnchorX + 300], AnchorX, scale));
    }

    [Fact]
    public void AreFramesInSkillPanel_InvalidInputs_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => SlotPanelConsistency.AreFramesInSkillPanel(null!, AnchorX, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SlotPanelConsistency.AreFramesInSkillPanel([AnchorX + 300], double.NaN, 1.0));
        Assert.Throws<ArgumentException>(
            () => SlotPanelConsistency.AreFramesInSkillPanel([double.NaN], AnchorX, 1.0));
    }
}
