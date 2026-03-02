using UnityEngine;
using Photon.Pun;
using System.Collections;
using System;

[RequireComponent(typeof(PlayerStats))]
public class PlayerCombat : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Weapon")]
    public WeaponData currentWeapon;
    public Transform weaponHolder;

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private string bulletPrefabName = "NetworkBullet";

    [Header("Audio")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptyClipSound;
    private AudioSource _audioSource;

    [Header("State")]
    private int _currentAmmo;
    private bool _isReloading;
    private float _reloadTimer;
    private float _fireCooldown;
    private float _currentRecoil;
    private bool _isShooting;

    [Header("Debug")]
    public bool testing;
    
    public event Action<float> OnDamageDealt;
    public event Action OnKill;
    
    private PlayerStats _stats;

    public int CurrentAmmo => _currentAmmo;
    public int MaxAmmo => _stats != null ? _stats.MaxAmmo : (currentWeapon != null ? currentWeapon.magazineSize : 8);
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _isReloading && _stats != null ? _reloadTimer / _stats.ReloadSpeed : 0f;
    
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
        if (!testing && (photonView == null || !photonView.IsMine)) return;

        _fireCooldown -= Time.deltaTime;

        ReadInput();
        
        if (_isReloading)
        {
            _reloadTimer += Time.deltaTime;
            if (_reloadTimer >= (_stats != null ? _stats.ReloadSpeed : (currentWeapon != null ? currentWeapon.reloadTime : 1.5f)))
            {
                FinishReload();
            }
            return;
        }
        
        if (_isShooting && _fireCooldown <= 0f)
        {
            if (_currentAmmo > 0)
            {
                Shoot();
            }
            else
            {
                StartReload();
            }
        }
        
        if (InputManager.Instance != null && InputManager.Instance.IsReloadPressed && _currentAmmo < MaxAmmo && !_isReloading)
        {
            StartReload();
            InputManager.Instance.ResetReload();
        }
        
        if (_currentRecoil > 0 && currentWeapon != null)
        {
            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * currentWeapon.recoilRecoverySpeed);
        }
    }

    private void ReadInput()
    {
        if (InputManager.Instance != null)
        {
            _isShooting = InputManager.Instance.IsShooting;
        }
        else
        {
            _isShooting = Input.GetButton("Fire1");
        }
    }

    private void Shoot()
    {
        if (firePoint == null) return;

        float fireRate = _stats != null ? _stats.FireRate : (currentWeapon != null ? 1f / currentWeapon.fireRate : 0.3f);
        _fireCooldown = fireRate;
        _currentAmmo--;
        
        if (currentWeapon != null)
        {
            _currentRecoil += currentWeapon.recoilKickback;
        }
        
        float damage = _stats != null ? _stats.Damage : (currentWeapon != null ? currentWeapon.damage : 20f);
        float bulletSpeed = _stats != null ? _stats.BulletSpeed : 40f;
        
        bool isCrit = false;
        if (currentWeapon != null && UnityEngine.Random.value < currentWeapon.critChance)
        {
            damage *= currentWeapon.critMultiplier;
            isCrit = true;
            Debug.Log($"[PlayerCombat] CRITICAL HIT! {damage} damage");
        }

        object[] instantiationData = new object[]
        {
            damage,
            bulletSpeed,
            _stats != null && _stats.HasEffect(SpecialEffect.HomingBullets),
            _stats != null && _stats.HasEffect(SpecialEffect.ExplosiveBullets),
            _stats != null && _stats.HasEffect(SpecialEffect.Ricochet),
            _stats != null && _stats.HasEffect(SpecialEffect.LifeSteal),
            photonView.Owner.ActorNumber
        };

        PhotonNetwork.Instantiate(
            bulletPrefabName,
            firePoint.position,
            firePoint.rotation,
            0,
            instantiationData
        );
        
        PlayMuzzleFlash();
        PlaySound(shootSound);
        
        if (!testing && photonView.IsMine)
            photonView.RPC(nameof(RPC_FireEffect), RpcTarget.Others);
    }

    [PunRPC]
    private void RPC_FireEffect()
    {
        PlayMuzzleFlash();
        PlaySound(shootSound);
    }

    private void PlayMuzzleFlash()
    {
        if (currentWeapon != null && currentWeapon.muzzleFlashPrefab != null && firePoint != null)
        {
            GameObject flash = Instantiate(currentWeapon.muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
            Destroy(flash, 0.1f);
        }
    }

    private void StartReload()
    {
        if (_isReloading || _currentAmmo == MaxAmmo) return;
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
    
    public void RefillAmmo()
    {
        _currentAmmo = MaxAmmo;
        _isReloading = false;
        _reloadTimer = 0f;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_currentAmmo);
            stream.SendNext(_isReloading);
        }
        else
        {
            _currentAmmo = (int)stream.ReceiveNext();
            _isReloading = (bool)stream.ReceiveNext();
        }
    }
}
