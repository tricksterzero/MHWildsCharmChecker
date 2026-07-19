using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;
using CharmChecker.Core.Model;
using CharmChecker.Core.Skill;
using CharmChecker.Core.SlotIcon;
using Microsoft.Win32;

namespace CharmChecker.App;

public class CharmListItem
{
    public int Id { get; }
    public Charm Charm { get; }
    public string RarityText { get; }
    public string SkillText { get; }
    public string SlotText { get; }
    public string DateText { get; }
    public string DateTimeText { get; }

    public CharmListItem(int id, Charm charm)
    {
        Id = id;
        Charm = charm;

        RarityText = charm.Rarity.HasValue ? charm.Rarity.Value.ToString() : "-";

        SkillText = charm.Skills.Count > 0
            ? string.Join(" / ", charm.Skills.Select(s => $"{s.Name} Lv{s.Lv}"))
            : "（なし）";

        SlotText = FormatSlotsStatic(charm);

        DateText = charm.SourceTimestamp.ToString("yyyy-MM-dd");
        DateTimeText = charm.SourceTimestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static string FormatSlotsStatic(Charm charm)
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

public class CharmJson
{
    [JsonPropertyName("skills")]
    public List<SkillJson> Skills { get; set; } = [];
    [JsonPropertyName("armorSlots")]
    public List<int> ArmorSlots { get; set; } = [];
    [JsonPropertyName("weaponSlots")]
    public List<int> WeaponSlots { get; set; } = [];
    [JsonPropertyName("rarity")]
    public int? Rarity { get; set; }
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";
    [JsonPropertyName("sourceTimestamp")]
    public DateTime SourceTimestamp { get; set; }
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

public class SkillJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("lv")]
    public int Lv { get; set; }
}

public class AppSettings
{
    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 800;
    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 450;
    [JsonPropertyName("windowLeft")]
    public double WindowLeft { get; set; } = double.NaN;
    [JsonPropertyName("windowTop")]
    public double WindowTop { get; set; } = double.NaN;
    [JsonPropertyName("screenshotFolder")]
    public string ScreenshotFolder { get; set; } = "";
    [JsonPropertyName("detailPanelHeight")]
    public double DetailPanelHeight { get; set; } = double.NaN;
}

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public ObservableCollection<CharmListItem> CharmItems { get; } = [];
    private int _nextId = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string DataDir => AppDomain.CurrentDomain.BaseDirectory;
    private static string CharmsFilePath => Path.Combine(DataDir, "charms.json");
    private static string SettingsFilePath => Path.Combine(DataDir, "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        ErrorLogger.LogStartup();
        LoadSettings();
        LoadCharms();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
        SaveCharms();
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return;
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is null) return;

            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
            if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }
            if (!string.IsNullOrEmpty(settings.ScreenshotFolder))
                ScreenshotFolderPath.Text = settings.ScreenshotFolder;
            if (!double.IsNaN(settings.DetailPanelHeight) && settings.DetailPanelHeight >= 80)
                DetailRowDef.Height = new GridLength(settings.DetailPanelHeight);
        }
        catch (Exception ex) { ErrorLogger.Log("LoadSettings", SettingsFilePath, ex); }
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            WindowWidth = Width,
            WindowHeight = Height,
            WindowLeft = Left,
            WindowTop = Top,
            ScreenshotFolder = ScreenshotFolderPath.Text,
            DetailPanelHeight = DetailRowDef.ActualHeight,
        };
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    private void LoadCharms()
    {
        try
        {
            if (!File.Exists(CharmsFilePath)) return;
            var json = File.ReadAllText(CharmsFilePath);
            var charms = ParseCharmsJson(json);
            foreach (var charm in charms)
                AddCharm(charm);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("LoadCharms", CharmsFilePath, ex);
            MessageBox.Show(
                $"護石データの読み込みに失敗しました。\n\nファイル: {CharmsFilePath}\n{ex.Message}",
                "読み込みエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static List<Charm> ParseCharmsJson(string json)
    {
        var items = JsonSerializer.Deserialize<List<CharmJson>>(json);
        if (items is null) return [];

        return items.Select(cj => new Charm
        {
            Skills = cj.Skills.Select(s => new CharmSkill(s.Name, s.Lv)).ToList(),
            ArmorSlots = cj.ArmorSlots,
            WeaponSlots = cj.WeaponSlots,
            Rarity = cj.Rarity,
            Source = Enum.TryParse<CharmSource>(cj.Source, out var src) ? src : CharmSource.CsvImport,
            SourceTimestamp = cj.SourceTimestamp,
            Version = Enum.TryParse<GameVersion>(cj.Version, out var ver) ? ver : GameVersion.Wilds,
        }).ToList();
    }

    private void SaveCharms()
    {
        var items = CharmItems.Select(i =>
        {
            var c = i.Charm;
            return new CharmJson
            {
                Skills = c.Skills.Select(s => new SkillJson { Name = s.Name, Lv = s.Lv }).ToList(),
                ArmorSlots = c.ArmorSlots,
                WeaponSlots = c.WeaponSlots,
                Rarity = c.Rarity,
                Source = c.Source.ToString(),
                SourceTimestamp = c.SourceTimestamp,
                Version = c.Version.ToString(),
            };
        }).ToList();
        var json = JsonSerializer.Serialize(items, JsonOptions);
        try
        {
            File.WriteAllText(CharmsFilePath, json);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("SaveCharms", CharmsFilePath, ex);
            MessageBox.Show(
                $"護石データの保存に失敗しました。\n\nファイル: {CharmsFilePath}\n{ex.Message}",
                "保存エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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

            DetailRarity.Text = charm.Rarity.HasValue ? $"RARE {charm.Rarity.Value}" : "不明";

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

    private void ManualAddCharm_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CharmEditWindow() { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultCharm is null) return;

        AddCharm(dialog.ResultCharm);
        SaveCharms();
    }

    private void EditCharm_Click(object sender, RoutedEventArgs e)
    {
        if (CharmDataGrid.SelectedItem is not CharmListItem item) return;

        var dialog = new CharmEditWindow(item.Charm) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultCharm is null) return;

        var edited = dialog.ResultCharm;
        var newCharm = new Charm
        {
            Skills = edited.Skills,
            ArmorSlots = edited.ArmorSlots,
            WeaponSlots = edited.WeaponSlots,
            Rarity = edited.Rarity,
            Source = item.Charm.Source,
            SourceTimestamp = item.Charm.SourceTimestamp,
            Version = item.Charm.Version,
        };

        var index = CharmItems.IndexOf(item);
        CharmItems[index] = new CharmListItem(item.Id, newCharm);
        CharmDataGrid.SelectedIndex = index;
        SaveCharms();
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
        {
            CharmItems.Remove(item);
            SaveCharms();
        }
    }

    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp"];
    private List<(Charm Charm, string FileName)>? _readingResults;
    private CancellationTokenSource? _readingCts;

    private void BrowseScreenshotFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog();
        if (!string.IsNullOrEmpty(ScreenshotFolderPath.Text))
            dialog.InitialDirectory = ScreenshotFolderPath.Text;
        if (dialog.ShowDialog() == true)
            ScreenshotFolderPath.Text = dialog.FolderName;
    }

    private async void StartReading_Click(object sender, RoutedEventArgs e)
    {
        var folder = ScreenshotFolderPath.Text;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            ReadingSummaryText.Text = "フォルダを選択してください。";
            return;
        }

        var allFiles = Directory.GetFiles(folder)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        var latestScreenshot = CharmItems
            .Where(i => i.Charm.Source == CharmSource.Screenshot)
            .Select(i => i.Charm.SourceTimestamp)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        var targetFiles = allFiles
            .Where(f => File.GetLastWriteTime(f) > latestScreenshot)
            .OrderBy(f => File.GetLastWriteTime(f))
            .ToList();

        if (targetFiles.Count == 0)
        {
            ReadingSummaryText.Text = $"フォルダ内に{allFiles.Count}枚の画像がありますが、新規ファイルはありません。";
            return;
        }

        if (targetFiles.Count >= 500)
        {
            var confirm = MessageBox.Show(
                $"対象ファイルが{targetFiles.Count}枚あります。処理しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        _readingCts?.Cancel();
        _readingCts?.Dispose();
        _readingCts = new CancellationTokenSource();
        var ct = _readingCts.Token;

        StartReadingButton.IsEnabled = false;
        CancelReadingButton.IsEnabled = true;
        CancelReadingButton.Visibility = Visibility.Visible;
        ReadingProgressPanel.Visibility = Visibility.Visible;
        ReadingProgressBar.Maximum = targetFiles.Count;
        ReadingProgressBar.Value = 0;
        ReadingSummaryText.Text = "";
        ReadingResultGrid.Visibility = Visibility.Collapsed;
        AddReadingResultButton.Visibility = Visibility.Collapsed;
        _readingResults = null;

        var knownSkills = SkillNameLoader.LoadFromEmbeddedResource();
        var charmTypes = CharmTypeLoader.LoadFromEmbeddedResource();

        var results = new List<(Charm Charm, string FileName)>();
        int processed = 0;
        int detected = 0;
        int failed = 0;
        bool cancelled = false;

        foreach (var file in targetFiles)
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            processed++;
            ReadingProgressBar.Value = processed;
            ReadingProgressText.Text = $"{processed} / {targetFiles.Count} 処理中...";

            try
            {
                var readResult = await SkillReadingPipeline.ReadWithMetadataAsync(file, knownSkills);
                if (readResult is null) continue;

                var validSkills = readResult.Skills
                    .Where(s => s.Name is not null && s.Lv is not null)
                    .Select(s => new CharmSkill(s.Name!, s.Lv!.Value))
                    .ToList();

                if (validSkills.Count == 0) continue;

                var charmType = CharmTypeLoader.Lookup(readResult.CharmName, charmTypes);
                var slots = await Task.Run(() => ReadSlots(file, charmType?.HasWeaponSlot ?? false));

                var charm = new Charm
                {
                    Skills = validSkills,
                    ArmorSlots = slots.ArmorSlots,
                    WeaponSlots = slots.WeaponSlots,
                    Rarity = charmType?.Rarity,
                    Source = CharmSource.Screenshot,
                    SourceTimestamp = File.GetLastWriteTime(file),
                };
                results.Add((charm, Path.GetFileName(file)));
                detected++;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("ReadScreenshot", Path.GetFileName(file), ex);
                failed++;
            }

            await Task.Yield();
        }

        // charmType(未解/史伝/秘歴/栄世の護石)でRarityが決まらなかった護石(通常護石・希望の護石等)は
        // スキル・スロット構成からのRARE推定で補完する。
        InferRarityBatch(results.Select(r => r.Charm).ToList());

        CancelReadingButton.Visibility = Visibility.Collapsed;

        if (cancelled)
        {
            ReadingProgressText.Text = "中断";
            var summary = $"{processed} / {targetFiles.Count}枚まで処理（中断）、{detected}枚から護石を検出";
            if (failed > 0)
                summary += $"（{failed}枚で処理エラー）";
            ReadingSummaryText.Text = summary;
        }
        else
        {
            ReadingProgressText.Text = "完了";
            var summary = $"{allFiles.Count}枚中 新規{targetFiles.Count}枚を処理、{detected}枚から護石を検出";
            if (failed > 0)
                summary += $"（{failed}枚で処理エラー）";
            ReadingSummaryText.Text = summary;
        }

        if (results.Count > 0)
        {
            _readingResults = results;
            ReadingResultGrid.ItemsSource = results.Select(r => new
            {
                RarityText = r.Charm.Rarity.HasValue ? r.Charm.Rarity.Value.ToString() : "-",
                SkillText = string.Join(" / ", r.Charm.Skills.Select(s => $"{s.Name} Lv{s.Lv}")),
                SlotText = CharmListItem.FormatSlotsStatic(r.Charm),
                r.FileName,
            }).ToList();
            ReadingResultGrid.Visibility = Visibility.Visible;
            AddReadingResultButton.Visibility = Visibility.Visible;
        }

        StartReadingButton.IsEnabled = true;
    }

    private void CancelReading_Click(object sender, RoutedEventArgs e)
    {
        _readingCts?.Cancel();
        CancelReadingButton.IsEnabled = false;
        ReadingProgressText.Text = "中断しています...";
    }

    private static (List<int> ArmorSlots, List<int> WeaponSlots) ReadSlots(
        string imagePath, bool hasWeaponSlot)
    {
        using var img = OpenCvSharp.Cv2.ImRead(imagePath);
        var (sx, sy) = SlotIconAnalyzer.ScaleFactors(img);

        var (boxFrames, boxGray) = DetectInRegion(img, SlotIconAnalyzer.PanelRegion(img), sx, sy);
        var (detFrames, detGray) = DetectInRegion(img, SlotIconAnalyzer.DetailPanelRegion(img), sx, sy);

        List<OpenCvSharp.Rect> frames;
        OpenCvSharp.Mat gray;
        OpenCvSharp.Mat disposeGray;

        if (boxFrames.Count > detFrames.Count && boxFrames.Count > 0)
        {
            frames = boxFrames; gray = boxGray;
            disposeGray = detGray;
        }
        else
        {
            frames = detFrames; gray = detGray;
            disposeGray = boxGray;
        }

        disposeGray.Dispose();

        try
        {
            return ClassifyFrames(frames, gray, hasWeaponSlot);
        }
        finally
        {
            gray.Dispose();
        }
    }

    private static (List<OpenCvSharp.Rect> Frames, OpenCvSharp.Mat Gray) DetectInRegion(
        OpenCvSharp.Mat img, OpenCvSharp.Rect region, double sx, double sy)
    {
        using var panel = new OpenCvSharp.Mat(img, region);
        var gray = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.CvtColor(panel, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
        var frames = SlotIconAnalyzer.DetectFrames(gray, sx, sy);
        return (frames, gray);
    }

    private static (List<int> ArmorSlots, List<int> WeaponSlots) ClassifyFrames(
        List<OpenCvSharp.Rect> frames, OpenCvSharp.Mat gray, bool hasWeaponSlot)
    {
        var armorSlots = new List<int> { 0, 0, 0 };
        var weaponSlots = new List<int> { 0, 0, 0 };
        int armorIdx = 0;
        bool weaponAssigned = false;

        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            var levelResult = SlotIconAnalyzer.ClassifyLevel(gray, frame);

            int lv = levelResult.Level switch
            {
                SlotLevel.Lv1 => 1,
                SlotLevel.Lv2 => 2,
                SlotLevel.Lv3 => 3,
                _ => 0,
            };

            // 栄世の護石: 1つ目のフレームが武器スロット
            if (hasWeaponSlot && !weaponAssigned && i == 0)
            {
                weaponSlots[0] = lv;
                weaponAssigned = true;
            }
            else if (armorIdx < 3)
            {
                armorSlots[armorIdx++] = lv;
            }
        }

        return SlotValidation.Validate(armorSlots, weaponSlots);
    }

    private void AddReadingResult_Click(object sender, RoutedEventArgs e)
    {
        if (_readingResults is null || _readingResults.Count == 0) return;

        foreach (var (charm, _) in _readingResults)
            AddCharm(charm);

        SaveCharms();
        ReadingSummaryText.Text += $"\n{_readingResults.Count}件を護石一覧に追加しました。";
        _readingResults = null;
        AddReadingResultButton.Visibility = Visibility.Collapsed;
    }

    private void DuplicateCheck_Click(object sender, RoutedEventArgs e)
    {
        var window = new DuplicateCheckWindow(CharmItems) { Owner = this };
        window.ShowDialog();
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
            InferRarityBatch(parsed);

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

                SaveCharms();
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

                SaveCharms();
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

    private static void InferRarityBatch(List<Charm> charms)
    {
        var skillGroups = RarityInference.LoadSkillGroups();
        var combinations = RarityInference.LoadCombinations();
        foreach (var charm in charms)
        {
            if (charm.Rarity is null)
                charm.Rarity = RarityInference.Infer(charm, skillGroups, combinations);
        }
    }

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
        try
        {
            File.WriteAllText(dialog.FileName, text);
            ExportResultText.Text = $"ファイルに保存しました。（{CharmItems.Count}件 → {dialog.FileName}）";
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("ExportCsv", dialog.FileName, ex);
            ExportResultText.Text = $"保存に失敗しました: {ex.Message}";
        }
    }

    private void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(ScreenshotFolderPath.Text) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ScreenshotFolderPath.Text = dialog.ScreenshotFolder;
            SaveSettings();
        }
    }

    private void ImportLocalDataMenu_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        List<Charm> parsed;
        try
        {
            var json = File.ReadAllText(dialog.FileName);
            parsed = ParseCharmsJson(json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"読み込みエラー: {ex.Message}", "インポート", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (parsed.Count == 0)
        {
            MessageBox.Show("インポート対象の護石データがありません。", "インポート");
            return;
        }

        var mode = MessageBox.Show(
            $"インポートデータ: {parsed.Count}件\n既存データ: {CharmItems.Count}件\n\n"
            + "「はい」→ 差分追加（既存データに追加）\n"
            + "「いいえ」→ 全件上書き（既存データを置換）",
            "インポートモード",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (mode == MessageBoxResult.Cancel) return;

        if (mode == MessageBoxResult.No)
        {
            CharmItems.Clear();
            _nextId = 1;
            foreach (var charm in parsed)
                AddCharm(charm);
            SaveCharms();
            MessageBox.Show($"{parsed.Count}件をインポートしました。（全件上書き）", "インポート");
        }
        else
        {
            int added = 0, skipped = 0;
            foreach (var charm in parsed)
            {
                if (IsExactDuplicate(charm))
                    skipped++;
                else
                {
                    AddCharm(charm);
                    added++;
                }
            }
            SaveCharms();
            MessageBox.Show($"{added}件を追加、{skipped}件をスキップしました。（差分追加）", "インポート");
        }
    }

    private void ExportLocalDataMenu_Click(object sender, RoutedEventArgs e)
    {
        if (CharmItems.Count == 0)
        {
            MessageBox.Show("護石データがありません。", "エクスポート");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
            FileName = "charms.json",
        };
        if (dialog.ShowDialog() != true) return;

        if (!File.Exists(CharmsFilePath))
            SaveCharms();

        File.Copy(CharmsFilePath, dialog.FileName, overwrite: true);
        MessageBox.Show($"{CharmItems.Count}件の護石データをエクスポートしました。", "エクスポート");
    }

    private void ResetDataMenu_Click(object sender, RoutedEventArgs e)
    {
        if (CharmItems.Count == 0)
        {
            MessageBox.Show("護石データがありません。", "データの初期化");
            return;
        }

        var result = MessageBox.Show(
            $"護石データ（{CharmItems.Count}件）をすべて削除します。\nこの操作は取り消せません。よろしいですか？",
            "データの初期化",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        CharmItems.Clear();
        SaveCharms();
        MessageBox.Show("護石データを初期化しました。", "データの初期化");
    }

    private async void AboutMenu_Click(object sender, RoutedEventArgs e)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        var versionText = version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "不明";
        var dialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = "アプリ情報",
            Content = $"MHWilds 護石チェッカー Ver.{versionText}\n\n"
                + "使用ライブラリ:\n"
                + "  OpenCvSharp4 4.13.0.20260602\n"
                + "  WPF-UI 4.3.0\n"
                + "  Windows.Media.Ocr (Windows 組み込み)",
            CloseButtonText = "OK",
        };
        await dialog.ShowDialogAsync();
    }
}