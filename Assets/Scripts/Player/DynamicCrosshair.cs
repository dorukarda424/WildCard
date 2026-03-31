using UnityEngine;
using UnityEngine.UI;

public class DynamicCrosshair : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image dot;
    [SerializeField] private Image circle;

    private PlayerMovement _playerMovement;
    private PlayerCombat _playerCombat;

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
        _playerMovement = movement;
        
        if (_playerCombat != null)
        {
            _playerCombat.OnDamageDealt -= HandleShot;
        }

        _playerCombat = combat;

        if (_playerCombat != null)
        {
            _playerCombat.OnDamageDealt += HandleShot;
        }
    }

    private void OnDestroy()
    {
        if (_playerCombat != null)
            _playerCombat.OnDamageDealt -= HandleShot;
    }

    private void HandleShot(float damage) => OnShot();

    [Header("Reloading")]
    [SerializeField] private GameObject reloadingContainer;
    [SerializeField] private RectTransform reloadingSpinner;
    [SerializeField] private float rotationSpeed = 200f;

    private void Update()
    {
        if (_playerMovement == null) return;

        UpdateReloadingUI();

        bool isAiming   = InputManager.Instance != null && InputManager.Instance.IsAiming;
        bool isMoving   = _playerMovement.IsMoving && _playerMovement.IsGrounded;
        bool isAirborne = !_playerMovement.IsGrounded;

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

    private void UpdateReloadingUI()
    {
        if (_playerCombat == null) return;

        bool isReloading = _playerCombat.IsReloading;

        if (reloadingContainer != null)
        {
            reloadingContainer.SetActive(isReloading);
        }

        if (isReloading && reloadingSpinner != null)
        {
            reloadingSpinner.Rotate(0, 0, -rotationSpeed * Time.deltaTime);
        }

        // Hide crosshair components when reloading
        float alpha = isReloading ? 0f : 1f;
        if (dot != null)
        {
            Color c = dot.color;
            c.a = alpha;
            dot.color = c;
        }
        if (circle != null)
        {
            Color c = circle.color;
            c.a = alpha;
            circle.color = c;
        }
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
