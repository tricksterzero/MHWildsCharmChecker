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
        }
        else
        {
            Title = "護石の手動入力";
        }
    }

    private void LoadFromCharm(Charm charm)
    {
        var skillNames = new[] { Skill1Name, Skill2Name, Skill3Name };
        var skillLvs = new[] { Skill1Lv, Skill2Lv, Skill3Lv };

        for (int i = 0; i < charm.Skills.Count && i < 3; i++)
        {
            skillNames[i].Text = charm.Skills[i].Name;
            SetComboBoxIndex(skillLvs[i], charm.Skills[i].Lv);
        }

        var armorSorted = charm.ArmorSlots.Where(v => v > 0).OrderByDescending(v => v).ToList();
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
        var charm = BuildCharm();
        if (charm is null)
        {
            InferredRarityText.Text = "-";
            return;
        }
        var rarity = RarityInference.Infer(charm);
        InferredRarityText.Text = rarity.HasValue ? $"RARE {rarity}" : "-";
    }

    private Charm? BuildCharm()
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

        var charm = new Charm
        {
            Skills = skills,
            ArmorSlots = armorSlots,
            WeaponSlots = weaponSlots,
            Source = CharmSource.Manual,
            SourceTimestamp = DateTime.Now,
        };
        charm.Rarity = RarityInference.Infer(charm);
        return charm;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var charm = BuildCharm();
        if (charm is null || charm.Skills.Count == 0)
        {
            MessageBox.Show("スキルを1つ以上入力してください。", "入力エラー",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // スキル名の検証
        foreach (var skill in charm.Skills)
        {
            if (!_skillNames.Contains(skill.Name))
            {
                var result = MessageBox.Show(
                    $"「{skill.Name}」はスキル候補一覧にありません。このまま保存しますか？",
                    "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }
        }

        ResultCharm = charm;
        DialogResult = true;
    }
}
