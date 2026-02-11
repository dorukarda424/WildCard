using UnityEngine;
using UnityEngine.Events;

public class InteractiveSwitch : MonoBehaviour, IInteractable
{
    public UnityEvent onInteract;
    public bool isOneTime = false;
    public string interactPrompt = "Press E to Use";

    private bool hasBeenUsed = false;

    public void Interact()
    {
        if (isOneTime && hasBeenUsed) return;

        onInteract.Invoke();
        hasBeenUsed = true;
        
        // You could add an animation or sound here
        Debug.Log("Interacted with " + gameObject.name);
    }
}
