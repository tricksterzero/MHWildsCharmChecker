using CharmChecker.Core.Model;

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

        RarityText = CharmDisplayText.Rarity(charm);
        SkillText = CharmDisplayText.Skills(charm, " / ");
        SlotText = CharmDisplayText.Slots(charm);

        DateText = charm.SourceTimestamp.ToString("yyyy-MM-dd");
        DateTimeText = charm.SourceTimestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
