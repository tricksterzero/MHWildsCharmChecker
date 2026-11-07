using CharmChecker.Core.Model;
using CharmChecker.Core.Skill;
using static CharmChecker.App.MainWindow;

namespace CharmChecker.Tests;

/// <summary>スクショ1枚の読み取り(スキル・護石名・スロット・構成表との照合)をまとめて確かめるテスト。</summary>
public class ScreenshotProcessingTests
{
    [Fact]
    public async Task ProcessScreenshotAsync_OneOfTwoSkillsUnread_ExcludedAsInconsistent()
    {
        // 狩猟後の鑑定BOX画面。実物は栄世の護石 達人芸1/巧撃1 だが、OCRが巧撃の行を検出できず
        // 達人芸1だけが読める(2026-11-07)。1スキルの護石は構成表に存在しないため、
        // 誤った護石として保存せずに除外されるべき回帰テスト。
        var path = Path.Combine(TestPaths.FindAssetsDir(), "case8 translucent panel", "20261102110031_1.jpg");
        if (!File.Exists(path))
            return;

        var outcome = await ProcessScreenshotAsync(
            path, SkillNameLoader.LoadFromEmbeddedResource(), CharmTypeLoader.LoadFromEmbeddedResource());

        Assert.Equal(ScreenshotOutcomeKind.InconsistentWithCombinations, outcome.Kind);
        Assert.Null(outcome.Charm);
    }
}
