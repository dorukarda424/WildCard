using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Individual card display in the selection UI.
/// Shows only the card image (text is baked into the image).
/// </summary>
public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Elements")]
    [SerializeField] private Image cardImage;

    [Header("Hover Animation")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float animationSpeed = 10f;

    private Action _onClick;
    private bool _isHovered;
    private Vector3 _originalScale;

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    /// <summary>
    /// Set up the card display with data from a CardData ScriptableObject.
    /// </summary>
    public void Setup(CardData cardData, Action onClick)
    {
        _onClick = onClick;

        if (cardImage != null && cardData.icon != null)
        {
            cardImage.sprite = cardData.icon;
        }
    }

    private void Update()
    {
        // Smooth hover scale animation
        Vector3 targetScale = _isHovered ? _originalScale * hoverScale : _originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);
    }

    // ────────── Pointer Events ──────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _onClick?.Invoke();
    }
}
