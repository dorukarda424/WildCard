using UnityEngine;

public class SimpleBullet : MonoBehaviour
{
    public float damage = 10f;

    void OnCollisionEnter(Collision collision)
    {
        // Add impact effect logic here
        Destroy(gameObject);
    }
}
