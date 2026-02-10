using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log(transform.name + " took " + amount + " damage. Current Health: " + currentHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log(transform.name + " died.");
        // Add death logic here (e.g., disable movement, show game over screen, respawn)
        
        // Example: Disable the player controller
        FPSPlayerController controller = GetComponent<FPSPlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // Keep the game object active for a moment or handle scene reload
    }
}
