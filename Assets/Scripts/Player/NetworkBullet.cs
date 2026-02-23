using UnityEngine;
using Photon.Pun;

/// <summary>
/// Networked bullet with support for card effects:
/// homing, explosive, ricochet, and life steal.
/// Instantiation data is passed from PlayerCombat.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class NetworkBullet : MonoBehaviourPunCallbacks
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("Homing Settings")]
    [SerializeField] private float homingStrength = 5f;
    [SerializeField] private float homingSearchRadius = 30f;

    [Header("Ricochet Settings")]
    [SerializeField] private int maxRicochets = 2;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 4f;

    // Set from instantiation data
    private float _damage;
    private float _speed;
    private bool _isHoming;
    private bool _isExplosive;
    private bool _isRicochet;
    private bool _isLifeSteal;
    private int _ownerActorNumber;
    private int _ricochetCount;

    private Rigidbody _rb;
    private Transform _homingTarget;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Parse instantiation data
        if (photonView.InstantiationData != null && photonView.InstantiationData.Length >= 7)
        {
            object[] data = photonView.InstantiationData;
            _damage           = (float)data[0];
            _speed            = (float)data[1];
            _isHoming         = (bool)data[2];
            _isExplosive      = (bool)data[3];
            _isRicochet       = (bool)data[4];
            _isLifeSteal      = (bool)data[5];
            _ownerActorNumber = (int)data[6];
        }
        else
        {
            // Fallback defaults
            _damage = 20f;
            _speed = 40f;
            _ownerActorNumber = -1;
        }
    }

    private void Start()
    {
        // Initial velocity
        _rb.linearVelocity = transform.forward * _speed;

        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (_isHoming)
        {
            UpdateHoming();
        }
    }

    // ────────── Homing ──────────

    private void UpdateHoming()
    {
        if (_homingTarget == null || !_homingTarget.gameObject.activeInHierarchy)
        {
            FindHomingTarget();
            if (_homingTarget == null) return;
        }

        Vector3 direction = (_homingTarget.position - transform.position).normalized;
        Vector3 newVelocity = Vector3.Lerp(_rb.linearVelocity.normalized, direction, homingStrength * Time.fixedDeltaTime);
        _rb.linearVelocity = newVelocity.normalized * _speed;
        transform.forward = _rb.linearVelocity.normalized;
    }

    private void FindHomingTarget()
    {
        float closestDist = homingSearchRadius;
        Transform closest = null;

        // Find all players and pick the closest enemy
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.IsDead) continue;
            if (player.photonView.Owner.ActorNumber == _ownerActorNumber) continue;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = player.transform;
            }
        }

        _homingTarget = closest;
    }

    // ────────── Collision ──────────

    private void OnCollisionEnter(Collision collision)
    {
        // Don't damage self
        var targetHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (targetHealth != null)
        {
            if (targetHealth.photonView.Owner.ActorNumber == _ownerActorNumber) return;

            // Only the bullet owner processes damage (authoritative)
            if (photonView.IsMine)
            {
                targetHealth.TakeDamageFromNetwork(_damage, _ownerActorNumber);

                // Life steal: heal the attacker
                if (_isLifeSteal)
                {
                    var ownerHealth = FindOwnerHealth();
                    if (ownerHealth != null)
                    {
                        ownerHealth.Heal(_damage * 0.2f); // 20% life steal
                    }
                }
            }
        }

        // Explosive: AoE damage
        if (_isExplosive && photonView.IsMine)
        {
            ExplodeAoE();
        }

        // Ricochet: bounce off surfaces
        if (_isRicochet && targetHealth == null && _ricochetCount < maxRicochets)
        {
            _ricochetCount++;
            Vector3 reflected = Vector3.Reflect(_rb.linearVelocity.normalized, collision.contacts[0].normal);
            _rb.linearVelocity = reflected * _speed;
            transform.forward = reflected;
            return; // Don't destroy on ricochet
        }

        // Spawn explosion effect
        if (explosionEffectPrefab != null && _isExplosive)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        // Destroy the bullet
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void ExplodeAoE()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            var health = hit.GetComponent<PlayerHealth>();
            if (health == null) continue;
            if (health.IsDead) continue;
            if (health.photonView.Owner.ActorNumber == _ownerActorNumber) continue;

            // Damage falloff based on distance
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            float falloff = 1f - (dist / explosionRadius);
            float aoeDamage = _damage * 0.6f * falloff; // 60% of base damage at center

            health.TakeDamageFromNetwork(aoeDamage, _ownerActorNumber);
        }
    }

    private PlayerHealth FindOwnerHealth()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.photonView.Owner.ActorNumber == _ownerActorNumber)
                return player;
        }
        return null;
    }
}
