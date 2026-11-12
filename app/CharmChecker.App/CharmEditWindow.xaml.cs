using System.Windows;
using System.Windows.Controls;
using CharmChecker.Core.Model;
using CharmChecker.Core.Skill;
using System.Linq;

namespace CharmChecker.App;

public partial class CharmEditWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly IReadOnlyList<string> _skillNames;
    public Charm? ResultCharm { get; private set; }

    /// <summary>
    /// 編集対象の護石を、この画面の選択肢で欠けなく表せるか(新規入力では常にtrue)。falseなら呼び出し側は
    /// 画面を開かずに知らせる(過去のCSV取り込みで入った、スキルLv5以上や防具[2,2,0]などの護石)。
    /// </summary>
    public bool CanRepresentExisting { get; } = true;

    public CharmEditWindow(Charm? existing = null)
    {
        InitializeComponent();

        var gameOrder = GameSkillOrder.Order.ToList();
        var charmSkillNames = new HashSet<string>(
            RarityInference.LoadSkillGroups().Select(e => e.Name));
        _skillNames = gameOrder
            .Where(n => charmSkillNames.Contains(n))
            .ToList();

        var skillNameComboBoxes = new[] { Skill1Name, Skill2Name, Skill3Name };
        foreach (var cb in skillNameComboBoxes)
            cb.ItemsSource = _skillNames;

        var skillLvComboBoxes = new[] { Skill1Lv, Skill2Lv, Skill3Lv };
        foreach (var cb in skillLvComboBoxes)
            cb.SelectedIndex = 0;

        ArmorSlot1.SelectedIndex = 0;
        ArmorSlot2.SelectedIndex = 0;
        WeaponSlot1.SelectedIndex = 0;

        if (existing is not null)
        {
            Title = "護石の編集";
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

        foreach (var cb in skillLvComboBoxes)
            cb.SelectionChanged += OnInputChanged;
        foreach (var cb in new[] { ArmorSlot1, ArmorSlot2, WeaponSlot1 })
            cb.SelectionChanged += OnInputChanged;
        foreach (var cb in skillNameComboBoxes)
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
        var skillNames = new[] { Skill1Name, Skill2Name, Skill3Name };
        var skillLvs = new[] { Skill1Lv, Skill2Lv, Skill3Lv };

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

    /// <summary>
    /// 画面の入力から護石を作る(RAREは推定して入れる)。名前の重複など護石の不変条件を満たさない入力なら、
    /// 護石の代わりに理由を返す。名前が空・Lv0の欄は空き欄として読み飛ばす。
    /// </summary>
    private (Charm? Charm, string? Error) TryBuildCharm()
    {
        var skills = new List<CharmSkill>();
        var skillInputs = new[]
        {
            (Skill1Name, Skill1Lv),
            (Skill2Name, Skill2Lv),
            (Skill3Name, Skill3Lv),
        };

        foreach (var (nameBox, lvBox) in skillInputs)
        {
            var name = nameBox.Text?.Trim() ?? "";
            var lv = GetComboBoxValue(lvBox);
            if (name != "" && lv > 0)
                skills.Add(new CharmSkill(name, lv));
        }

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
            return (RarityInference.WithInferredRarity(charm), null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var (charm, error) = TryBuildCharm();
        if (charm is null)
        {
            MessageBox.Show(error, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // ゲームとしてありうるかの検査(全入口共通のCharmValidator)。Errorは保存しない。
        // Warning(スキル候補一覧にない名前)は確認して、はいなら保存する
        var issues = CharmValidator.Validate(charm);
        var firstError = issues.FirstOrDefault(i => i.Severity == CharmIssueSeverity.Error);
        if (firstError is not null)
        {
            var message = charm.Skills.Count == 0 ? "スキルを1つ以上入力してください。" : firstError.Message;
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
