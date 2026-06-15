using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CharmChecker.Core.Model;
using Microsoft.Win32;

namespace CharmChecker.App;

public class CharmListItem
{
    public int Id { get; }
    public Charm Charm { get; }
    public string SkillText { get; }
    public string SlotText { get; }
    public string DateText { get; }
    public string DateTimeText { get; }

    public CharmListItem(int id, Charm charm)
    {
        Id = id;
        Charm = charm;

        SkillText = charm.Skills.Count > 0
            ? string.Join(" / ", charm.Skills.Select(s => $"{s.Name} Lv{s.Lv}"))
            : "（なし）";

        SlotText = FormatSlots(charm);

        DateText = charm.SourceTimestamp.ToString("yyyy-MM-dd");
        DateTimeText = charm.SourceTimestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string FormatSlots(Charm charm)
    {
        var parts = new List<string>();

        var weaponLv = charm.WeaponSlots.Where(v => v > 0).ToList();
        var armorLvs = charm.ArmorSlots.Where(v => v > 0).OrderByDescending(v => v).ToList();

        foreach (var lv in weaponLv)
            parts.Add($"武{lv}");
        foreach (var lv in armorLvs)
            parts.Add($"防{lv}");

        return parts.Count > 0 ? string.Join(" - ", parts) : "なし";
    }
}

public partial class MainWindow : Window
{
    public ObservableCollection<CharmListItem> CharmItems { get; } = [];
    private int _nextId = 1;

    public MainWindow()
    {
        InitializeComponent();
    }

    private CharmListItem AddCharm(Charm charm)
    {
        var item = new CharmListItem(_nextId++, charm);
        CharmItems.Add(item);
        return item;
    }

