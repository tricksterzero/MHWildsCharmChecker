using System.Windows;
using System.Windows.Controls;
using CharmChecker.Core.Model;
using CharmChecker.Core.Skill;
using System.Linq;

namespace CharmChecker.App;

public partial class CharmEditWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly IReadOnlyList<string> _skillNames;

    /// <summary>編集対象の護石(新規入力ではnull)。スキル・スロットを変えなかったときに元のRAREを残すために使う。</summary>
    private readonly Charm? _existing;

    public Charm? ResultCharm { get; private set; }

    /// <summary>
    /// 編集対象の護石を、この画面の選択肢で欠けなく表せるか(新規入力では常にtrue)。falseなら呼び出し側は
    /// 画面を開かずに知らせる(過去のCSV取り込みで入った、スキルLv5以上や防具[2,2,0]などの護石)。
    /// </summary>
    public bool CanRepresentExisting { get; } = true;

    // 入力欄の組(1〜3つ目の順)。画面の欄はInitializeComponentの後でできるため、フィールドの初期化式ではなく
    // 呼ぶたびに配列を作るプロパティにする
    private ComboBox[] SkillNameBoxes => [Skill1Name, Skill2Name, Skill3Name];
    private ComboBox[] SkillLvBoxes => [Skill1Lv, Skill2Lv, Skill3Lv];
    private ComboBox[] SlotBoxes => [ArmorSlot1, ArmorSlot2, WeaponSlot1];

    public CharmEditWindow(Charm? existing = null)
    {
        InitializeComponent();

        var gameOrder = GameSkillOrder.Order.ToList();
        var charmSkillNames = new HashSet<string>(
            CharmCombinationTable.LoadSkillGroups().Select(e => e.Name));
        _skillNames = gameOrder
            .Where(n => charmSkillNames.Contains(n))
            .ToList();

        foreach (var cb in SkillNameBoxes)
            cb.ItemsSource = _skillNames;
        foreach (var cb in SkillLvBoxes)
            cb.SelectedIndex = 0;
        foreach (var cb in SlotBoxes)
            cb.SelectedIndex = 0;

        if (existing is not null)
        {
            Title = "護石の編集";
            _existing = existing;
            LoadFromCharm(existing);
            // 編集画面の選択肢(スキルLv0〜4、防具1つ目0〜3・2つ目0〜1、武器0〜1)で表せない値を持つ護石は、
            // 開いてOKすると値が黙って変わってしまうため、読み込んだ値から組み立て直して元と同じかを確かめる
            var (rebuilt, _) = TryBuildCharm();
            CanRepresentExisting = rebuilt is not null && DuplicateChecker.AreIdentical(rebuilt, existing);
        }
        else
        {
            Title = "護石の手動入力";
        }

        foreach (var cb in SkillLvBoxes)
            cb.SelectionChanged += OnInputChanged;
        foreach (var cb in SlotBoxes)
            cb.SelectionChanged += OnInputChanged;
        foreach (var cb in SkillNameBoxes)
        {
            cb.SelectionChanged += OnInputChanged;
            cb.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new RoutedEventHandler(OnInputChanged));
        }
    }

    private void OnInputChanged(object sender, RoutedEventArgs e)
    {
        UpdateInferredRarity();
    }

    private void LoadFromCharm(Charm charm)
    {
        var skillNames = SkillNameBoxes;
        var skillLvs = SkillLvBoxes;

        for (int i = 0; i < charm.Skills.Count && i < skillNames.Length; i++)
        {
            skillNames[i].Text = charm.Skills[i].Name;
            SetComboBoxIndex(skillLvs[i], charm.Skills[i].Lv);
        }

        var armorSorted = charm.ArmorSlotsDescending.Where(v => v > 0).ToList();
        if (armorSorted.Count > 0) SetComboBoxIndex(ArmorSlot1, armorSorted[0]);
        if (armorSorted.Count > 1) SetComboBoxIndex(ArmorSlot2, armorSorted[1]);

        var weaponActive = charm.WeaponSlots.Where(v => v > 0).ToList();
        if (weaponActive.Count > 0) SetComboBoxIndex(WeaponSlot1, weaponActive[0]);

        UpdateInferredRarity();
    }

    private static void SetComboBoxIndex(ComboBox cb, int value)
    {
        if (value >= 0 && value < cb.Items.Count)
            cb.SelectedIndex = value;
    }

    private static int GetComboBoxValue(ComboBox cb)
    {
        return cb.SelectedIndex >= 0 ? cb.SelectedIndex : 0;
    }

    private void UpdateInferredRarity()
    {
        // 入力の途中(同じスキルを2つ選んでいる等)で護石を作れないときは「-」を表示するだけにする
        var (charm, _) = TryBuildCharm();
        InferredRarityText.Text = charm?.Rarity is int rarity ? $"RARE {rarity}" : "-";
    }

    /// <summary>スキル欄の入力(1〜3つ目の順。名前は前後の空白を除いたもの)。</summary>
    private List<(string Name, int Lv)> ReadSkillRows() =>
        SkillNameBoxes.Zip(SkillLvBoxes)
            .Select(pair => (pair.First.Text?.Trim() ?? "", GetComboBoxValue(pair.Second)))
            .ToList();

    /// <summary>
    /// 画面の入力から護石を作る(RAREは推定して入れる。編集でスキル・スロットを変えていなければ元のRAREを残す)。
    /// 名前の重複など護石の不変条件を満たさない入力なら、護石の代わりに理由を返す。
    /// 名前とLvの両方が入った欄だけを使い、それ以外の欄は読み飛ばす(片方だけの欄はOK時に<see cref="HalfFilledSkillRowMessage"/>で拒否する)。
    /// </summary>
    private (Charm? Charm, string? Error) TryBuildCharm()
    {
        var skills = ReadSkillRows()
            .Where(row => row.Name != "" && row.Lv > 0)
            .Select(row => new CharmSkill(row.Name, row.Lv))
            .ToList();

        var armorSlots = new List<int>
        {
            GetComboBoxValue(ArmorSlot1),
            GetComboBoxValue(ArmorSlot2),
            0,
        };
        var weaponSlots = new List<int>
        {
            GetComboBoxValue(WeaponSlot1),
            0,
            0,
        };

        // 同じスキル名の重複は護石の不変条件に反するため、護石を作る前に理由を返す
        var duplicateNames = skills.GroupBy(s => s.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateNames.Count > 0)
            return (null, $"同じスキルが複数入力されています: {string.Join(", ", duplicateNames)}");

        try
        {
            var charm = new Charm
            {
                Skills = skills,
                ArmorSlots = armorSlots,
                WeaponSlots = weaponSlots,
                Source = CharmSource.Manual,
                SourceTimestamp = DateTime.Now,
            };
            return (KeepRarityIfUnchanged(RarityInference.WithInferredRarity(charm), _existing), null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// 編集でスキル・スロットを変えていなければ(<see cref="DuplicateChecker.AreIdentical"/>)、元の護石のRAREにした護石を返す。
    /// それ以外(新規入力・構成を変えた編集)は<paramref name="built"/>をそのまま返す(副作用なし)。
    /// 推定し直すと、候補にないスキル名を含む護石などでは元のRAREが消えてしまうため(2026-11-12)。
    /// 事前条件: builtはnullでない。
    /// </summary>
    internal static Charm KeepRarityIfUnchanged(Charm built, Charm? existing)
    {
        ArgumentNullException.ThrowIfNull(built);
        return existing is not null && DuplicateChecker.AreIdentical(built, existing)
            ? built.WithRarity(existing.Rarity)
            : built;
    }

    /// <summary>
    /// スキル欄のうち、名前とLvの片方だけが入った最初の欄を知らせる文を返す。なければnull(副作用なし)。
    /// 名前は空文字列なら未入力、Lvは0なら未入力とみなす(名前は呼び出し側で前後の空白を除いておく)。
    /// Lvを選び忘れた欄を黙って捨てて保存しないため(CSVの取り込みでも同じ入力は拒否している。2026-11-12)。
    /// 事前条件: rowsとその名前はnullでなく、Lvは0以上。
    /// </summary>
    internal static string? HalfFilledSkillRowMessage(IReadOnlyList<(string Name, int Lv)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        for (int i = 0; i < rows.Count; i++)
        {
            var (name, lv) = rows[i];
            ArgumentNullException.ThrowIfNull(name, nameof(rows));
            ArgumentOutOfRangeException.ThrowIfNegative(lv, nameof(rows));
            if ((name != "") != (lv > 0))
                return $"スキル{i + 1}の名前とLvの片方だけが入力されています。名前とLvの両方を入力するか、両方を空（Lv0）にしてください。";
        }
        return null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var halfFilled = HalfFilledSkillRowMessage(ReadSkillRows());
        if (halfFilled is not null)
        {
            MessageBox.Show(halfFilled, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (charm, error) = TryBuildCharm();
        if (charm is null)
        {
            MessageBox.Show(error, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // ゲームとしてありうるかの検査(手動入力・CSV・JSONで共通のCharmValidator)。Errorは保存しない。
        // Warning(スキル候補一覧にない名前)は確認して、はいなら保存する
        var issues = CharmValidator.Validate(charm);
        var errorMessages = CharmValidator.ErrorMessages(issues);
        if (errorMessages.Count > 0)
        {
            var message = charm.Skills.Count == 0 ? "スキルを1つ以上入力してください。" : errorMessages[0];
            MessageBox.Show(message, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        foreach (var warning in issues)
        {
            var result = MessageBox.Show(
                $"{warning.Message}このまま保存しますか？",
                "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        ResultCharm = charm;
        DialogResult = true;
    }
}
