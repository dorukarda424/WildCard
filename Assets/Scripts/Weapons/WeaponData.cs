using UnityEngine;

/// <summary>
/// ScriptableObject for weapon configuration.
/// Create via Assets → Create → WildCard → Weapon Data.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "WildCard/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Info")]
    public string weaponName;
    public Sprite icon;

    [Header("Damage")]
    public float damage = 20f;
    public float fireRate = 5f;           // shots per second
    public float range = 100f;

    [Header("Ammo")]
    public int magazineSize = 8;
    public float reloadTime = 1.5f;

    [Header("Recoil")]
    public float recoilKickback = 1f;
    public float recoilRecoverySpeed = 5f;

    [Header("Critical")]
    public float critChance = 0.1f;       // 0-1
    public float critMultiplier = 2f;

    [Header("Visuals")]
    public GameObject muzzleFlashPrefab;
    public GameObject impactEffectPrefab;
    public AudioClip fireSound;
}
