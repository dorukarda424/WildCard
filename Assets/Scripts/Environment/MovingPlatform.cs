using System.Collections.Generic;
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
    private Vector3 _lastPosition;
    private List<CharacterController> _riders = new List<CharacterController>();

    void Start()
    {
        if (waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
        }
        _lastPosition = transform.position;
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
            _lastPosition = transform.position;
            return;
        }

        Transform target = waypoints[currentTargetIndex];
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Apply platform movement delta to riders (without parenting)
        Vector3 delta = transform.position - _lastPosition;
        if (delta.sqrMagnitude > 0.0001f)
        {
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                if (_riders[i] != null && _riders[i].enabled)
                    _riders[i].Move(delta);
                else
                    _riders.RemoveAt(i);
            }
        }
        _lastPosition = transform.position;

        if (Vector3.Distance(transform.position, target.position) < 0.01f)
        {
            isWaiting = true;
        }
    }

    public void SetActive(bool state) => isActive = state;

    private void OnTriggerEnter(Collider other)
    {
        var cc = other.GetComponent<CharacterController>();
        if (cc != null && !_riders.Contains(cc))
        {
            _riders.Add(cc);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            _riders.Remove(cc);
        }
    }
}
