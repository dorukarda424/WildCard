using UnityEngine;

public class HazardLaser : MonoBehaviour
{
    public float damage = 20f;
    public float beamSpeed = 0.5f;
    public float onDuration = 2.0f;
    public float offDuration = 2.0f;
    public LineRenderer lineRenderer;
    public LayerMask hitLayers;

    private bool isOn = true;
    private float timer = 0f;

    void Start()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (isOn && timer >= onDuration)
        {
            isOn = false;
            timer = 0f;
            lineRenderer.enabled = false;
        }
        else if (!isOn && timer >= offDuration)
        {
            isOn = true;
            timer = 0f;
            lineRenderer.enabled = true;
        }

        if (isOn)
        {
            //UpdateLaser();
        }
    }

    //void UpdateLaser()
    //{
    //    RaycastHit hit;
    //    Vector3 direction = transform.forward;
    //    lineRenderer.SetPosition(0, transform.position);

    //    if (Physics.Raycast(transform.position, direction, out hit, 100f, hitLayers))
    //    {
    //        lineRenderer.SetPosition(1, hit.point);
            
    //        // Apply damage if it hits a player
    //        PlayerHealth health = hit.collider.GetComponent<PlayerHealth>();
    //        if (health != null)
    //        {
    //            // Simple cooldown to avoid instant death
    //            health.TakeDamage(damage * Time.deltaTime);
    //        }
    //    }
    //    else
    //    {
    //        lineRenderer.SetPosition(1, transform.position + direction * 100f);
    //    }
    //}
}
