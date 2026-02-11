using UnityEngine;

public class RotatingHazard : MonoBehaviour
{
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 100f;
    public float damage = 20f;
    public bool isActive = true;

    void Update()
    {
        if (isActive)
        {
            transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        
    }
}
