using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using CharmChecker.Core.Model;
using CharmChecker.Core.Screenshot;
using CharmChecker.Core.Skill;
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
    /// <summary>"system"(Windowsの設定に合わせる、既定)/"light"/"dark"。<see cref="ThemeService.Parse"/>参照。</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "system";
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

    private AppThemeMode _themeMode = AppThemeMode.System;

    public MainWindow()
    {
        InitializeComponent();
        ErrorLogger.LogStartup();
        LoadSettings();
        ThemeService.Apply(_themeMode, this);
        LoadCharms();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
        // 保存を止めている場合は、起動時に知らせているため終了時は黙って保存しない
        if (!_charmSaveBlocked)
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
            _themeMode = ThemeService.Parse(settings.Theme);
        }
        catch (Exception ex) { ErrorLogger.Log("LoadSettings", SettingsFilePath, ex); }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                WindowWidth = Width,
                WindowHeight = Height,
                WindowLeft = Left,
                WindowTop = Top,
                ScreenshotFolder = ScreenshotFolderPath.Text,
                DetailPanelHeight = DetailRowDef.ActualHeight,
                Theme = ThemeService.ToSettingString(_themeMode),
            };
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("SaveSettings", SettingsFilePath, ex);
        }
    }

    /// <summary>
    /// 起動時のcharms.jsonの読み込みで、元のファイルを守るために護石データの保存を止めているか。
    /// trueになるのは、ファイルを読み込めなかった場合と、不正な項目を除いて読み込んだがバックアップを
    /// 作れなかった場合(このまま保存すると、元のファイルが読み込めた分だけ・または空の一覧で上書きされるため)。
    /// 起動している間は戻さない(ファイルを直して起動し直してもらう)。2026-11-08追加。
    /// v1.4.1までは、読み込めなかった場合も一覧を空のまま起動し、終了時に空の一覧で上書きしていた。
    /// </summary>
    private bool _charmSaveBlocked;
    private bool _charmSaveBlockedNotified;

    private void LoadCharms()
    {
        if (!File.Exists(CharmsFilePath)) return;

        CharmJsonParseResult parsed;
        try
        {
            parsed = CharmJsonSerializer.Parse(File.ReadAllText(CharmsFilePath));
        }
        catch (Exception ex)
        {
            // 想定外の例外でも起動を止めず、元のファイルを守るために保存を止める(v1.4.1までと同じくすべて捕まえる)
            _charmSaveBlocked = true;
            ErrorLogger.Log("LoadCharms", CharmsFilePath, ex);
            MessageBox.Show(
                $"護石データを読み込めませんでした。\n\nファイル: {CharmsFilePath}\n{ex.Message}\n\n"
                + "元のファイルを守るため、このまま起動している間は護石データを保存しません（追加・編集しても保存されません）。"
                + "ファイルを直すか別の場所へ移してから、起動し直してください。",
                "読み込みエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (parsed.Rejected.Count > 0)
            HandleRejectedOnLoad(parsed.Rejected);

        // charm-types.jsonの固有名護石以外で、修正前の実装漏れによりRarityが
        // null保存されたままの既存データを起動時に補完する。
        foreach (var charm in parsed.Charms.Select(RarityInference.WithInferredRarity))
            AddCharm(charm);
    }

    /// <summary>
    /// 起動時の読み込みで不変条件を満たさず除いた項目があるとき、元のファイルをバックアップして知らせる。
    /// バックアップを作れなければ保存を止める(<see cref="_charmSaveBlocked"/>)。
    /// </summary>
    private void HandleRejectedOnLoad(IReadOnlyList<CharmJsonRejection> rejected)
    {
        foreach (var r in rejected)
            ErrorLogger.Log("LoadCharms", $"{r.Index + 1}件目を読み込みませんでした: {r.Reason}");

        var backupPath = $"{CharmsFilePath}.bak-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Copy(CharmsFilePath, backupPath, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _charmSaveBlocked = true;
            ErrorLogger.Log("LoadCharms", backupPath, ex);
            MessageBox.Show(
                $"護石データのうち{rejected.Count}件が不正な形式のため読み込みませんでした（理由は error.log に記録しました）。\n\n"
                + $"元のファイルのバックアップを作れなかったため、元のファイルを守るために、このまま起動している間は護石データを保存しません（追加・編集しても保存されません）。\n{ex.Message}",
                "読み込みの警告",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(
            $"護石データのうち{rejected.Count}件が不正な形式のため読み込みませんでした（理由は error.log に記録しました）。\n\n"
            + $"読み込む前の元のファイルを次の場所に残しました。\n{backupPath}",
            "読み込みの警告",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void SaveCharms()
    {
        if (_charmSaveBlocked)
        {
            ErrorLogger.Log("SaveCharms", "起動時の読み込みに失敗したため、護石データを保存しませんでした。");
            if (!_charmSaveBlockedNotified)
            {
                _charmSaveBlockedNotified = true;
                MessageBox.Show(
                    "起動時に護石データ（charms.json）を正しく読み込めなかったため、元のファイルを守るために保存していません。\n"
                    + "この変更は保存されません。ファイルを直すか別の場所へ移してから、起動し直してください。",
                    "保存していません",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            return;
        }

        var json = CharmJsonSerializer.Serialize(AllCharms);
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

    private const int ProbabilitySignificantDigits = 3;

    internal static string FormatProbability(double? probability)
    {
        if (probability is not double p || p <= 0)
            return "算出不可";

        double percent = p * 100;
        int magnitude = (int)Math.Floor(Math.Log10(percent));
        int decimals = Math.Max(0, ProbabilitySignificantDigits - 1 - magnitude);
        double rounded = Math.Round(percent, decimals);

        // 丸めで桁上がりした場合(例: 9.996% → 小数点2桁のまま丸めると10.00%になり有効数字4桁になる)、
        // 桁上がり後の桁数で小数桁数を再計算し、有効数字3桁を保つ
        if (rounded > 0)
        {
            int roundedMagnitude = (int)Math.Floor(Math.Log10(rounded));
            if (roundedMagnitude > magnitude)
            {
                decimals = Math.Max(0, ProbabilitySignificantDigits - 1 - roundedMagnitude);
                rounded = Math.Round(percent, decimals);
            }
        }

        return $"約{rounded.ToString($"F{decimals}")}%";
    }

    private void CharmDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharmDataGrid.SelectedItem is CharmListItem item)
        {
            DetailPlaceholder.Visibility = Visibility.Collapsed;
            DetailContent.Visibility = Visibility.Visible;

            var charm = item.Charm;

            DetailRarity.Text = charm.Rarity.HasValue ? $"RARE {charm.Rarity.Value}" : "不明";

            DetailProbability.Text = FormatProbability(CharmProbabilityEstimator.Estimate(charm));

            DetailTheoreticalValue.Text = CharmTheoreticalValueChecker.IsTheoretical(charm) switch
            {
                true => "理論値護石です",
                false => "理論値ではありません",
                null => "判定不可",
            };

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
        if (!dialog.CanRepresentExisting)
        {
            MessageBox.Show(
                $"護石 #{item.Id}（{item.SkillText}  {item.SlotText}）は、編集画面で表せない値（Lv5以上のスキルや、"
                + "現行ゲームにないスロットの形など）を持つため編集できません。\n編集画面で開くと値が変わってしまうためです。",
                "編集できません",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            dialog.Close();
            return;
        }
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

    /// <summary>
    /// スクショ読み取りの並列数。処理時間の大半はWindows.Media.OcrのOCR(全画面OCRだけで1枚約100ms)で、
    /// 並列化でほぼ線形に短縮できることを実測済み(2026-09-30、Ryzen 7 9800X3D 8C/16T・検証用78枚:
    /// 並列1で約12.5s/並列4で約3.5s/並列8で約2.1s、いずれも読み取り結果は完全一致)。計測用プロセスでの
    /// ピークのワーキングセットは並列1・4では約1.2〜1.3GBでほぼ変わらないが、並列8では1.4〜1.7GBに増えるため、
    /// 性能の低い環境も考慮して論理コア数の半分・上限4とする。OCRエンジンは呼び出しごとに生成しており(生成コストは約0.09msで無視できる)、
    /// 並列呼び出しでエンジンを共有しないため、キャッシュ化はしないこと。
    /// </summary>
    private static readonly int ReadingParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

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

        List<string> allFiles;
        try
        {
            allFiles = Directory.GetFiles(folder)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLogger.Log("ReadScreenshot", folder, ex);
            ReadingSummaryText.Text = $"フォルダを読み込めませんでした: {ex.Message}";
            return;
        }

        var targetFiles = ScreenshotFileSelector.SelectTargets(
            allFiles.Select(f => (f, File.GetLastWriteTime(f))).ToList(),
            AllCharms,
            rescanAll: RescanAllCheckBox.IsChecked == true);

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

        try
        {
            var knownSkills = SkillNameLoader.LoadFromEmbeddedResource();
            var charmTypes = CharmTypeLoader.LoadFromEmbeddedResource();

            var progress = new Progress<int>(count =>
            {
                // 報告は並列に発生するため到着順が前後しうる。表示が逆戻りしないよう最大値のみ反映する
                if (count <= ReadingProgressBar.Value) return;
                ReadingProgressBar.Value = count;
                // 中断受付後も開始済みの画像は処理を続けて報告が届くため、「中断しています...」を上書きしない
                if (!ct.IsCancellationRequested)
                    ReadingProgressText.Text = $"{count} / {targetFiles.Count} 処理中...";
            });
            var outcomes = await ScreenshotCharmReader.ProcessAllAsync(
                targetFiles, knownSkills, charmTypes, ReadingParallelism, progress, ct);

            // ログ出力・集計はUIスレッドでファイル順に行う(ErrorLoggerはスレッドセーフではなく、
            // error.logの記録順・results(=一覧への追加順)もファイル順に保つため)。
            var results = new List<(Charm Charm, string FileName)>();
            int processed = 0;
            int detected = 0;
            int failed = 0;
            int decorationSkipped = 0;
            int inconsistentSkipped = 0;
            int panelMismatchSkipped = 0;

            for (int i = 0; i < outcomes.Length; i++)
            {
                if (outcomes[i] is not { } outcome) continue;
                processed++;
                var fileName = Path.GetFileName(targetFiles[i]);

                switch (outcome.Kind)
                {
                    case ScreenshotOutcomeKind.NotCharmPanel:
                        break;
                    case ScreenshotOutcomeKind.Detected:
                        results.Add((outcome.Charm!, fileName));
                        detected++;
                        break;
                    case ScreenshotOutcomeKind.NoValidSkill:
                        // 護石パネル自体は検出できたがスキル名/Lvが1件も読み取れなかったケース。
                        // 「対象外スクショだった」のか「護石だが読み取り失敗」なのかをログで区別できるようにする。
                        ErrorLogger.Log("ReadScreenshot",
                            $"{fileName}: 護石パネルは検出できたがスキルを1件も読み取れませんでした。");
                        failed++;
                        break;
                    case ScreenshotOutcomeKind.NoCharmName:
                        ErrorLogger.Log("ReadScreenshot",
                            $"{fileName}: 護石名を読み取れず武器スロット有無が不明なため読み取りをスキップしました。");
                        failed++;
                        break;
                    case ScreenshotOutcomeKind.DecorationEquipped:
                        // 装飾品装着済みスロットはレベル判定が信頼できないため(CLAUDE.md参照)、
                        // 護石全体を読み取り対象から除外する。エラーではなく仕様上の除外のため
                        // failedとは別カウントし、ユーザーに区別して提示する。
                        ErrorLogger.Log("ReadScreenshot",
                            $"{fileName}: スロットに装飾品が装着されているため読み取りをスキップしました。");
                        decorationSkipped++;
                        break;
                    case ScreenshotOutcomeKind.SlotDetectionFailed:
                        ErrorLogger.Log("ReadScreenshot",
                            $"{fileName}: スロットアイコンを検出できなかったため読み取りをスキップしました。");
                        failed++;
                        break;
                    case ScreenshotOutcomeKind.InconsistentWithCombinations:
                        // 実在しない構成の読み取り結果(スキルの読み落とし等)を保存しないための除外。
                        // 装飾品装着と同じく、処理エラーとは別に数えてユーザーに区別して提示する。
                        ErrorLogger.Log("ReadScreenshot",
                            $"{fileName}: 読み取ったスキル・スロットが護石の組み合わせ表のどれにも当てはまらないため、読み取りをスキップしました。");
                        inconsistentSkipped++;
                        break;
                    case ScreenshotOutcomeKind.SlotPanelMismatch:
                        // 2つの護石が混ざった結果(スキル欄とスロット欄が別のパネル)を保存しないための除外。
                        // 組み合わせ表との照合と同じく、処理エラーとは別に数える。
                        ErrorLogger.Log("ReadScreenshot",
                            $"{fileName}: スキル欄とスロット欄が別の護石パネルのものと判定されたため、読み取りをスキップしました。");
                        panelMismatchSkipped++;
                        break;
                    case ScreenshotOutcomeKind.Error:
                        ErrorLogger.Log("ReadScreenshot", fileName, outcome.Error!);
                        failed++;
                        break;
                }
            }

            bool cancelled = processed < targetFiles.Count;

            // charmType(未解/史伝/秘歴/栄世の護石)でRarityが決まらなかった護石(通常護石・希望の護石等)は
            // スキル・スロット構成からのRARE推定で補完する。
            results = results.Select(r => (RarityInference.WithInferredRarity(r.Charm), r.FileName)).ToList();

            CancelReadingButton.Visibility = Visibility.Collapsed;

            if (cancelled)
            {
                ReadingProgressText.Text = "中断";
                var summary = $"{processed} / {targetFiles.Count}枚まで処理（中断）、{detected}枚から護石を検出";
                if (failed > 0)
                    summary += $"（{failed}枚で処理エラー）";
                if (decorationSkipped > 0)
                    summary += $"（{decorationSkipped}枚は装飾品装着のため除外）";
                if (inconsistentSkipped > 0)
                    summary += $"（{inconsistentSkipped}枚は読み取り結果が組み合わせ表に当てはまらないため除外）";
                if (panelMismatchSkipped > 0)
                    summary += $"（{panelMismatchSkipped}枚はスキル欄とスロット欄の位置が合わないため除外）";
                ReadingSummaryText.Text = summary;
            }
            else
            {
                ReadingProgressText.Text = "完了";
                var summary = $"{allFiles.Count}枚中 新規{targetFiles.Count}枚を処理、{detected}枚から護石を検出";
                if (failed > 0)
                    summary += $"（{failed}枚で処理エラー）";
                if (decorationSkipped > 0)
                    summary += $"（{decorationSkipped}枚は装飾品装着のため除外）";
                if (inconsistentSkipped > 0)
                    summary += $"（{inconsistentSkipped}枚は読み取り結果が組み合わせ表に当てはまらないため除外）";
                if (panelMismatchSkipped > 0)
                    summary += $"（{panelMismatchSkipped}枚はスキル欄とスロット欄の位置が合わないため除外）";
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
        }
        finally
        {
            StartReadingButton.IsEnabled = true;
        }
    }

    private void CancelReading_Click(object sender, RoutedEventArgs e)
    {
        _readingCts?.Cancel();
        CancelReadingButton.IsEnabled = false;
        ReadingProgressText.Text = "中断しています...";
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

            var parsedRaw = CharmCsvConverter.ParseText(csvText);

            // ゲームにありえない値(存在しないLv・スロットの形・スキル0個)は、形式エラーと同じく行番号付きで
            // 全体を取り込まない。候補にないスキル名は警告として件数だけ知らせて取り込む
            var skillGroups = RarityInference.LoadSkillGroups();
            var issuesPerLine = parsedRaw.Select(c => CharmValidator.Validate(c, skillGroups)).ToList();
            int errorIndex = issuesPerLine.FindIndex(issues => CharmValidator.HasError(issues));
            if (errorIndex >= 0)
            {
                var errors = issuesPerLine[errorIndex]
                    .Where(i => i.Severity == CharmIssueSeverity.Error).Select(i => i.Message);
                ImportResultText.Text = $"読み込みエラー: 行 {errorIndex + 1}: {string.Join(" ", errors)}（どの護石も取り込んでいません）";
                return;
            }
            int warningCount = issuesPerLine.Count(issues => issues.Count > 0);
            string warningNote = warningCount > 0 ? $"（{warningCount}件にスキル候補一覧にないスキル名があります）" : "";
            var parsed = parsedRaw.Select(RarityInference.WithInferredRarity).ToList();

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
                ImportResultText.Text = $"{parsed.Count}件をインポートしました。{warningNote}";
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
                ImportResultText.Text = $"{added}件を追加、{skipped}件をスキップしました。（既存{CharmItems.Count}件）{warningNote}";
            }
        }
        catch (FormatException ex)
        {
            ImportResultText.Text = $"読み込みエラー: {ex.Message}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ImportResultText.Text = $"ファイル読み込みエラー: {ex.Message}";
        }
    }

    /// <summary>一覧に完全同一の護石があるか(判定は重複チェックと同じ<see cref="DuplicateChecker.AreIdentical"/>)。</summary>
    private bool IsExactDuplicate(Charm candidate) =>
        AllCharms.Any(existing => DuplicateChecker.AreIdentical(existing, candidate));

    private IEnumerable<Charm> AllCharms => CharmItems.Select(i => i.Charm);

    private void ExportClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (CharmItems.Count == 0)
        {
            ExportResultText.Text = "エクスポートする護石がありません。";
            return;
        }
        var text = CharmCsvConverter.ToText(AllCharms);
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            // 他のアプリがクリップボードを開いている間は、書き込みが失敗することがある
            ErrorLogger.Log("ExportClipboard", "クリップボードへの書き込みに失敗しました。", ex);
            ExportResultText.Text = "クリップボードを使用できませんでした。少し待ってからもう一度お試しください。";
            return;
        }
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

    /// <summary>タイトルバーのメニューボタンのショートカット(Alt+D/Alt+S/Alt+A、MainWindow.xamlのInputBindings)。</summary>
    public static readonly System.Windows.Input.RoutedCommand OpenDataMenuCommand = new();
    public static readonly System.Windows.Input.RoutedCommand OpenSettingsCommand = new();
    public static readonly System.Windows.Input.RoutedCommand OpenAboutCommand = new();

    private void OpenDataMenuCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        => TitleMenuButton_Click(DataMenuButton, e);

    private void OpenSettingsCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        => SettingsMenu_Click(sender, e);

    private void OpenAboutCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        => AboutMenu_Click(sender, e);

    /// <summary>タイトルバー内のメニューボタン: 左クリックでも自身のContextMenuをボタン直下に開く。</summary>
    private void TitleMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(ScreenshotFolderPath.Text, _themeMode) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ScreenshotFolderPath.Text = dialog.ScreenshotFolder;
            // テーマは設定ダイアログを閉じた後に適用する(開いているダイアログがない状態で色定義を差し替える)
            if (dialog.SelectedTheme != _themeMode)
            {
                _themeMode = dialog.SelectedTheme;
                ThemeService.Apply(_themeMode, this);
            }
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

        CharmJsonParseResult parseResult;
        try
        {
            parseResult = CharmJsonSerializer.Parse(File.ReadAllText(dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show($"読み込みエラー: {ex.Message}", "インポート", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 新しく取り込むデータなので、不変条件の違反に加えてゲームにありえない値の項目も取り込まない
        // (起動時の既存データの読み込みとは扱いが違う)。候補にないスキル名は警告として件数だけ知らせる
        var skillGroups = RarityInference.LoadSkillGroups();
        var excludedReasons = parseResult.Rejected.Select(r => $"{r.Index + 1}件目: {r.Reason}").ToList();
        var parsed = new List<Charm>();
        int warningCount = 0;
        foreach (var charm in parseResult.Charms)
        {
            var issues = CharmValidator.Validate(charm, skillGroups);
            if (CharmValidator.HasError(issues))
            {
                excludedReasons.Add(string.Join(" ", issues.Where(i => i.Severity == CharmIssueSeverity.Error).Select(i => i.Message)));
                continue;
            }
            if (issues.Count > 0) warningCount++;
            parsed.Add(RarityInference.WithInferredRarity(charm));
        }
        foreach (var reason in excludedReasons)
            ErrorLogger.Log("ImportLocalData", $"{dialog.FileName}: 取り込みませんでした: {reason}");

        if (parsed.Count == 0)
        {
            var detail = excludedReasons.Count > 0 ? $"\n（{excludedReasons.Count}件は不正な形式のため取り込めません。理由は error.log に記録しました）" : "";
            MessageBox.Show($"インポート対象の護石データがありません。{detail}", "インポート");
            return;
        }

        var notes = "";
        if (excludedReasons.Count > 0)
            notes += $"\n不正な形式のため取り込まない: {excludedReasons.Count}件（例: {excludedReasons[0]}。理由は error.log に記録しました）";
        if (warningCount > 0)
            notes += $"\nスキル候補一覧にないスキル名を含む: {warningCount}件（取り込みます）";

        var mode = MessageBox.Show(
            $"インポートデータ: {parsed.Count}件\n既存データ: {CharmItems.Count}件{notes}\n\n"
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

        // ディスク上のcharms.jsonをコピーすると、直前の保存に失敗していた場合に表示中の一覧と違う
        // 古い内容を書き出してしまうため、表示中の一覧をそのまま書き出す
        try
        {
            File.WriteAllText(dialog.FileName, CharmJsonSerializer.Serialize(AllCharms));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLogger.Log("ExportLocalData", dialog.FileName, ex);
            MessageBox.Show($"エクスポートに失敗しました。\n\nファイル: {dialog.FileName}\n{ex.Message}",
                "エクスポート", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
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

    private void AboutMenu_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }
}