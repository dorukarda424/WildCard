using UnityEngine;


public class ShootingTarget : MonoBehaviour
{
    [Header("Spawn Area")]
    [Tooltip("The BoxCollider (set as Trigger) that defines the area where the target can spawn.")]
    [SerializeField] private BoxCollider spawnZone;

    [Header("Target Settings")]
    [Tooltip("Health of the target before it respawns.")]
    [SerializeField] private float maxHealth = 50f;
    [Tooltip("Seconds the target stays invisible before respawning.")]
    [SerializeField] private float respawnDelay = 1f;
    [Tooltip("If true, the target moves to a new random position on each respawn. If false, it resets in place.")]
    [SerializeField] private bool randomizePosition = true;

    [Header("Visual Feedback")]
    [Tooltip("Optional particle effect spawned on hit.")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("Optional particle effect spawned on destruction.")]
    [SerializeField] private GameObject destroyEffectPrefab;

    private float _currentHealth;
    private Renderer[] _renderers;
    private Collider _collider;

    private void Start()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _collider = GetComponent<Collider>();
        _currentHealth = maxHealth;

        // Spawn at initial random position inside the box
        if (randomizePosition && spawnZone != null)
        {
            transform.position = GetRandomPositionInBox();
        }
    }

    /// <summary>
    /// Called when a bullet collides with this target.
    /// NetworkBullet uses OnCollisionEnter and checks for IDamageable/PlayerHealth,
    /// but won't find either on this object — so the bullet just gets destroyed on impact.
    /// We detect the hit via OnCollisionEnter from the target's side.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // Check if the thing that hit us is a bullet
        var bullet = collision.gameObject.GetComponent<NetworkBullet>();
        if (bullet == null) return;

        // Use reflection or just a fixed damage since bullet._damage is private
        // We'll just count every hit as "one hit" and subtract a fixed amount
        TakeHit(maxHealth / 2f); // 2 hits to destroy (adjustable via maxHealth)
    }

    /// <summary>
    /// Public method so other systems can also damage the target if needed.
    /// </summary>
    public void TakeHit(float damage)
    {
        if (_currentHealth <= 0f) return; // Already dead

        _currentHealth -= damage;

        // Spawn hit effect
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        if (_currentHealth <= 0f)
        {
            DestroyTarget();
        }
    }

    private void DestroyTarget()
    {
        // Spawn destroy effect
        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }

        // Hide the target
        SetVisible(false);

        // Respawn after delay
        Invoke(nameof(Respawn), respawnDelay);
    }

    private void Respawn()
    {
        _currentHealth = maxHealth;

        if (randomizePosition && spawnZone != null)
        {
            transform.position = GetRandomPositionInBox();
        }

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in _renderers)
        {
            if (r != null) r.enabled = visible;
        }

        if (_collider != null)
        {
            _collider.enabled = visible;
        }
    }

    /// <summary>
    /// Returns a random world-space position inside the BoxCollider bounds.
    /// </summary>
    private Vector3 GetRandomPositionInBox()
    {
        if (spawnZone == null) return transform.position;

        // Get the box's local-space center and size
        Vector3 center = spawnZone.center;
        Vector3 size = spawnZone.size;

        // Random point in local space
        float x = Random.Range(center.x - size.x / 2f, center.x + size.x / 2f);
        float y = Random.Range(center.y - size.y / 2f, center.y + size.y / 2f);
        float z = center.z-1;

        // Convert to world space using the BoxCollider's transform
        return spawnZone.transform.TransformPoint(new Vector3(x, y, z));
    }
}
