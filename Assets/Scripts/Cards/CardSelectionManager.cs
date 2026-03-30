using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Manages the card selection phase between rounds.
/// Presents random cards to the player and applies the chosen one.
/// </summary>
public class CardSelectionManager : MonoBehaviourPunCallbacks
{
    public static CardSelectionManager Instance { get; private set; }

    [Header("References")]
    public CardDatabase cardDatabase;
    [SerializeField] private CardSelectionUI cardSelectionUI;

    [Header("Settings")]
    [SerializeField] private int cardsToShow = 3;

    private List<CardData> _currentCards;
    private bool _isSelecting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Try to load from Resources if not assigned
        if (cardDatabase == null)
        {
            cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
        }

        if (cardSelectionUI != null)
        {
            cardSelectionUI.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called by RoundManager when entering card selection phase.
    /// Only shown to non-winner players.
    /// </summary>
    public void ShowCardSelection()
    {
        if (_isSelecting) return;
        _isSelecting = true;

        // Draw random cards
        _currentCards = cardDatabase.GetRandomCards(cardsToShow);

        if (_currentCards == null || _currentCards.Count == 0)
        {
            Debug.LogWarning("[CardSelectionManager] No cards available! Veritabanı boş veya atanamamış test edilemez.");
            FinishSelection(null);
            return;
        }

        // Show UI
        if (cardSelectionUI != null)
        {
            cardSelectionUI.gameObject.SetActive(true);
            cardSelectionUI.ShowCards(_currentCards, OnCardSelected);
        }
        else
        {
            Debug.LogError("[CardSelectionManager] CardSelectionUI referansı YOK! Inspector'dan atamalısın.");
        }

        // Unlock cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"[CardSelectionManager] Showing {_currentCards.Count} cards to choose from.");
    }

    /// <summary>
    /// Called when the player clicks a card.
    /// </summary>
    private void OnCardSelected(int cardIndex)
    {
        if (!_isSelecting || cardIndex < 0 || cardIndex >= _currentCards.Count) return;

        CardData selectedCard = _currentCards[cardIndex];
        Debug.Log($"[CardSelectionManager] Player selected: {selectedCard.cardName}");

        FinishSelection(selectedCard);
    }

    /// <summary>
    /// Called on timeout — auto-picks the first card.
    /// </summary>
    public void AutoPick()
    {
        if (!_isSelecting) return;

        if (_currentCards != null && _currentCards.Count > 0)
        {
            Debug.Log("[CardSelectionManager] Auto-picking first card due to timeout.");
            FinishSelection(_currentCards[0]);
        }
        else
        {
            FinishSelection(null);
        }
    }

    private void FinishSelection(CardData card)
    {
        _isSelecting = false;

        // Hide UI
        if (cardSelectionUI != null)
        {
            cardSelectionUI.gameObject.SetActive(false);
        }

        // Re-lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Apply card to local player's stats
        if (card != null)
        {
            var localPlayer = GetLocalPlayerStats();
            if (localPlayer != null)
            {
                // EĞER ÇEVRİMDIŞIYSAK (Test Ediyorsak) VEYA OFFLINE MODDAYSAN
                // Yalnızca Çevrimiçiyken Networklü Card Gönder
                if (PhotonNetwork.InRoom)
                {
                    localPlayer.ApplyCardNetworked(card);
                }
                else
                {
                    // Offline: apply locally only — no RPC
                    localPlayer.ApplyCard(card);
                }
            }
        }

        // Notify RoundManager that this player has picked
        if (RoundManager.Instance != null)
        {
            int myId = 1; // Default
            if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null)
            {
                myId = PhotonNetwork.LocalPlayer.ActorNumber;
            }

            RoundManager.Instance.OnCardPicked(myId);
        }
    }

    private PlayerStats GetLocalPlayerStats()
    {
        var players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            // Eger cevrimdisi calisiyorsak o zaman photonView mineda degilse de onu dondurecek
            if (!PhotonNetwork.InRoom || player.photonView.IsMine)
                return player;
        }
        return null;
    }
}