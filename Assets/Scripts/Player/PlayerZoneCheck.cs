using UnityEngine;
using Photon.Pun;

public class PlayerZoneCheck : MonoBehaviourPun
{
    [Header("zoneCylinder")]
    [Tooltip("Auto-found at runtime if left empty (player is spawned dynamically)")]
    public Transform zoneCylinder;

    [Header("damage Settings")]
    public float damagePerTick = 5f;

    private PlayerHealth _playerHealth;
    private float _damageTimer = 0f;
    private bool _isLocalPlayer;

    /// <summary>
    /// True when this player is outside the safe zone. Used by ZoneGasOverlay for screen effects.
    /// </summary>
    public bool IsOutsideZone { get; private set; }

    void Start()
    {
        _playerHealth = GetComponent<PlayerHealth>();

        // Cache local player check (same pattern as PlayerCombat)
        _isLocalPlayer = !PhotonNetwork.InRoom
                      || (photonView != null && photonView.IsMine);

        // Player is spawned at runtime via PhotonNetwork.Instantiate,
        // so the prefab can't hold a reference to a scene object.
        // Auto-find the zone cylinder if not assigned.
        if (zoneCylinder == null)
        {
            var zoneController = FindObjectOfType<ZoneController>();
            if (zoneController != null)
            {
                zoneCylinder = zoneController.transform;
                Debug.Log($"[PlayerZoneCheck] Auto-found zone cylinder: {zoneCylinder.name}");
            }
        }
    }

    void Update()
    {
        if (!_isLocalPlayer || _playerHealth == null || _playerHealth.IsDead)
        {
            IsOutsideZone = false;
            return;
        }

        if (zoneCylinder == null)
        {
            IsOutsideZone = false;
            return;
        }

        float currentRadius = zoneCylinder.localScale.x / 2f;

        float distanceToCenter = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(zoneCylinder.position.x, zoneCylinder.position.z)
        );

        IsOutsideZone = distanceToCenter > currentRadius;

        if (IsOutsideZone)
        {
            _damageTimer += Time.deltaTime;

            if (_damageTimer >= 1f)
            {
                // Use local damage — zone damage doesn't need RPC
                _playerHealth.TakeDamageLocal(damagePerTick);
                _damageTimer = 0f;
            }
        }
        else
        {
            _damageTimer = 0f;
        }
    }
}
