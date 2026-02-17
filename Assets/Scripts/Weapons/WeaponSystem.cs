using UnityEngine;
using Photon.Pun;
using System.Collections;

public class WeaponController : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Setup")]
    public WeaponData currentWeapon;
    public Transform weaponHolder; // Empty GameObject under camera for weapon model
    public Transform firePoint; // Muzzle position
    public Camera playerCamera;
    
    [Header("State")]
    private int _currentAmmo;
    private bool _isReloading;
    private float _nextFireTime;
    private float _currentRecoil;
    
    [Header("Input")]
    private InputSystem_Actions _inputActions;
    private bool _isShooting;
    
    public bool testing;
    private PhotonView _photonView;

    private void Awake()
    {
        _inputActions = new InputSystem_Actions();
        
        // Fire (hold for automatic)
        _inputActions.Player.Attack.performed += ctx => _isShooting = true;
        _inputActions.Player.Attack.canceled += ctx => _isShooting = false;
        
        // Reload
        _inputActions.Player.Reload.performed += ctx => StartReload();
        
        _photonView = GetComponent<PhotonView>();
        
        if (currentWeapon != null)
        {
            _currentAmmo = currentWeapon.magazineSize;
        }
    }

    public override void OnEnable()
    {
        _inputActions.Player.Enable();
    }

    public override void OnDisable()
    {
        _inputActions.Player.Disable();
    }

    private void Update()
    {
        // Only process input if this is MY player
        if (!testing && !_photonView.IsMine) return;
        
        // Handle shooting
        if (_isShooting && !_isReloading && Time.time >= _nextFireTime && _currentAmmo > 0)
        {
            Fire();
        }
        
        // Auto-reload when empty
        if (_currentAmmo <= 0 && !_isReloading)
        {
            StartReload();
        }
        
        // Recoil recovery
        if (_currentRecoil > 0)
        {
            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * currentWeapon.recoilRecoverySpeed);
            ApplyRecoil();
        }
    }

    private void Fire()
    {
        _nextFireTime = Time.time + currentWeapon.fireRate;
        _currentAmmo--;
        
        // Apply recoil
        _currentRecoil += currentWeapon.recoilKickback;
        
        // Raycast from camera center
        RaycastHit hit;
        Vector3 shootDirection = playerCamera.transform.forward;
        
        if (Physics.Raycast(playerCamera.transform.position, shootDirection, out hit, currentWeapon.range))
        {
            // Calculate damage (with crit chance)
            float finalDamage = currentWeapon.damage;
            if (Random.value < currentWeapon.critChance)
            {
                finalDamage *= currentWeapon.critMultiplier;
                Debug.Log($"CRITICAL HIT! {finalDamage} damage");
            }
            
            // Deal damage to target
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                if (testing || _photonView.IsMine)
                {
                    // Send damage via RPC
                    PhotonView targetView = hit.collider.GetComponent<PhotonView>();
                    if (targetView != null)
                    {
                        targetView.RPC("TakeDamage", RpcTarget.All, finalDamage, _photonView.ViewID);
                    }
                }
            }
            
            // Spawn impact effect
            if (currentWeapon.impactEffectPrefab != null)
            {
                GameObject impact = Instantiate(currentWeapon.impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 1f);
            }
        }
        
        // Visual/audio feedback
        PlayMuzzleFlash();
        PlayFireSound();
        
        // Network sync (tell other clients you shot)
        if (!testing && _photonView.IsMine)
        {
            _photonView.RPC("RPC_FireEffect", RpcTarget.Others);
        }
    }

    [PunRPC]
    private void RPC_FireEffect()
    {
        // Remote players see/hear your shot
        PlayMuzzleFlash();
        PlayFireSound();
    }

    private void PlayMuzzleFlash()
    {
        if (currentWeapon.muzzleFlashPrefab != null && firePoint != null)
        {
            GameObject flash = Instantiate(currentWeapon.muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
            Destroy(flash, 0.1f);
        }
    }

    private void PlayFireSound()
    {
        if (currentWeapon.fireSound != null)
        {
            AudioSource.PlayClipAtPoint(currentWeapon.fireSound, firePoint.position, 0.5f);
        }
    }

    private void ApplyRecoil()
    {
        // Kick camera up (your PlayerController handles camera rotation)
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            // You'll need to add a public method to PlayerController:
            // public void AddRecoil(float amount)
            // controller.AddRecoil(_currentRecoil);
        }
    }

    private void StartReload()
    {
        if (_isReloading || _currentAmmo == currentWeapon.magazineSize) return;
        
        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        _isReloading = true;
        Debug.Log("Reloading...");
        
        yield return new WaitForSeconds(currentWeapon.reloadTime);
        
        _currentAmmo = currentWeapon.magazineSize;
        _isReloading = false;
        Debug.Log("Reload complete!");
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
