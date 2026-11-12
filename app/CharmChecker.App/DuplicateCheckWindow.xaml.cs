using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using CharmChecker.Core.Model;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace CharmChecker.App;

public partial class DuplicateCheckWindow : FluentWindow
{
    private string _resultText = "";

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

        var summaryLine = $"{charmItems.Count}件をチェックしました。"
            + $"完全同一: {identicalCount}グループ、上位互換あり: {inferiorCount}件";
        DupCheckSummary.Text = summaryLine;

        var sb = new StringBuilder();
        sb.AppendLine(summaryLine);

        if (identicalCount > 0)
        {
            DupIdenticalSection.Visibility = Visibility.Visible;
            var items = result.DuplicateGroups.Select(g =>
            {
                var ids = g.Indices.Select(i => $"#{charmItems[i].Id}");
                var sample = charmItems[g.Indices[0]];
                return new ResultItem(
                    Header: $"[RARE {sample.RarityText}] {sample.SkillText}  {sample.SlotText}（{g.Indices.Count}個）",
                    Detail: $"対象: {string.Join(", ", ids)}");
            }).ToList();
            DupIdenticalList.ItemsSource = items;
            AppendSection(sb, "--- 完全同一の護石 ---", items);
        }

        if (inferiorCount > 0)
        {
            DupInferiorSection.Visibility = Visibility.Visible;
            var items = result.Inferiors.Select(inf =>
            {
                var target = charmItems[inf.TargetIndex];
                var superiors = inf.SuperiorIndices
                    .Select(i => $"#{charmItems[i].Id}（{charmItems[i].SkillText}  {charmItems[i].SlotText}）");
                return new ResultItem(
                    Header: $"処分候補: #{target.Id}（[RARE {target.RarityText}] {target.SkillText}  {target.SlotText}）",
                    Detail: $"上位互換: {string.Join(", ", superiors)}");
            }).ToList();
            DupInferiorList.ItemsSource = items;
            AppendSection(sb, "--- 上位互換がある護石 ---", items);
        }

        _resultText = sb.ToString();
        SaveButton.Visibility = Visibility.Visible;
    }

    /// <summary>画面の一覧の1項目(DuplicateCheckWindow.xamlのTextBlockがHeader・Detailにバインドする)。</summary>
    private sealed record ResultItem(string Header, string Detail);

    /// <summary>保存用の文字列に、空行・見出し・各項目(見出しの行と字下げした詳細の行)を書き足す。</summary>
    private static void AppendSection(StringBuilder sb, string title, IEnumerable<ResultItem> items)
    {
        sb.AppendLine();
        sb.AppendLine(title);
        foreach (var item in items)
        {
            sb.AppendLine(item.Header);
            sb.AppendLine($"  {item.Detail}");
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "テキストファイル (*.txt)|*.txt",
            DefaultExt = ".txt",
            FileName = "重複チェック結果.txt",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, _resultText, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("SaveDuplicateCheckResult", dialog.FileName, ex);
                System.Windows.MessageBox.Show($"保存に失敗しました: {ex.Message}", "保存エラー",
                    System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
