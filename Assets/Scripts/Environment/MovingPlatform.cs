using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    public Transform[] waypoints;
    public float speed = 3.0f;
    public float waitTime = 1.0f;
    public bool isActive = true;

    private int currentTargetIndex = 0;
    private bool isWaiting = false;
    private float waitCounter = 0f;

    void Start()
    {
        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
        }
    }

    void Update()
    {
        if (!isActive || waypoints.Length < 2) return;

        if (isWaiting)
        {
            waitCounter += Time.deltaTime;
            if (waitCounter >= waitTime)
            {
                isWaiting = false;
                waitCounter = 0f;
                currentTargetIndex = (currentTargetIndex + 1) % waypoints.Length;
            }
            return;
        }

        Transform target = waypoints[currentTargetIndex];
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < 0.01f)
        {
            isWaiting = true;
        }
    }

    public void SetActive(bool state) => isActive = state;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the player (or has a CharacterController)
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            other.transform.SetParent(transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            other.transform.SetParent(null);
            // Don't modify scale here, just unparent
        }
    }
}
