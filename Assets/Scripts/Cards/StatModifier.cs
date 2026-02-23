/// <summary>
/// A single stat change applied by a card.
/// Serializable so it shows up in the Unity Inspector.
/// </summary>
[System.Serializable]
public struct StatModifier
{
    public StatType statType;
    public ModifierMode mode;
    public float value;

    public StatModifier(StatType type, ModifierMode mode, float value)
    {
        this.statType = type;
        this.mode = mode;
        this.value = value;
    }

    /// <summary>
    /// Returns a human-readable description like "+25 Health" or "+20% Fire Rate".
    /// </summary>
    public string GetDescription()
    {
        string sign = value >= 0 ? "+" : "";
        string suffix = mode == ModifierMode.Percentage ? "%" : "";
        float displayValue = mode == ModifierMode.Percentage ? value * 100f : value;
        return $"{sign}{displayValue:0}{suffix} {statType}";
    }
}