    private void CharmDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharmDataGrid.SelectedItem is CharmListItem item)
        {
            DetailPlaceholder.Visibility = Visibility.Collapsed;
            DetailContent.Visibility = Visibility.Visible;

            var charm = item.Charm;

            DetailSkills.Text = charm.Skills.Count > 0
                ? string.Join("\n", charm.Skills.Select(s => $"{s.Name} Lv{s.Lv}"))
                : "（なし）";

            var slotLines = new List<string>();
            var weaponLv = charm.WeaponSlots.Where(v => v > 0).ToList();
            var armorLvs = charm.ArmorSlots.Where(v => v > 0).OrderByDescending(v => v).ToList();
            if (weaponLv.Count > 0)
                slotLines.Add($"武器: Lv{weaponLv[0]}");
            if (armorLvs.Count > 0)
                slotLines.Add($"防具: {string.Join(", ", armorLvs.Select(v => $"Lv{v}"))}");
            DetailSlots.Text = slotLines.Count > 0 ? string.Join("\n", slotLines) : "なし";

            DetailSource.Text = charm.Source switch
            {
                CharmSource.Screenshot => "スクリーンショット",
                CharmSource.CsvImport => "CSVインポート",
                CharmSource.Manual => "手動入力",
                _ => "不明",
            };

            DetailDateTime.Text = item.DateTimeText;
        }
        else
        {
            DetailPlaceholder.Visibility = Visibility.Visible;
            DetailContent.Visibility = Visibility.Collapsed;
        }
    }

    private void EditCharm_Click(object sender, RoutedEventArgs e)
    {
        // 編集機能は後で実装
        MessageBox.Show("編集機能は未実装です。", "未実装");
    }

    private void DeleteCharm_Click(object sender, RoutedEventArgs e)
    {
        if (CharmDataGrid.SelectedItem is not CharmListItem item) return;

        var result = MessageBox.Show(
            $"護石 #{item.Id}（{item.SkillText}）を削除しますか？",
            "削除の確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            CharmItems.Remove(item);
    }

    private void ExecuteDuplicateCheck_Click(object sender, RoutedEventArgs e)
    {
        if (CharmItems.Count == 0)
        {
            DupCheckSummary.Text = "護石データがありません。";
            DupIdenticalSection.Visibility = Visibility.Collapsed;
            DupInferiorSection.Visibility = Visibility.Collapsed;
            return;
        }

        var charms = CharmItems.Select(i => i.Charm).ToList();
        var result = DuplicateChecker.Check(charms);

        var identicalCount = result.DuplicateGroups.Count;
        var inferiorCount = result.Inferiors.Count;

        if (identicalCount == 0 && inferiorCount == 0)
        {
            DupCheckSummary.Text = $"{CharmItems.Count}件をチェックしました。重複・上位互換は見つかりませんでした。";
            DupIdenticalSection.Visibility = Visibility.Collapsed;
            DupInferiorSection.Visibility = Visibility.Collapsed;
            return;
        }

        DupCheckSummary.Text = $"{CharmItems.Count}件をチェックしました。"
            + $"完全同一: {identicalCount}グループ、上位互換あり: {inferiorCount}件";

        if (identicalCount > 0)
        {
            DupIdenticalSection.Visibility = Visibility.Visible;
            DupIdenticalList.ItemsSource = result.DuplicateGroups.Select(g =>
            {
                var ids = g.Indices.Select(i => $"#{CharmItems[i].Id}");
                var sample = CharmItems[g.Indices[0]];
                return new
                {
                    Header = $"{sample.SkillText}  {sample.SlotText}（{g.Indices.Count}個）",
                    Detail = $"対象: {string.Join(", ", ids)}",
                };
            }).ToList();
        }
        else
        {
            DupIdenticalSection.Visibility = Visibility.Collapsed;
        }

        if (inferiorCount > 0)
        {
            DupInferiorSection.Visibility = Visibility.Visible;
            DupInferiorList.ItemsSource = result.Inferiors.Select(inf =>
            {
                var target = CharmItems[inf.TargetIndex];
                var superiors = inf.SuperiorIndices
                    .Select(i => $"#{CharmItems[i].Id}（{CharmItems[i].SkillText}  {CharmItems[i].SlotText}）");
                return new
                {
                    Header = $"処分候補: #{target.Id}（{target.SkillText}  {target.SlotText}）",
                    Detail = $"上位互換: {string.Join(", ", superiors)}",
                };
            }).ToList();
        }
        else
        {
            DupInferiorSection.Visibility = Visibility.Collapsed;
        }
    }

    private void ImportSource_Changed(object sender, RoutedEventArgs e)
    {
        if (FileSelectPanel is null) return;
        FileSelectPanel.Visibility = ImportFromFile.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
        ImportTextBox.Visibility = ImportFromText.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
        };
        if (dialog.ShowDialog() == true)
            ImportFilePath.Text = dialog.FileName;
    }

    private void ExecuteImport_Click(object sender, RoutedEventArgs e)
    {
        string csvText;
        try
        {
            if (ImportFromFile.IsChecked == true)
            {
                var path = ImportFilePath.Text;
                if (string.IsNullOrWhiteSpace(path))
                {
                    ImportResultText.Text = "ファイルを選択してください。";
                    return;
                }
                csvText = File.ReadAllText(path);
            }
            else
            {
                csvText = ImportTextBox.Text;
                if (string.IsNullOrWhiteSpace(csvText))
                {
                    ImportResultText.Text = "テキストを入力してください。";
                    return;
                }
            }

            var parsed = CharmCsvConverter.ParseText(csvText);

            if (ModeOverwrite.IsChecked == true)
            {
                var result = MessageBox.Show(
                    $"既存の護石データ（{CharmItems.Count}件）をすべて削除し、インポートデータ（{parsed.Count}件）で置き換えます。\nよろしいですか？",
                    "全件上書きの確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                CharmItems.Clear();
                _nextId = 1;
                foreach (var charm in parsed)
                    AddCharm(charm);

                ImportResultText.Text = $"{parsed.Count}件をインポートしました。";
            }
            else
            {
                int added = 0;
                int skipped = 0;
                foreach (var charm in parsed)
                {
                    if (IsExactDuplicate(charm))
                    {
                        skipped++;
                    }
                    else
                    {
                        AddCharm(charm);
                        added++;
                    }
                }

                ImportResultText.Text = $"{added}件を追加、{skipped}件をスキップしました。（既存{CharmItems.Count}件）";
            }
        }
        catch (FormatException ex)
        {
            ImportResultText.Text = $"読み込みエラー: {ex.Message}";
        }
        catch (IOException ex)
        {
            ImportResultText.Text = $"ファイル読み込みエラー: {ex.Message}";
        }
    }

    private bool IsExactDuplicate(Charm candidate)
    {
        foreach (var item in CharmItems)
        {
            var existing = item.Charm;
            if (existing.Skills.Count != candidate.Skills.Count) continue;

            var existingSkills = existing.Skills
                .Select(s => $"{s.Name}:{s.Lv}").OrderBy(s => s).ToList();
            var candidateSkills = candidate.Skills
                .Select(s => $"{s.Name}:{s.Lv}").OrderBy(s => s).ToList();
            if (!existingSkills.SequenceEqual(candidateSkills)) continue;

            var existingArmor = existing.ArmorSlots.OrderByDescending(x => x).ToList();
            var candidateArmor = candidate.ArmorSlots.OrderByDescending(x => x).ToList();
            if (!existingArmor.SequenceEqual(candidateArmor)) continue;

            var existingWeapon = existing.WeaponSlots.OrderByDescending(x => x).ToList();
            var candidateWeapon = candidate.WeaponSlots.OrderByDescending(x => x).ToList();
            if (!existingWeapon.SequenceEqual(candidateWeapon)) continue;

            return true;
        }
        return false;
    }

    private IEnumerable<Charm> AllCharms => CharmItems.Select(i => i.Charm);

    private void ExportClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (CharmItems.Count == 0)
        {
            ExportResultText.Text = "エクスポートする護石がありません。";
            return;
        }
        var text = CharmCsvConverter.ToText(AllCharms);
        Clipboard.SetText(text);
        ExportResultText.Text = $"クリップボードにコピーしました。（{CharmItems.Count}件）";
    }

    private void ExportFile_Click(object sender, RoutedEventArgs e)
    {
        if (CharmItems.Count == 0)
        {
            ExportResultText.Text = "エクスポートする護石がありません。";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
            FileName = "charms.csv",
        };
        if (dialog.ShowDialog() != true) return;

        var text = CharmCsvConverter.ToText(AllCharms);
        File.WriteAllText(dialog.FileName, text);
        ExportResultText.Text = $"ファイルに保存しました。（{CharmItems.Count}件 → {dialog.FileName}）";
    }
}