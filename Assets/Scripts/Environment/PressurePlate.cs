using UnityEngine;
using UnityEngine.Events;

public class PressurePlate : MonoBehaviour
{
    public UnityEvent onActivate;
    public UnityEvent onDeactivate;
    public float downDistance = 0.1f;
    public float smoothTime = 0.1f;

    private Vector3 originalPosition;
    private Vector3 pressedPosition;
    private int triggerCount = 0;

    void Start()
    {
        originalPosition = transform.position;
        pressedPosition = originalPosition + Vector3.down * downDistance;
    }

    void Update()
    {
        Vector3 targetPos = (triggerCount > 0) ? pressedPosition : originalPosition;
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime / smoothTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.attachedRigidbody != null)
        {
            if (triggerCount == 0)
            {
                onActivate.Invoke();
            }
            triggerCount++;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.attachedRigidbody != null)
        {
            triggerCount--;
            if (triggerCount == 0)
            {
                onDeactivate.Invoke();
            }
        }
    }
}
