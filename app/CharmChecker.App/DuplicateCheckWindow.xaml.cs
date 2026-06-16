using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CharmChecker.Core.Model;
using Wpf.Ui.Controls;

namespace CharmChecker.App;

public partial class DuplicateCheckWindow : FluentWindow
{
    public DuplicateCheckWindow(ObservableCollection<CharmListItem> charmItems)
    {
        InitializeComponent();
        RunCheck(charmItems);
    }

    private void RunCheck(ObservableCollection<CharmListItem> charmItems)
    {
        if (charmItems.Count == 0)
        {
            DupCheckSummary.Text = "護石データがありません。";
            return;
        }

        var charms = charmItems.Select(i => i.Charm).ToList();
        var result = DuplicateChecker.Check(charms);

        var identicalCount = result.DuplicateGroups.Count;
        var inferiorCount = result.Inferiors.Count;

        if (identicalCount == 0 && inferiorCount == 0)
        {
            DupCheckSummary.Text = $"{charmItems.Count}件をチェックしました。重複・上位互換は見つかりませんでした。";
            return;
        }

        DupCheckSummary.Text = $"{charmItems.Count}件をチェックしました。"
            + $"完全同一: {identicalCount}グループ、上位互換あり: {inferiorCount}件";

        if (identicalCount > 0)
        {
            DupIdenticalSection.Visibility = Visibility.Visible;
            DupIdenticalList.ItemsSource = result.DuplicateGroups.Select(g =>
            {
                var ids = g.Indices.Select(i => $"#{charmItems[i].Id}");
                var sample = charmItems[g.Indices[0]];
                return new
                {
                    Header = $"[RARE {sample.RarityText}] {sample.SkillText}  {sample.SlotText}（{g.Indices.Count}個）",
                    Detail = $"対象: {string.Join(", ", ids)}",
                };
            }).ToList();
        }

        if (inferiorCount > 0)
        {
            DupInferiorSection.Visibility = Visibility.Visible;
            DupInferiorList.ItemsSource = result.Inferiors.Select(inf =>
            {
                var target = charmItems[inf.TargetIndex];
                var superiors = inf.SuperiorIndices
                    .Select(i => $"#{charmItems[i].Id}（{charmItems[i].SkillText}  {charmItems[i].SlotText}）");
                return new
                {
                    Header = $"処分候補: #{target.Id}（[RARE {target.RarityText}] {target.SkillText}  {target.SlotText}）",
                    Detail = $"上位互換: {string.Join(", ", superiors)}",
                };
            }).ToList();
        }
    }
}
