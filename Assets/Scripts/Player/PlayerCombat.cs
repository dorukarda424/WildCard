using UnityEngine;
using Photon.Pun;
using System.Collections;
using System;

public class PlayerCombat : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Setup")]
    public WeaponData currentWeapon;
    public Transform weaponHolder;
    public Transform firePoint;
    public Camera playerCamera;

    [Header("State")]
    private int _currentAmmo;
    private bool _isReloading;
    private float _nextFireTime;
    private float _currentRecoil;
    private bool _isShooting;

    [Header("Debug")]
    public bool testing;
    
    public event Action<float> OnDamageDealt;
    public event Action OnKill;

    private PhotonView _photonView;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();

        if (currentWeapon != null)
            _currentAmmo = currentWeapon.magazineSize;
    }

    private void Update()
    {
        if (!testing && (photonView == null || !photonView.IsMine)) return;

        ReadInput();

        if (_isShooting && !_isReloading && Time.time >= _nextFireTime && _currentAmmo > 0)
            Fire();

        if (_currentAmmo <= 0 && !_isReloading)
            StartReload();

        if (_currentRecoil > 0)
        {
            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * currentWeapon.recoilRecoverySpeed);
            ApplyRecoil();
        }
    }

    private void ReadInput()
    {
        if (InputManager.Instance == null) return;

        _isShooting = InputManager.Instance.IsShooting;

        if (InputManager.Instance.IsReloadPressed)
        {
            StartReload();
            InputManager.Instance.ResetReload();
        }
    }

    private void Fire()
    {
        _nextFireTime = Time.time + 1f / currentWeapon.fireRate;
        _currentAmmo--;
        _currentRecoil += currentWeapon.recoilKickback;

        RaycastHit hit;
        Vector3 shootDirection = playerCamera.transform.forward;

        if (Physics.Raycast(playerCamera.transform.position, shootDirection, out hit, currentWeapon.range))
        {
            float finalDamage = currentWeapon.damage;
            if (UnityEngine.Random.value < currentWeapon.critChance)
            {
                finalDamage *= currentWeapon.critMultiplier;
                Debug.Log($"CRITICAL HIT! {finalDamage} damage");
            }
            
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                PhotonView targetView = hit.collider.GetComponent<PhotonView>();
                if (targetView != null)
                {
                    targetView.RPC("TakeDamage", RpcTarget.All, finalDamage, _photonView.ViewID);
                    OnDamageDealt?.Invoke(finalDamage);
                }
            }
            
            if (currentWeapon.impactEffectPrefab != null)
            {
                GameObject impact = Instantiate(currentWeapon.impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 1f);
            }
        }

        PlayMuzzleFlash();
        PlayFireSound();

        if (!testing && _photonView.IsMine)
            _photonView.RPC("RPC_FireEffect", RpcTarget.Others);
    }

    [PunRPC]
    private void RPC_FireEffect()
    {
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
            AudioSource.PlayClipAtPoint(currentWeapon.fireSound, firePoint.position, 0.5f);
    }

    private void ApplyRecoil()
    {
        // Hook into PlayerCamera.AddRecoil() once PlayerCamera is built
        // PlayerCamera cam = GetComponent<PlayerCamera>();
        // if (cam != null) cam.AddRecoil(_currentRecoil);
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
