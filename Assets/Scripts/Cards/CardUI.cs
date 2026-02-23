using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Individual card display in the selection UI.
/// Shows icon, name, description, rarity border, and stat modifiers.
/// </summary>
public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Elements")]
    [SerializeField] private Image cardBackground;
    [SerializeField] private Image cardBorder;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Rarity Colors")]
    [SerializeField] private Color commonColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color rareColor = new Color(0.2f, 0.5f, 1f);
    [SerializeField] private Color legendaryColor = new Color(1f, 0.8f, 0f);

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

        if (nameText != null) nameText.text = cardData.cardName;
        if (descriptionText != null) descriptionText.text = cardData.description;

        if (iconImage != null)
        {
            if (cardData.icon != null)
            {
                iconImage.sprite = cardData.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        // Rarity display
        Color rarityColor = GetRarityColor(cardData.rarity);
        if (cardBorder != null) cardBorder.color = rarityColor;
        if (rarityText != null)
        {
            rarityText.text = cardData.rarity.ToString().ToUpper();
            rarityText.color = rarityColor;
        }

        // Stat modifiers display
        if (statsText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var mod in cardData.modifiers)
            {
                sb.AppendLine(mod.GetDescription());
            }
            if (cardData.specialEffects != SpecialEffect.None)
            {
                sb.AppendLine($"<color=#FFD700>★ {cardData.specialEffects}</color>");
            }
            statsText.text = sb.ToString().TrimEnd();
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

    // ────────── Helpers ──────────

    private Color GetRarityColor(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:    return commonColor;
            case CardRarity.Rare:      return rareColor;
            case CardRarity.Legendary: return legendaryColor;
            default:                   return commonColor;
        }
    }
}
