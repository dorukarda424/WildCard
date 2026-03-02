using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI panel that displays 3 cards for the player to choose from.
/// Should be placed on a Canvas as an overlay panel.
/// </summary>
public class CardSelectionUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Transform cardContainer; // Horizontal layout group
    [SerializeField] private CardUI cardPrefab;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Background")]
    [SerializeField] private Image backgroundOverlay;

    private List<CardUI> _spawnedCards = new List<CardUI>();
    private Action<int> _onCardSelected;
    private float _timer;

    /// <summary>
    /// Show the card selection panel with the given cards.
    /// </summary>
    public void ShowCards(List<CardData> cards, Action<int> onSelected)
    {
        _onCardSelected = onSelected;

        // Clear old cards
        foreach (var card in _spawnedCards)
        {
            if (card != null) Destroy(card.gameObject);
        }
        _spawnedCards.Clear();

        // Set header
        if (headerText != null)
        {
            headerText.text = "CHOOSE YOUR CARD";
        }

        // Spawn card UI elements
        if (cardPrefab == null)
        {
            Debug.LogError("[CardSelectionUI] cardPrefab atanmamış! Inspector'dan CardUI prefab'ını ata.");
            return;
        }
        if (cardContainer == null)
        {
            Debug.LogError("[CardSelectionUI] cardContainer atanmamış! Inspector'dan ata.");
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            int index = i; // capture for lambda
            CardUI cardUI = Instantiate(cardPrefab, cardContainer);
            cardUI.Setup(cards[i], () => OnCardClicked(index));
            _spawnedCards.Add(cardUI);
        }

        _timer = 15f; // matches RoundManager timeout
    }

    private void Update()
    {
        if (_timer > 0f)
        {
            _timer -= Time.deltaTime;
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(_timer).ToString();
            }
        }
    }

    private void OnCardClicked(int index)
    {
        _onCardSelected?.Invoke(index);
    }
}
