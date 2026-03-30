using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "WildCard/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Identification")]
    public string weaponName = "Rifle";
    public int weaponID; // For network sync
    
    [Header("Combat Stats")]
    public float damage = 25f;
    public float fireRate = 0.1f; // Time between shots
    public int magazineSize = 30;
    public float reloadTime = 2f;
    public float range = 100f;
    
    [Header("Recoil")]
    public float recoilKickback = 0.5f; // Camera pitch per shot
    public float recoilRecoverySpeed = 5f;
    
    [Header("Audio/Visual")]
    public AudioClip fireSound;
    public GameObject muzzleFlashPrefab;
    public GameObject impactEffectPrefab;
    
    [Header("Roguelike Modifiers")]
    public WeaponRarity rarity;
    public float critChance = 0.1f; // 10%
    public float critMultiplier = 2f;
}

public enum WeaponRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}
