using UnityEngine;
using Photon.Pun;

/// <summary>
/// Handles shooting mechanics integrated with PlayerStats.
/// Fire rate, damage, ammo, and bullet speed all read from PlayerStats.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerCombat : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private string bulletPrefabName = "NetworkBullet";

    [Header("Audio/Visual")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptyClipSound;
    private AudioSource _audioSource;

    private PlayerStats _stats;
    private float _fireCooldown;
    private int _currentAmmo;
    private bool _isReloading;
    private float _reloadTimer;

    // ────────── Properties ──────────

    public int CurrentAmmo => _currentAmmo;
    public int MaxAmmo => _stats != null ? _stats.MaxAmmo : 8;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _isReloading ? _reloadTimer / _stats.ReloadSpeed : 0f;

    // ────────── Lifecycle ──────────

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        _currentAmmo = MaxAmmo;
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        _fireCooldown -= Time.deltaTime;

        // Handle reload
        if (_isReloading)
        {
            _reloadTimer += Time.deltaTime;
            if (_reloadTimer >= _stats.ReloadSpeed)
            {
                FinishReload();
            }
            return; // Can't shoot while reloading
        }

        // Shoot on left click
        if (Input.GetButton("Fire1") && _fireCooldown <= 0f)
        {
            if (_currentAmmo > 0)
            {
                Shoot();
            }
            else
            {
                // Auto-reload on empty click
                StartReload();
            }
        }

        // Manual reload
        if (Input.GetKeyDown(KeyCode.R) && _currentAmmo < MaxAmmo && !_isReloading)
        {
            StartReload();
        }
    }

    // ────────── Shooting ──────────

    private void Shoot()
    {
        if (firePoint == null) return;

        _fireCooldown = _stats.FireRate;
        _currentAmmo--;

        // Spawn bullet via Photon (visible to all players)
        object[] instantiationData = new object[]
        {
            _stats.Damage,
            _stats.BulletSpeed,
            _stats.HasEffect(SpecialEffect.HomingBullets),
            _stats.HasEffect(SpecialEffect.ExplosiveBullets),
            _stats.HasEffect(SpecialEffect.Ricochet),
            _stats.HasEffect(SpecialEffect.LifeSteal),
            photonView.Owner.ActorNumber
        };

        PhotonNetwork.Instantiate(
            bulletPrefabName,
            firePoint.position,
            firePoint.rotation,
            0,
            instantiationData
        );

        PlaySound(shootSound);
    }

    // ────────── Reload ──────────

    private void StartReload()
    {
        _isReloading = true;
        _reloadTimer = 0f;
        PlaySound(reloadSound);
        Debug.Log("[PlayerCombat] Reloading...");
    }

    private void FinishReload()
    {
        _isReloading = false;
        _currentAmmo = MaxAmmo;
        Debug.Log("[PlayerCombat] Reload complete.");
    }

    /// <summary>
    /// Called at round start to refill ammo.
    /// </summary>
    public void RefillAmmo()
    {
        _currentAmmo = MaxAmmo;
        _isReloading = false;
        _reloadTimer = 0f;
    }

    // ────────── Audio ──────────

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
}
