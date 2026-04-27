using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// MasterClient-authoritative round lifecycle manager.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class RoundManager : MonoBehaviourPunCallbacks
{
    public static RoundManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private int roundsToWin = 5;
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float roundOverDelay = 8f; // Must be >= kill cam total (victim 2s + killer 5s)
    [SerializeField] private float cardSelectionTimeout = 15f;
    [SerializeField] private string playerPrefabName = "Player";

    [Header("Map Rotation")]
    [Tooltip("Scene names in sequential order. After each round the next map loads.")]
    [SerializeField] private string[] mapRotation = { "level 1", "level 2", "level 3" };

    [Header("Debug")]
    [Tooltip("Inspector'dan tikla → mevcut round aninda biter")]
    [SerializeField] private bool forceEndRound = false;

    [Header("References")]
    [SerializeField] private Transform[] spawnPoints;

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

    private HashSet<int> _alivePlayerActors = new HashSet<int>();
    private HashSet<int> _registeredPlayerActors = new HashSet<int>();
    private HashSet<int> _cardPickedActors = new HashSet<int>();
    private bool _isLoadingNextMap = false;
    private bool _localPlayerSpawned = false;

    public System.Action<RoundState> OnStateChanged;
    public System.Action<int> OnRoundStarted;
    public System.Action<int, int> OnPlayerKilled;
    public System.Action<int> OnRoundWon;
    public System.Action<int> OnMatchWon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Restore round counter from GameManager (persists across scene loads)
        if (GameManager.instance != null)
        {
            CurrentRound = GameManager.instance.currentRound;
        }

        // Çevrimdışı isen veya Odadaysan oyuncuyu direk spawnla
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom != null)
        {
            SpawnLocalPlayer();
        }
    }

    private void Update()
    {
        // Fallback: if Awake() couldn't spawn because the room wasn't ready yet,
        // retry every frame until we succeed (handles AutomaticallySyncScene timing)
        if (!_localPlayerSpawned && (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom != null))
        {
            SpawnLocalPlayer();
        }

        // ------------- DEBUG: FORCE END ROUND -------------
        if (forceEndRound)
        {
            forceEndRound = false;

            int winnerId = PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : 1;

            if (CurrentState == RoundState.WaitingForPlayers || CurrentState == RoundState.Countdown)
            {
                CurrentRound++;
            }

            if (!_alivePlayerActors.Contains(winnerId))
            {
                _alivePlayerActors.Add(winnerId);
            }
            if (!_registeredPlayerActors.Contains(winnerId))
            {
                _registeredPlayerActors.Add(winnerId);
            }

            Debug.Log($"[RoundManager] DEBUG: Round zorla bitiriliyor. Winner: {winnerId}");

            // Eğer çevrimdışıysak direkt RPC methodlarını kullan (Bypass)
            if (PhotonNetwork.InRoom) EndRound(winnerId);
            else RPC_EndRound(winnerId, roundOverDelay);

            return;
        }
        // ------------------------------------------------

        // Sadece Master Client veya Çevrimdışı (Offline Mode) state idare edebilir
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        StateTimer -= Time.deltaTime;

        switch (CurrentState)
        {
            case RoundState.WaitingForPlayers: UpdateWaiting(); break;
            case RoundState.Countdown: UpdateCountdown(); break;
            case RoundState.Fighting: break;
            case RoundState.RoundOver: UpdateRoundOver(); break;
            case RoundState.CardSelection: UpdateCardSelection(); break;
        }
    }

    // ────────── REGISTER ──────────
    public void RegisterPlayer(int actorNumber)
    {
        if (PhotonNetwork.InRoom) photonView.RPC(nameof(RPC_RegisterPlayer), RpcTarget.AllBuffered, actorNumber);
        else RPC_RegisterPlayer(actorNumber); // Offline
    }

    [PunRPC]
    private void RPC_RegisterPlayer(int actorNumber)
    {
        _registeredPlayerActors.Add(actorNumber);
    }

    // ────────── WAITING ──────────
    private void UpdateWaiting()
    {
        // En az 1 oyuncu kayıtlıysa geri sayımı başlat
        // (Çevrimdışı test veya lobi üzerinden tek kişi girildiğinde de çalışır)
        if (_registeredPlayerActors.Count >= 1)
        {
            StartCountdown();
        }
    }

    // ────────── COUNTDOWN ──────────
    private void StartCountdown()
    {
        CurrentRound++;

        // Persist to GameManager so it survives scene transitions
        if (GameManager.instance != null)
        {
            GameManager.instance.currentRound = CurrentRound;
        }

        if (PhotonNetwork.InRoom) photonView.RPC(nameof(RPC_ChangeState), RpcTarget.All, (int)RoundState.Countdown, CurrentRound, countdownDuration);
        else RPC_ChangeState((int)RoundState.Countdown, CurrentRound, countdownDuration);
    }

    [PunRPC]
    private void RPC_ChangeState(int newState, int round, float timer)
    {
        CurrentState = (RoundState)newState;
        CurrentRound = round;
        StateTimer = timer;

        if (CurrentState == RoundState.Countdown)
        {
            // GERİ SAYIM BAŞLADIYSA OYUNCULARI KİLİTLE (HAREKETSİZ)
            FreezeLocalPlayer(true);

            // VE HERKESİ RASTGELE YENİ KÖŞESİNE DİZ BEKLESİN
            RespawnAllPlayers();
        }

        OnStateChanged?.Invoke(CurrentState);
    }

    private void UpdateCountdown()
    {
        if (StateTimer <= 0f) StartFighting();
    }

    // ────────── FIGHTING ──────────
    private void StartFighting()
    {
        List<int> aliveList = new List<int>(_registeredPlayerActors);
        if (PhotonNetwork.InRoom) photonView.RPC(nameof(RPC_StartFighting), RpcTarget.All, aliveList.ToArray());
        else RPC_StartFighting(aliveList.ToArray());
    }

    [PunRPC]
    private void RPC_StartFighting(int[] aliveActors)
    {
        CurrentState = RoundState.Fighting;
        StateTimer = -1f;

        _alivePlayerActors.Clear();
        foreach (int actor in aliveActors) _alivePlayerActors.Add(actor);

        // FIGHT KOMUTU GELDİ, KİLİTLERİ AÇ SAVAŞ BAŞLASIN
        FreezeLocalPlayer(false);

        OnStateChanged?.Invoke(CurrentState);
        OnRoundStarted?.Invoke(CurrentRound);
    }

    public void OnPlayerDied(int victimActorNumber, int killerActorNumber)
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        if (PhotonNetwork.InRoom) photonView.RPC(nameof(RPC_PlayerDied), RpcTarget.All, victimActorNumber, killerActorNumber);
        else RPC_PlayerDied(victimActorNumber, killerActorNumber);
    }

    [PunRPC]
    private void RPC_PlayerDied(int victimActorNumber, int killerActorNumber)
    {
        _alivePlayerActors.Remove(victimActorNumber);
        OnPlayerKilled?.Invoke(victimActorNumber, killerActorNumber);

        if (ScoreManager.Instance != null) ScoreManager.Instance.RecordKill(killerActorNumber, victimActorNumber);

        bool isOver = PhotonNetwork.InRoom ? PhotonNetwork.IsMasterClient && _alivePlayerActors.Count <= 1 : _alivePlayerActors.Count == 0;
        if (isOver)
        {
            int winnerId = -1;
            foreach (int id in _alivePlayerActors) winnerId = id;
            if (PhotonNetwork.InRoom) EndRound(winnerId);
            else RPC_EndRound(winnerId, roundOverDelay);
        }
    }

    // ────────── ROUND OVER ──────────
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

        if (ScoreManager.Instance != null) ScoreManager.Instance.RecordRoundWin(winnerActorNumber);

        OnRoundWon?.Invoke(winnerActorNumber);
        OnStateChanged?.Invoke(CurrentState);
    }

    private void UpdateRoundOver()
    {
        if (StateTimer <= 0f)
        {
            if (ScoreManager.Instance != null && ScoreManager.Instance.GetRoundWins(RoundWinnerActorNumber) >= roundsToWin)
            {
                if (PhotonNetwork.InRoom) EndMatch(RoundWinnerActorNumber);
                else RPC_EndMatch(RoundWinnerActorNumber);
            }
            else
            {
                if (PhotonNetwork.InRoom) StartCardSelection();
                else RPC_StartCardSelection(RoundWinnerActorNumber, cardSelectionTimeout);
            }
        }
    }

    // ────────── CARD SELECTION ──────────
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

        OnStateChanged?.Invoke(CurrentState);

        FreezeLocalPlayer(true);

        // TEST İÇİN: HERKES SEÇSİN. (Eğer tam sürüme geçerken sadece kaybeden seçsin istersen if(PhotonNetwork.LocalPlayer.ActorNumber != winner) eklersin)
        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.ShowCardSelection();
        }
    }

    public void OnCardPicked(int actorNumber)
    {
        if (PhotonNetwork.InRoom) photonView.RPC(nameof(RPC_CardPicked), RpcTarget.All, actorNumber);
        else RPC_CardPicked(actorNumber);
    }

    [PunRPC]
    private void RPC_CardPicked(int actorNumber)
    {
        _cardPickedActors.Add(actorNumber);
    }

    private void UpdateCardSelection()
    {
        // Offline oyun testinde odaya kayıtlı sadece 1 kişi olabilir. Eğer o kişi bizsek, tıklayana kadar bekle:
        if (!PhotonNetwork.InRoom || _registeredPlayerActors.Count <= 1)
        {
            // Zaman bitmediyse VE henüz karta TIKLAMADIYSAK oyunu başlatma, bekle.
            if (StateTimer > 0f && _cardPickedActors.Count == 0)
            {
                return;
            }

            // Eğer süre bittiyse (timeout) veya tıklayarak listeye eklendiysek geç
            LoadNextMap();
            return;
        }

        // Multiplayer Orijinal Mantık: Herkes kartını seçtiyse veya Süre bittiyse geç.
        if (_cardPickedActors.Count >= _registeredPlayerActors.Count || StateTimer <= 0f)
        {
            LoadNextMap();
        }
    }

    // ────────── MAP ROTATION ──────────
    private void LoadNextMap()
    {
        // Prevent multiple calls while scene is loading
        if (_isLoadingNextMap) return;
        _isLoadingNextMap = true;

        // If no map rotation is configured or only one map, just stay on the same scene
        if (mapRotation == null || mapRotation.Length <= 1)
        {
            _isLoadingNextMap = false;
            StartCountdown();
            return;
        }

        // Get current map index from GameManager (persists across scenes)
        int currentIndex = 0;
        if (GameManager.instance != null)
        {
            currentIndex = GameManager.instance.currentMapIndex;
        }

        // Advance to next map
        int nextIndex = (currentIndex + 1) % mapRotation.Length;
        string nextMap = mapRotation[nextIndex];

        // Save the new index so it persists after scene load
        if (GameManager.instance != null)
        {
            GameManager.instance.currentMapIndex = nextIndex;
        }

        Debug.Log($"[RoundManager] Map rotation: {mapRotation[currentIndex]} → {nextMap} (index {nextIndex})");

        if (GameManager.instance != null)
        {
            GameManager.instance.nextSceneName = nextMap;
        }
        
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("LoadingScene");
        }
        else if (!PhotonNetwork.InRoom)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");
        }
    }

    // ────────── MATCH OVER ──────────
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
    }

    // ────────── SPAWNING & FREEZE ──────────
    private void SpawnLocalPlayer()
    {
        if (_localPlayerSpawned) return;
        _localPlayerSpawned = true;

        try
        {
            Vector3 spawnPos = Vector3.zero;
            int localActor = PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : 1;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int spawnIndex = Mathf.Max(0, localActor - 1) % spawnPoints.Length;
                if (spawnPoints[spawnIndex] != null)
                {
                    spawnPos = spawnPoints[spawnIndex].position;
                }
                else
                {
                    spawnPos = new Vector3((localActor - 1) * 3f, 1f, 0f);
                }
            }
            else
            {
                // Fallback to prevent spawning inside each other if spawn points aren't linked in Inspector
                spawnPos = new Vector3((localActor - 1) * 3f, 1f, 0f);
            }

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.Instantiate(playerPrefabName, spawnPos, Quaternion.identity);
            }
            else
            {
                // Çevrimdışı mode test için Resources'tan spawn
                Instantiate(Resources.Load(playerPrefabName), spawnPos, Quaternion.identity);
            }

            RegisterPlayer(localActor);
            Debug.Log($"[RoundManager] Local player successfully spawned at {spawnPos}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RoundManager] Failed to spawn local player: {e.Message}\n{e.StackTrace}");
            _localPlayerSpawned = false; // Allow retrying if it was a temporary error
        }
    }

    private void RespawnAllPlayers()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var player in players)
        {
            Vector3 spawnPos = Vector3.zero;
            int actorNr = PhotonNetwork.InRoom && player.photonView != null ? player.photonView.OwnerActorNr : 1;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int spawnIndex = Mathf.Max(0, actorNr - 1) % spawnPoints.Length;
                spawnPos = spawnPoints[spawnIndex].position;
            }
            else
            {
                // Fallback to prevent spawning inside each other
                spawnPos = new Vector3((actorNr - 1) * 3f, 1f, 0f);
            }

            if (!PhotonNetwork.InRoom || player.photonView.IsMine)
            {
                // Görünmez olmuşsa tekrar aç
                if (!player.gameObject.activeSelf) player.gameObject.SetActive(true);

                // Teleport işlemini yap
                player.Respawn(spawnPos);

                // Mermiyi yenile
                var combat = player.GetComponent<PlayerCombat>();
                if (combat != null) combat.RefillAmmo();
            }
            // Remote playerların Transform/State reset işini de unutmayalım
            else if (PhotonNetwork.InRoom && !player.photonView.IsMine)
            {
                player.ResetState();
            }
        }
    }

    private string GetPlayerName(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return "Test Player";

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
                return string.IsNullOrEmpty(player.NickName) ? $"Player {actorNumber}" : player.NickName;
        }
        return $"Player {actorNumber}";
    }

    private void FreezeLocalPlayer(bool frozen)
    {
        var movement = FindLocalComponent<PlayerMovement>(true);
        if (movement != null) movement.enabled = !frozen;

        var combat = FindLocalComponent<PlayerCombat>(true);
        if (combat != null) combat.enabled = !frozen;

        Cursor.lockState = frozen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = frozen;

        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(!frozen);
    }

    private T FindLocalComponent<T>(bool includeInactive = false) where T : MonoBehaviour
    {
        var findMode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        var all = FindObjectsByType<T>(findMode, FindObjectsSortMode.None);
        T fallback = null;

        foreach (var comp in all)
        {
            if (!PhotonNetwork.InRoom) return comp; // Çevrimdışı ise ilk bulduğunu al

            var pv = comp.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine) return comp;

            // Fallback: offline spawn edilmiş oyuncuyu kaybetmeyelim
            if (fallback == null) fallback = comp;
        }

        // IsMine olan bulunamadıysa (offline→online geçişi), ilk bulunanı döndür
        return fallback;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        _registeredPlayerActors.Remove(otherPlayer.ActorNumber);
        _alivePlayerActors.Remove(otherPlayer.ActorNumber);

        if (PhotonNetwork.IsMasterClient && CurrentState == RoundState.Fighting && _alivePlayerActors.Count <= 1)
        {
            int winnerId = -1;
            foreach (int id in _alivePlayerActors) winnerId = id;
            EndRound(winnerId);
        }
    }
}
