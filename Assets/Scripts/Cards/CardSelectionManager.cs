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
            Debug.Log($"[CardSelectionManager] FinishSelection — card: {card.cardName}, modifiers: {card.modifiers.Length}, specialEffects: {card.specialEffects}");
            var localPlayer = GetLocalPlayerStats();
            if (localPlayer != null)
            {
                Debug.Log($"[CardSelectionManager] Found local PlayerStats on: {localPlayer.gameObject.name} (active: {localPlayer.gameObject.activeSelf}, enabled: {localPlayer.enabled})");
                Debug.Log($"[CardSelectionManager] BEFORE — Damage: {localPlayer.Damage}, MoveSpeed: {localPlayer.MoveSpeed}, MaxAmmo: {localPlayer.MaxAmmo}, AppliedCards: {localPlayer.AppliedCardIds.Count}");

                // Use networked apply only if player has a valid photonView owner.
                // If player was spawned locally (before Photon connected), Owner is null
                // and RPC would fail — use local-only apply in that case.
                bool canUseNetwork = PhotonNetwork.InRoom
                                  && localPlayer.photonView != null
                                  && localPlayer.photonView.Owner != null
                                  && localPlayer.photonView.IsMine;

                if (canUseNetwork)
                {
                    localPlayer.ApplyCardNetworked(card);
                }
                else
                {
                    localPlayer.ApplyCard(card);
                    Debug.Log("[CardSelectionManager] Applied card locally (player has no network owner).");
                }

                Debug.Log($"[CardSelectionManager] AFTER  — Damage: {localPlayer.Damage}, MoveSpeed: {localPlayer.MoveSpeed}, MaxAmmo: {localPlayer.MaxAmmo}, AppliedCards: {localPlayer.AppliedCardIds.Count}");
            }
            else
            {
                Debug.LogError("[CardSelectionManager] FinishSelection — GetLocalPlayerStats() returned NULL! Card NOT applied!");
            }
        }
        else
        {
            Debug.LogWarning("[CardSelectionManager] FinishSelection — card is NULL, nothing to apply.");
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
        // Include inactive objects — player may be disabled after death
        var players = FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[CardSelectionManager] GetLocalPlayerStats — found {players.Length} PlayerStats objects (including inactive). InRoom: {PhotonNetwork.InRoom}");

        PlayerStats fallback = null;

        foreach (var player in players)
        {
            bool isMine = player.photonView != null && player.photonView.IsMine;
            bool ownerIsNull = player.photonView != null && player.photonView.Owner == null;

            Debug.Log($"[CardSelectionManager]   → {player.gameObject.name} | active: {player.gameObject.activeSelf} | enabled: {player.enabled} | IsMine: {isMine} | Owner: {(player.photonView?.Owner?.NickName ?? "NULL")}");

            // Normal case: offline or IsMine
            if (!PhotonNetwork.InRoom || isMine)
                return player;

            // Edge case: player was spawned via local Instantiate before Photon connected.
            // photonView exists but Owner is null and IsMine is false.
            // This player IS ours — it was locally spawned.
            if (ownerIsNull)
            {
                Debug.Log($"[CardSelectionManager]   → Owner is NULL (locally spawned), using as fallback.");
                fallback = player;
            }
        }

        // If no IsMine match but we found a locally-spawned player, use it
        if (fallback != null)
        {
            Debug.Log($"[CardSelectionManager] Using fallback (locally spawned player): {fallback.gameObject.name}");
            return fallback;
        }

        Debug.LogError("[CardSelectionManager] GetLocalPlayerStats — No matching PlayerStats found!");
        return null;
    }
}