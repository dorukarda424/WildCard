using UnityEngine;

public class TriggerZone : MonoBehaviour
{
    public string zoneMessage = "Area Entered";
    public Color gizmoColor = new Color(0, 1, 0, 0.3f);
    public UnityEngine.Events.UnityEvent onTriggerEnter;
    public bool oneTimeOnly = false;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (oneTimeOnly && triggered) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log(zoneMessage);
            onTriggerEnter.Invoke();
            triggered = true;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.DrawCube(transform.position + box.center, box.size);
        }
    }
}
