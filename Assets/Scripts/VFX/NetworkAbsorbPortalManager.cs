using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Manages the Black Hole ability for one player.
/// Press Ability1 (Z) to spawn a portal in front of the camera.
/// Press again while the portal has absorbed bullets to release stored attacks.
/// Requires the BlackHole SpecialEffect from the card system.
/// </summary>
public class NetworkAbsorbPortalManager : MonoBehaviourPunCallbacks
{
    [Header("Portal Settings")]
    [SerializeField] private string portalPrefabName = "NetworkAbsorbPortal";
    [SerializeField] private string bulletPrefabName = "NetworkBullet";
    [SerializeField] private float spawnDistance = 4f;
    [SerializeField] private float cooldown = 10f;

    [Header("Release Settings")]
    [SerializeField] private float releaseBurstDelay = 0.1f;
    [SerializeField] private float releaseSpreadAngle = 5f;
    [SerializeField] private float releaseBulletSpeed = 50f;

    private NetworkAbsorbPortal _currentPortal;
    private List<float> _absorbedDamages = new List<float>();
    private float _cooldownTimer;

    private PlayerStats _stats;
    private PlayerCamera _playerCamera;

    public bool HasPortalActive => _currentPortal != null;
    public int AbsorbedCount => _absorbedDamages.Count;
    public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _playerCamera = GetComponent<PlayerCamera>();
    }

    private void Update()
    {
        if (photonView != null && !photonView.IsMine) return;

        _cooldownTimer -= Time.deltaTime;

        if (!CanUseAbility()) return;

        if (InputManager.Instance != null && InputManager.Instance.IsAbility1Pressed)
        {
            InputManager.Instance.ConsumeAbility1();
            HandlePortalToggle();
        }
    }

    private bool CanUseAbility()
    {
        if (_stats == null) return false;
        if (!_stats.HasEffect(SpecialEffect.BlackHole)) return false;
        if (_cooldownTimer > 0f && _currentPortal == null) return false;
        return true;
    }

    private void HandlePortalToggle()
    {
        // If portal exists and has absorbed attacks, release them
        if (_currentPortal != null && _absorbedDamages.Count > 0)
        {
            ReleaseAttacks();
        }
        // If portal exists but hasn't absorbed anything, destroy and respawn
        else if (_currentPortal != null)
        {
            DestroyCurrentPortal();
            SpawnNewPortal();
        }
        else
        {
            SpawnNewPortal();
        }
    }

    private void SpawnNewPortal()
    {
        Camera cam = _playerCamera != null ? _playerCamera.GetCamera() : Camera.main;
        if (cam == null) return;

        _absorbedDamages.Clear();

        Vector3 spawnPos = cam.transform.position + cam.transform.forward * spawnDistance;
        Quaternion spawnRot = cam.transform.rotation;

        int actorNumber = photonView.Owner != null ? photonView.Owner.ActorNumber : -1;
        object[] data = new object[] { actorNumber };
        GameObject portalObj = PhotonNetwork.Instantiate(portalPrefabName, spawnPos, spawnRot, 0, data);
        _currentPortal = portalObj.GetComponent<NetworkAbsorbPortal>();

        Debug.Log("[AbsorbPortalManager] Portal spawned! Shoot it to store attacks.");
    }

    private void ReleaseAttacks()
    {
        Camera cam = _playerCamera != null ? _playerCamera.GetCamera() : Camera.main;
        if (cam == null) return;

        Debug.Log($"[AbsorbPortalManager] Releasing {_absorbedDamages.Count} absorbed attacks!");

        DestroyCurrentPortal();
        StartCoroutine(ReleaseRoutine(cam));
    }

    private IEnumerator ReleaseRoutine(Camera cam)
    {
        int actorNumber = photonView.Owner != null ? photonView.Owner.ActorNumber : -1;

        foreach (float dmg in _absorbedDamages)
        {
            Vector3 spawnPos = cam.transform.position + cam.transform.forward * 2f;

            // Slight spread for burst effect
            Quaternion spreadRot = cam.transform.rotation * Quaternion.Euler(
                Random.Range(-releaseSpreadAngle, releaseSpreadAngle),
                Random.Range(-releaseSpreadAngle, releaseSpreadAngle),
                0f);

            object[] data = new object[]
            {
                dmg,                // damage
                releaseBulletSpeed, // speed
                false,              // homing
                false,              // explosive
                false,              // ricochet
                false,              // life steal
                actorNumber         // owner
            };

            PhotonNetwork.Instantiate(bulletPrefabName, spawnPos, spreadRot, 0, data);

            yield return new WaitForSeconds(releaseBurstDelay);
        }

        _absorbedDamages.Clear();
        _cooldownTimer = cooldown;
    }

    private void DestroyCurrentPortal()
    {
        if (_currentPortal != null)
        {
            if (_currentPortal.photonView.IsMine)
            {
                PhotonNetwork.Destroy(_currentPortal.gameObject);
            }
            _currentPortal = null;
        }
    }

    /// <summary>
    /// Called by NetworkBullet when it gets absorbed by our portal.
    /// </summary>
    public void OnBulletAbsorbed(float damage)
    {
        _absorbedDamages.Add(damage);
        Debug.Log($"[AbsorbPortalManager] Absorbed bullet! Total stored: {_absorbedDamages.Count}");
    }
}
