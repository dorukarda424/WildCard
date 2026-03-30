using UnityEngine;

/// <summary>
/// ScriptableObject defining a single card.
/// Create via Assets → Create → WildCard → Card Data.
/// </summary>
[CreateAssetMenu(fileName = "NewCard", menuName = "WildCard/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Display")]
    public string cardName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public CardRarity rarity = CardRarity.Common;

    [Header("Stat Modifiers")]
    public StatModifier[] modifiers;

    [Header("Special Effects")]
    public SpecialEffect specialEffects = SpecialEffect.None;

    /// <summary>
    /// Unique identifier derived from the asset name.
    /// Used for network synchronization (send ID instead of full data).
    /// </summary>
    public string CardId => name;

    /// <summary>
    /// Builds a formatted description from the stat modifiers and special effects.
    /// </summary>
    public string GetFullDescription()
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine(description);
            sb.AppendLine();
        }

        foreach (var mod in modifiers)
        {
            sb.AppendLine(mod.GetDescription());
        }

        if (specialEffects != SpecialEffect.None)
        {
            sb.AppendLine($"<b>Special:</b> {specialEffects}");
        }

        return sb.ToString().TrimEnd();
    }
}
