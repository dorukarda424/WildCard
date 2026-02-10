using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 20f;
    public float lifeTime = 5f;
    public int damage = 10;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false; // Bullets usually travel straight
            // Since the bullet prefab is rotated 90 degrees to face forward, its local 'Up' vector points forward.
            rb.linearVelocity = transform.up * speed;
        }

        // Destroy bullet after lifeTime seconds to prevent clutter
        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check for PlayerHealth component on the hit object
        PlayerHealth target = collision.gameObject.GetComponent<PlayerHealth>();
        if (target != null)
        {
            target.TakeDamage(damage);
        }

        // Destroy bullet on impact
        Destroy(gameObject);
    }
}
