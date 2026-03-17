using UnityEngine;
using Photon.Pun;
using System;

[RequireComponent(typeof(PlayerStats))]
public class PlayerCombat : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Weapon Visuals / Audio")]
    public WeaponData currentWeapon;

    public Transform weaponHolder;
    [SerializeField] private Transform firePoint;

    [Header("Bullet")] 
    [SerializeField] private string bulletPrefabName = "NetworkBullet";
    [SerializeField] private GameObject bulletPrefab;
    
    [Header("Audio")] [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptyClipSound;
    private AudioSource _audioSource;

    [Header("Debug")] public bool testing;

    public event Action<float> OnDamageDealt;
    public event Action OnKill;

    private int _currentAmmo;
    private bool _isReloading;
    private float _reloadTimer;
    private float _fireCooldown;
    private float _currentRecoil;
    private bool _isShooting;
    private bool _isReloadPressed;

    private PlayerStats _stats;
    private PlayerCamera _playerCamera;
    private AudioSource _as;

    public int CurrentAmmo => _currentAmmo;
    public int MaxAmmo => _stats != null ? _stats.MaxAmmo : 8;
    public bool IsReloading => _isReloading;

    public float ReloadProgress => (_isReloading && _stats != null && _stats.ReloadSpeed > 0)
        ? _reloadTimer / _stats.ReloadSpeed
        : 0f;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _playerCamera = GetComponent<PlayerCamera>();

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
        HandleReload();
        HandleShooting();
        HandleRecoilDecay();
    }

    private void ReadInput()
    {
        if (InputManager.Instance == null) return;

        _isShooting = InputManager.Instance.IsShooting;

    if (InputManager.Instance.IsReloadPressed)
        {
            _isReloadPressed = true;
            InputManager.Instance.ResetReload();
        }
    }

    private void HandleReload()
    {
        if (_isReloadPressed)
        {
            _isReloadPressed = false;
            if (_currentAmmo < MaxAmmo && !_isReloading)
                StartReload();
        }

        if (_isReloading)
        {
            _reloadTimer += Time.deltaTime;
            float reloadTime = _stats != null ? _stats.ReloadSpeed : 1.5f;
            if (_reloadTimer >= reloadTime)
                FinishReload();
        }
    }

    private void HandleShooting()
    {
        if (_isReloading) return;

        if (_isShooting && _fireCooldown <= 0f)
        {
            if (_currentAmmo > 0)
                Shoot();
            else
                StartReload();
        }
    }

    private void Shoot()
    {
        if (firePoint == null)
        {
            Debug.LogWarning("[PlayerCombat] firePoint is not assigned!");
            return;
        }

        Camera cam = _playerCamera != null ? _playerCamera.GetCamera() : Camera.main;
        if (cam != null)
        {
            // Ekranın tam ortasından ileriye bir ışın gönder (Crosshair'ın olduğu yer)
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 targetPoint;

            // Eğer ışın bir yere çarpıyorsa (ör. duvar, düşman), hedef o noktadır
            // Kendi collider'ınıza çarpmaması için Raycast'e layer mask ekleyebilirsiniz gerekirse.
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                targetPoint = hit.point;
            }
            else
            {
                // Hiçbir yere çarpmadıysa 1000 birim ilerideki noktayı hedef al
                targetPoint = ray.GetPoint(1000f);
            }

            // firePoint'i hedefe doğru çevir
            firePoint.rotation = Quaternion.LookRotation(targetPoint - firePoint.position);
        }

        float fireRate = _stats != null ? _stats.FireRate : 0.3f;
        _fireCooldown = fireRate;
        _currentAmmo--;

        float damage = _stats != null ? _stats.Damage : 20f;
        float bulletSpeed = _stats != null ? _stats.BulletSpeed : 40f;

        if (currentWeapon != null && UnityEngine.Random.value < currentWeapon.critChance)
        {
            damage *= currentWeapon.critMultiplier;
            Debug.Log($"[PlayerCombat] CRITICAL HIT! {damage} damage");
        }
        
        if (_playerCamera != null)
            _playerCamera.AddRecoil(currentWeapon.recoilKickback);

        if (!testing && photonView != null)
        {
            // Owner null ise -1 vererek hatanın önüne geçiyoruz
            int actorNumber = photonView.Owner != null ? photonView.Owner.ActorNumber : -1;

            object[] data = new object[]
            {
                damage,
                bulletSpeed,
                _stats != null && _stats.HasEffect(SpecialEffect.HomingBullets),
                _stats != null && _stats.HasEffect(SpecialEffect.ExplosiveBullets),
                _stats != null && _stats.HasEffect(SpecialEffect.Ricochet),
                _stats != null && _stats.HasEffect(SpecialEffect.LifeSteal),
                actorNumber
            };
            
            PhotonNetwork.Instantiate(bulletPrefabName, firePoint.position, firePoint.rotation, 0, data);

            photonView.RPC(nameof(RPC_FireEffect), RpcTarget.Others);
        }
        else
        {
            Debug.Log($"[PlayerCombat] TEST FIRE — damage: {damage}, ammo left: {_currentAmmo}");
        }

        PlayMuzzleFlash();
        PlaySound(shootSound);
        OnDamageDealt?.Invoke(damage);
    }

    [PunRPC]
    private void RPC_FireEffect()
    {
        PlayMuzzleFlash();
        PlaySound(shootSound);
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

    private void HandleRecoilDecay()
    {
        if (_currentRecoil <= 0f) return;
        float recoverySpeed = currentWeapon != null ? currentWeapon.recoilRecoverySpeed : 5f;
        _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * recoverySpeed);
        if (_currentRecoil < 0.01f) _currentRecoil = 0f;
    }

    public void NotifyKill()
    {
        OnKill?.Invoke();
        Debug.Log("[PlayerCombat] Kill confirmed — OnKill event fired");
    }

    public void RefillAmmo()
    {
        _currentAmmo = MaxAmmo;
        _isReloading = false;
        _reloadTimer = 0f;
    }

    private void PlayMuzzleFlash()
    {
        if (currentWeapon != null && currentWeapon.muzzleFlashPrefab != null && firePoint != null)
        {
            GameObject flash = Instantiate(currentWeapon.muzzleFlashPrefab,
                firePoint.position,
                firePoint.rotation,
                firePoint);
            Destroy(flash, 0.1f);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
            _audioSource.PlayOneShot(clip);
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
