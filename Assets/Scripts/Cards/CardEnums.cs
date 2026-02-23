/// <summary>
/// All modifiable player stat types for the card system.
/// </summary>
public enum StatType
{
    Health,
    Damage,
    FireRate,
    MaxAmmo,
    MoveSpeed,
    BulletSpeed,
    ReloadSpeed
}

/// <summary>
/// How a stat modifier is applied.
/// </summary>
public enum ModifierMode
{
    /// <summary>Added directly to the base value.</summary>
    Flat,
    /// <summary>Multiplied with the base value (0.1 = +10%).</summary>
    Percentage
}

/// <summary>
/// Card rarity tiers — affects selection weight and visual presentation.
/// </summary>
public enum CardRarity
{
    Common,
    Rare,
    Legendary
}

/// <summary>
/// Special card effects beyond simple stat changes.
/// Stored as flags so a player can have multiple active effects.
/// </summary>
[System.Flags]
public enum SpecialEffect
{
    None            = 0,
    HomingBullets   = 1 << 0,
    ExplosiveBullets= 1 << 1,
    Shield          = 1 << 2,
    LifeSteal       = 1 << 3,
    DoubleJump      = 1 << 4,
    Ricochet        = 1 << 5
}
