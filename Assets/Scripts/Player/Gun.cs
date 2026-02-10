using UnityEngine;

public class Gun : MonoBehaviour
{
    [Header("Gun Settings")]
    public float damage = 10f;
    public float range = 100f;
    public float fireRate = 10f; // Rounds per second
    public float impactForce = 30f;

    [Header("References")]
    public Camera fpsCamera;
    public ParticleSystem muzzleFlash;
    public GameObject impactEffect;
    
    // If using physical bullets:
    public GameObject bulletPrefab;
    public Transform firePoint;
    public bool usePhysicalBullets = true;

    private float nextTimeToFire = 0f;

    void Start()
    {
        if (fpsCamera == null)
        {
            fpsCamera = Camera.main;
        }
    }

    void Update()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (usePhysicalBullets)
        {
            if (bulletPrefab != null && firePoint != null)
            {
                // Instantiate the bullet with a 90-degree rotation on the X-axis to correct its orientation
                Instantiate(bulletPrefab, firePoint.position, firePoint.rotation * Quaternion.Euler(90f, 0f, 0f));
            }
        }
        else
        {
            // Raycast shooting
            RaycastHit hit;
            if (Physics.Raycast(fpsCamera.transform.position, fpsCamera.transform.forward, out hit, range))
            {
                Debug.Log("Hit: " + hit.transform.name);

                PlayerHealth target = hit.transform.GetComponent<PlayerHealth>();
                if (target != null)
                {
                    target.TakeDamage(damage);
                }

                if (hit.rigidbody != null)
                {
                    hit.rigidbody.AddForce(-hit.normal * impactForce);
                }

                if (impactEffect != null)
                {
                    GameObject impactGO = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                    Destroy(impactGO, 2f);
                }
            }
        }
    }
}
