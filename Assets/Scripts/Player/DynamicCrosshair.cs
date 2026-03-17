using UnityEngine;
using UnityEngine.UI;

public class DynamicCrosshair : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image dot;
    [SerializeField] private Image circle;

    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;

    [Header("Size")]
    [SerializeField] private float baseRadius = 20f;
    [SerializeField] private float moveRadius = 40f;
    [SerializeField] private float jumpRadius = 55f;
    [SerializeField] private float shootKick = 10f;
    [SerializeField] private float lerpSpeed = 10f;

    [Header("ADS")]
    [SerializeField] private float adsRadius = 10f;
    [SerializeField] private float adsLerpSpeed = 12f;

    private float _currentRadius;

    private void Awake()
    {
        _currentRadius = baseRadius;
    }

    // Call this from your local Player setup script when they spawn
    public void SetPlayer(PlayerMovement movement, PlayerCombat combat)
    {
        playerMovement = movement;
        
        if (playerCombat != null)
        {
            playerCombat.OnDamageDealt -= HandleShot;
        }

        playerCombat = combat;

        if (playerCombat != null)
        {
            playerCombat.OnDamageDealt += HandleShot;
        }
    }

    private void OnDestroy()
    {
        if (playerCombat != null)
            playerCombat.OnDamageDealt -= HandleShot;
    }

    private void HandleShot(float damage) => OnShot();

    private void Update()
    {
        if (playerMovement == null) return;

        bool isAiming   = InputManager.Instance != null && InputManager.Instance.IsAiming;
        bool isMoving   = playerMovement.IsMoving && playerMovement.IsGrounded;
        bool isAirborne = !playerMovement.IsGrounded;

        float targetRadius;

        if (isAiming)
        {
            targetRadius = adsRadius;
        }
        else if (isAirborne)
        {
            targetRadius = jumpRadius;
        }
        else if (isMoving)
        {
            targetRadius = moveRadius;
        }
        else
        {
            targetRadius = baseRadius;
        }

        float lerp = isAiming ? adsLerpSpeed : lerpSpeed;
        _currentRadius = Mathf.Lerp(_currentRadius, targetRadius, Time.deltaTime * lerp);

        ApplyRadius(_currentRadius);
    }

    private void OnShot()
    {
        _currentRadius += shootKick;
    }

    private void ApplyRadius(float radius)
    {
        if (circle != null)
        {
            circle.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        }
    }
}
