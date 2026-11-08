using CharmChecker.Core.Model;
using CharmChecker.Core.Skill;
using CharmChecker.Core.Screenshot;

namespace CharmChecker.Tests;

/// <summary>スクショ1枚の読み取り(スキル・護石名・スロット・構成表との照合)をまとめて確かめるテスト。</summary>
public class ScreenshotProcessingTests
{
    [Fact]
    public async Task ProcessScreenshotAsync_ShortSkillRowUnreadAtOriginalSize_ReadWithScaledVariants()
    {
        // 狩猟後の鑑定BOX画面。実物は栄世の護石 達人芸1/巧撃1 だが、元の大きさの前処理5種では
        // OCRが巧撃の行(名前・Lvとも)を検出できず、達人芸1だけの護石として読まれていた(2026-11-07)。
        // スキル欄の切り出しを1.5倍に拡大した前処理で2行とも読めるようになったことを確かめる回帰テスト
        // (拡大した前処理を入れる前は、構成表との照合で除外されていた)。
        var path = Path.Combine(TestPaths.FindAssetsDir(), "case8 translucent panel", "20261102110031_1.jpg");
        if (!File.Exists(path))
            return;

        var outcome = await ScreenshotCharmReader.ProcessAsync(
            path, SkillNameLoader.LoadFromEmbeddedResource(), CharmTypeLoader.LoadFromEmbeddedResource());

        Assert.Equal(ScreenshotOutcomeKind.Detected, outcome.Kind);
        Assert.Equal([new CharmSkill("達人芸", 1), new CharmSkill("巧撃", 1)], outcome.Charm!.Skills);
        Assert.Equal([1, 0, 0], outcome.Charm.ArmorSlots);
        Assert.Equal([1, 0, 0], outcome.Charm.WeaponSlots);
    }

    [Fact]
    public async Task ProcessScreenshotAsync_SkillAndSlotFromDifferentPanels_ExcludedAsPanelMismatch()
    {
        // 2護石比較画面で、アンカーは左パネル(x=1602)、スロット枠は右パネル(アンカーから744px右)で
        // 検出される画像。スキル欄とスロット欄が別の護石のため、位置の確認で除外されることを確かめる
        // (2026-11-08。確認を入れる前は、空スロットの照合で「装飾品あり」として除外されていた)。
        var path = Path.Combine(TestPaths.FindAssetsDir(), "option 1280x720", "20260615094829_1.jpg");
        if (!File.Exists(path))
            return;

        var outcome = await ScreenshotCharmReader.ProcessAsync(
            path, SkillNameLoader.LoadFromEmbeddedResource(), CharmTypeLoader.LoadFromEmbeddedResource());

        Assert.Equal(ScreenshotOutcomeKind.SlotPanelMismatch, outcome.Kind);
    }
}
