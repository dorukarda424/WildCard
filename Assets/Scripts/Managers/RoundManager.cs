using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// MasterClient-authoritative round lifecycle manager.
/// Handles: Waiting → Countdown → Fighting → RoundOver → CardSelection → repeat
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class RoundManager : MonoBehaviourPunCallbacks
{
    public static RoundManager Instance { get; private set; }

    // ────────── Settings ──────────

    [Header("Round Settings")]
    [SerializeField] private int roundsToWin = 5;
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float roundOverDelay = 2f;
    [SerializeField] private float cardSelectionTimeout = 15f;
    [SerializeField] private string playerPrefabName = "Player";

    [Header("Debug")]
    [Tooltip("Inspector'dan tikla → mevcut round aninda biter (sadece MasterClient)")]
    [SerializeField] private bool forceEndRound = false;

    [Header("References")]
    [SerializeField] private Transform[] spawnPoints;

    // ────────── State ──────────

    public enum RoundState
    {
        WaitingForPlayers,
        Countdown,
        Fighting,
        RoundOver,
        CardSelection,
        MatchOver
    }

    public RoundState CurrentState { get; private set; } = RoundState.WaitingForPlayers;
    public int CurrentRound { get; private set; }
    public float StateTimer { get; private set; }
    public int RoundWinnerActorNumber { get; private set; }

    // Track alive players this round
    private HashSet<int> _alivePlayerActors = new HashSet<int>();
    // Track all registered players
    private HashSet<int> _registeredPlayerActors = new HashSet<int>();
    // Track who has picked their card this round
    private HashSet<int> _cardPickedActors = new HashSet<int>();

    // ────────── Events (for UI binding) ──────────

    public System.Action<RoundState> OnStateChanged;
    public System.Action<int> OnRoundStarted;
    public System.Action<int, int> OnPlayerKilled; // victim, killer
    public System.Action<int> OnRoundWon; // winner actor number
    public System.Action<int> OnMatchWon; // winner actor number

    // ────────── Lifecycle ──────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (PhotonNetwork.InRoom)
        {
            SpawnLocalPlayer();
        }
    }

    private void Update()
    {
        // ── Debug: Inspector'dan round bitirme ──
        if (forceEndRound)
        {
            forceEndRound = false;
            Debug.Log($"[RoundManager] DEBUG: Force End Round tıklandı! State: {CurrentState}, IsMasterClient: {PhotonNetwork.IsMasterClient}");

            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogWarning("[RoundManager] DEBUG: MasterClient değilsin, round bitirilemez!");
            }
            else
            {
                // Sahte winner ID → böylece sen "kaybeden" olursun ve kart seçim ekranı açılır
                int winnerId = -1;

                // Eğer henüz round başlamamışsa, round sayısını artır
                if (CurrentState == RoundState.WaitingForPlayers || CurrentState == RoundState.Countdown)
                {
                    CurrentRound++;
                }

                // Oyuncuyu alive listesine ekle (tek başına test için)
                if (!_alivePlayerActors.Contains(PhotonNetwork.LocalPlayer.ActorNumber))
                {
                    _alivePlayerActors.Add(PhotonNetwork.LocalPlayer.ActorNumber);
                }

                // Sahte "kazanan" oyuncuyu registered listesine ekle
                // böylece CardSelection _cardPickedActors(1) < _registeredPlayerActors(2) olur
                // ve kart seçim ekranı hemen kapanmaz
                if (!_registeredPlayerActors.Contains(-1))
                {
                    _registeredPlayerActors.Add(-1);
                }

                Debug.Log($"[RoundManager] DEBUG: Round {CurrentRound} zorla bitiriliyor. Winner: {winnerId}");
                EndRound(winnerId);
                return;
            }
        }

        // Only MasterClient drives state transitions
        if (!PhotonNetwork.IsMasterClient) return;

        StateTimer -= Time.deltaTime;

        switch (CurrentState)
        {
            case RoundState.WaitingForPlayers:
                UpdateWaiting();
                break;
            case RoundState.Countdown:
                UpdateCountdown();
                break;
            case RoundState.Fighting:
                // Fighting is event-driven (ends when players die)
                break;
            case RoundState.RoundOver:
                UpdateRoundOver();
                break;
            case RoundState.CardSelection:
                UpdateCardSelection();
                break;
        }
    }

    // ────────── Player Registration ──────────

    /// <summary>
    /// Called by Launcher after spawning a player.
    /// </summary>
    public void RegisterPlayer(int actorNumber)
    {
        photonView.RPC(nameof(RPC_RegisterPlayer), RpcTarget.AllBuffered, actorNumber);
    }

    [PunRPC]
    private void RPC_RegisterPlayer(int actorNumber)
    {
        _registeredPlayerActors.Add(actorNumber);
        Debug.Log($"[RoundManager] Player {actorNumber} registered. Total: {_registeredPlayerActors.Count}");
    }

    // ────────── State: Waiting ──────────

    private void UpdateWaiting()
    {
        // Need at least 2 players to start
        if (_registeredPlayerActors.Count >= 2)
        {
            StartCountdown();
        }
    }

    // ────────── State: Countdown ──────────

    private void StartCountdown()
    {
        CurrentRound++;
        photonView.RPC(nameof(RPC_ChangeState), RpcTarget.All,
            (int)RoundState.Countdown, CurrentRound, countdownDuration);
    }

    [PunRPC]
    private void RPC_ChangeState(int newState, int round, float timer)
    {
        CurrentState = (RoundState)newState;
        CurrentRound = round;
        StateTimer = timer;
        OnStateChanged?.Invoke(CurrentState);

        Debug.Log($"[RoundManager] State: {CurrentState} | Round: {CurrentRound} | Timer: {timer:F1}s");
    }

    private void UpdateCountdown()
    {
        if (StateTimer <= 0f)
        {
            StartFighting();
        }
    }

    // ────────── State: Fighting ──────────

    private void StartFighting()
    {
        // Mark all registered players as alive
        List<int> aliveList = new List<int>(_registeredPlayerActors);
        photonView.RPC(nameof(RPC_StartFighting), RpcTarget.All, aliveList.ToArray());
    }

    [PunRPC]
    private void RPC_StartFighting(int[] aliveActors)
    {
        CurrentState = RoundState.Fighting;
        StateTimer = -1f; // no timer during fighting

        _alivePlayerActors.Clear();
        foreach (int actor in aliveActors)
        {
            _alivePlayerActors.Add(actor);
        }

        // Respawn all players
        RespawnAllPlayers();

        OnStateChanged?.Invoke(CurrentState);
        OnRoundStarted?.Invoke(CurrentRound);
        Debug.Log($"[RoundManager] FIGHT! Round {CurrentRound} with {_alivePlayerActors.Count} players");
    }

    /// <summary>
    /// Called by PlayerHealth when a player dies.
    /// </summary>
    public void OnPlayerDied(int victimActorNumber, int killerActorNumber)
    {
        // Only process on MasterClient
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(nameof(RPC_PlayerDied), RpcTarget.All, victimActorNumber, killerActorNumber);
    }

    [PunRPC]
    private void RPC_PlayerDied(int victimActorNumber, int killerActorNumber)
    {
        _alivePlayerActors.Remove(victimActorNumber);
        OnPlayerKilled?.Invoke(victimActorNumber, killerActorNumber);

        // Notify ScoreManager
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RecordKill(killerActorNumber, victimActorNumber);
        }

        Debug.Log($"[RoundManager] Player {victimActorNumber} killed by {killerActorNumber}. " +
                  $"Alive: {_alivePlayerActors.Count}");

        // Check for round end (only on MasterClient)
        if (PhotonNetwork.IsMasterClient && _alivePlayerActors.Count <= 1)
        {
            int winnerId = -1;
            foreach (int id in _alivePlayerActors) winnerId = id;

            EndRound(winnerId);
        }
    }

    // ────────── State: Round Over ──────────

    private void EndRound(int winnerActorNumber)
    {
        photonView.RPC(nameof(RPC_EndRound), RpcTarget.All, winnerActorNumber, roundOverDelay);
    }

    [PunRPC]
    private void RPC_EndRound(int winnerActorNumber, float delay)
    {
        CurrentState = RoundState.RoundOver;
        StateTimer = delay;
        RoundWinnerActorNumber = winnerActorNumber;

        // Record round win
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RecordRoundWin(winnerActorNumber);
        }

        OnRoundWon?.Invoke(winnerActorNumber);
        OnStateChanged?.Invoke(CurrentState);

        string winnerName = GetPlayerName(winnerActorNumber);
        Debug.Log($"[RoundManager] Round {CurrentRound} won by {winnerName}!");
    }

    private void UpdateRoundOver()
    {
        if (StateTimer <= 0f)
        {
            // Check if someone won the match
            if (ScoreManager.Instance != null &&
                ScoreManager.Instance.GetRoundWins(RoundWinnerActorNumber) >= roundsToWin)
            {
                EndMatch(RoundWinnerActorNumber);
            }
            else
            {
                StartCardSelection();
            }
        }
    }

    // ────────── State: Card Selection ──────────

    private void StartCardSelection()
    {
        photonView.RPC(nameof(RPC_StartCardSelection), RpcTarget.All, RoundWinnerActorNumber, cardSelectionTimeout);
    }

    [PunRPC]
    private void RPC_StartCardSelection(int winnerActorNumber, float timeout)
    {
        CurrentState = RoundState.CardSelection;
        StateTimer = timeout;
        RoundWinnerActorNumber = winnerActorNumber;
        _cardPickedActors.Clear();

        // The winner auto-completes (they don't pick a card)
        _cardPickedActors.Add(winnerActorNumber);

        OnStateChanged?.Invoke(CurrentState);

        // Freeze all players during card selection
        FreezeLocalPlayer(true);

        // Trigger card selection UI for non-winners
        if (PhotonNetwork.LocalPlayer.ActorNumber != winnerActorNumber)
        {
            if (CardSelectionManager.Instance != null)
            {
                CardSelectionManager.Instance.ShowCardSelection();
            }
        }

        Debug.Log($"[RoundManager] Card selection phase. Winner ({winnerActorNumber}) skips.");
    }

    /// <summary>
    /// Called when a player finishes picking their card.
    /// </summary>
    public void OnCardPicked(int actorNumber)
    {
        photonView.RPC(nameof(RPC_CardPicked), RpcTarget.All, actorNumber);
    }

    [PunRPC]
    private void RPC_CardPicked(int actorNumber)
    {
        _cardPickedActors.Add(actorNumber);
        Debug.Log($"[RoundManager] Player {actorNumber} picked a card. " +
                  $"Picked: {_cardPickedActors.Count}/{_registeredPlayerActors.Count}");
    }

    private void UpdateCardSelection()
    {
        // All players picked or timeout expired
        if (_cardPickedActors.Count >= _registeredPlayerActors.Count || StateTimer <= 0f)
        {
            // Auto-pick for anyone who didn't pick
            if (StateTimer <= 0f)
            {
                Debug.Log("[RoundManager] Card selection timed out!");
            }

            // Unfreeze players before next round
            FreezeLocalPlayer(false);

            // Start next round
            StartCountdown();
        }
    }

    // ────────── State: Match Over ──────────

    private void EndMatch(int winnerActorNumber)
    {
        photonView.RPC(nameof(RPC_EndMatch), RpcTarget.All, winnerActorNumber);
    }

    [PunRPC]
    private void RPC_EndMatch(int winnerActorNumber)
    {
        CurrentState = RoundState.MatchOver;
        OnMatchWon?.Invoke(winnerActorNumber);
        OnStateChanged?.Invoke(CurrentState);

        string winnerName = GetPlayerName(winnerActorNumber);
        Debug.Log($"[RoundManager] MATCH WON by {winnerName}!");
    }

    // ────────── Players ──────────

    private void SpawnLocalPlayer()
    {
        Vector3 spawnPos = Vector3.zero;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Spawn each client on a unique point based on their ActorNumber
            int index = PhotonNetwork.LocalPlayer.ActorNumber % spawnPoints.Length;
            spawnPos = spawnPoints[index].position;
        }

        GameObject player = PhotonNetwork.Instantiate(playerPrefabName, spawnPos, Quaternion.identity);
        Debug.Log($"[RoundManager] Local Player instantiated directly in Level 2 at {spawnPos}");

        // Register immediately
        RegisterPlayer(PhotonNetwork.LocalPlayer.ActorNumber);
    }

    // ────────── Helpers ──────────

    private void RespawnAllPlayers()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            Vector3 spawnPos = Vector3.zero;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                // Use ActorNumber to consistently distribute spawn points across clients
                int index = player.photonView.OwnerActorNr % spawnPoints.Length;
                spawnPos = spawnPoints[index].position;
            }

            // Only respawn the local player's position to avoid conflicts
            if (player.photonView.IsMine)
            {
                player.Respawn(spawnPos);

                // Refill ammo
                var combat = player.GetComponent<PlayerCombat>();
                if (combat != null) combat.RefillAmmo();
            }
            else
            {
                // Remote players: just re-enable visuals (position comes from network)
                player.ResetState();
            }
        }
    }

    private string GetPlayerName(int actorNumber)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
                return string.IsNullOrEmpty(player.NickName) ? $"Player {actorNumber}" : player.NickName;
        }
        return $"Player {actorNumber}";
    }

    /// <summary>
    /// Freeze/unfreeze the local player during card selection.
    /// </summary>
    private void FreezeLocalPlayer(bool frozen)
    {
        var movement = FindLocalComponent<PlayerMovement>();
        if (movement != null) movement.enabled = !frozen;

        var combat = FindLocalComponent<PlayerCombat>();
        if (combat != null) combat.enabled = !frozen;

        // Show cursor for card selection UI
        Cursor.lockState = frozen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = frozen;

        // Disable input during freeze
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(!frozen);
    }

    private T FindLocalComponent<T>() where T : MonoBehaviour
    {
        var all = FindObjectsByType<T>(FindObjectsSortMode.None);
        foreach (var comp in all)
        {
            var pv = comp.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine) return comp;
        }
        return null;
    }

    /// <summary>
    /// Handle player disconnects during a round.
    /// </summary>
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        _registeredPlayerActors.Remove(otherPlayer.ActorNumber);
        _alivePlayerActors.Remove(otherPlayer.ActorNumber);

        Debug.Log($"[RoundManager] Player {otherPlayer.NickName} left. Alive: {_alivePlayerActors.Count}");

        // If fighting and only 1 alive, end round
        if (PhotonNetwork.IsMasterClient && CurrentState == RoundState.Fighting && _alivePlayerActors.Count <= 1)
        {
            int winnerId = -1;
            foreach (int id in _alivePlayerActors) winnerId = id;
            EndRound(winnerId);
        }
    }
}
