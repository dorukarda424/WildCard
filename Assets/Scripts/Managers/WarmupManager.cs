using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Manages the playable warmup lobby where players can see each other,
/// walk around, and practice shooting while waiting for the match to start.
/// Place this in the "WarmupLobby" scene alongside spawn points.
/// 
/// Flow:
///   LobbyScene (UI) → Quick Play joins room → Master loads "WarmupLobby" scene
///   → Players spawn, move freely, shoot (instant respawn / no real death)
///   → When minPlayers reached → countdown starts
///   → Countdown ends → Master loads "level 1"
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class WarmupManager : MonoBehaviourPunCallbacks
{
    public static WarmupManager Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] private string playerPrefabName = "Player";
    [SerializeField] private Transform[] spawnPoints;

    [Header("Warmup Settings")]
    [Tooltip("Minimum players to start the countdown.")]
    [SerializeField] private int minPlayersToStart = 2;
    [Tooltip("Seconds to wait after minimum players before starting the match.")]
    [SerializeField] private float matchCountdown = 30f;
    [Tooltip("If true, players take no damage during warmup. If false, they respawn instantly on death.")]
    [SerializeField] private bool disableDamage = false;
    [Tooltip("Respawn delay in seconds when a player dies during warmup (only if disableDamage is false).")]
    [SerializeField] private float respawnDelay = 2f;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI warmupLabel;
    [SerializeField] private Button leaveButton;

    // Room property key for countdown sync
    private const string CD_END_PROP = "wcdEnd";

    private bool _countdownActive = false;
    private float _countdownTimer;
    private bool _gameStarting = false;
    private bool _localPlayerSpawned = false;

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
        // Spawn the local player
        if (PhotonNetwork.InRoom)
        {
            SpawnLocalPlayer();
        }

        // Unlock cursor and movement for warmup
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (warmupLabel != null)
            warmupLabel.text = "WARMUP — Practice while waiting!";

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnClickLeave);

        UpdatePlayerCountUI();
    }

    private void Update()
    {
        // Fallback: if Start() couldn't spawn because the room wasn't ready yet,
        // retry every frame until we succeed (handles AutomaticallySyncScene timing)
        if (!_localPlayerSpawned && PhotonNetwork.InRoom)
        {
            SpawnLocalPlayer();
        }

        if (!PhotonNetwork.InRoom || _gameStarting) return;

        UpdatePlayerCountUI();
        UpdateCountdown();
    }

    // ══════════════════════════════════════════════════════════
    //  SPAWNING
    // ══════════════════════════════════════════════════════════

    private void SpawnLocalPlayer()
    {
        if (_localPlayerSpawned) return;
        _localPlayerSpawned = true;

        int localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        Vector3 spawnPos = GetSpawnPosition(localActor);

        PhotonNetwork.Instantiate(playerPrefabName, spawnPos, Quaternion.identity);

        Debug.Log($"[WarmupManager] Local player spawned at {spawnPos}");
    }

    private Vector3 GetSpawnPosition(int actorNumber)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = (actorNumber - 1) % spawnPoints.Length;
            return spawnPoints[index].position;
        }
        return new Vector3((actorNumber - 1) * 3f, 1f, 0f);
    }

    /// <summary>
    /// Called by PlayerHealth when a player dies during warmup.
    /// Instead of ending the round, we just respawn them.
    /// </summary>
    public void OnWarmupDeath(PlayerHealth player)
    {
        if (disableDamage) return;

        int actorNr = player.photonView.OwnerActorNr;
        Vector3 respawnPos = GetSpawnPosition(actorNr);

        // Respawn after a short delay
        StartCoroutine(RespawnAfterDelay(player, respawnPos));
    }

    private System.Collections.IEnumerator RespawnAfterDelay(PlayerHealth player, Vector3 pos)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (player != null)
        {
            player.Respawn(pos);

            // Refill ammo on respawn
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.RefillAmmo();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  COUNTDOWN
    // ══════════════════════════════════════════════════════════

    private void UpdateCountdown()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;

        if (PhotonNetwork.IsMasterClient)
        {
            // Room full → start countdown immediately (but don't skip warmup)
            if (playerCount >= maxPlayers && !_countdownActive)
            {
                _countdownActive = true;
                _countdownTimer = matchCountdown;

                int endTimestamp = PhotonNetwork.ServerTimestamp + (int)(matchCountdown * 1000f);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CD_END_PROP, endTimestamp } });

                Debug.Log($"[WarmupManager] Room full — countdown started: {matchCountdown}s");
            }

            if (playerCount >= minPlayersToStart)
            {
                if (!_countdownActive)
                {
                    _countdownActive = true;
                    _countdownTimer = matchCountdown;

                    int endTimestamp = PhotonNetwork.ServerTimestamp + (int)(matchCountdown * 1000f);
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CD_END_PROP, endTimestamp } });

                    Debug.Log($"[WarmupManager] Countdown started: {matchCountdown}s");
                }

                _countdownTimer -= Time.deltaTime;

                if (_countdownTimer <= 0f)
                {
                    _countdownActive = false;
                    StartMatch();
                    return;
                }

                SetCountdownUI($"Match starts in {Mathf.CeilToInt(_countdownTimer)}s");
            }
            else
            {
                if (_countdownActive)
                {
                    _countdownActive = false;
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CD_END_PROP, -1 } });
                }
                SetCountdownUI("Waiting for players...");
            }
        }
        else
        {
            // Non-master: read from room properties
            object cdVal;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CD_END_PROP, out cdVal) && cdVal is int endTime && endTime > 0)
            {
                int remainingMs = endTime - PhotonNetwork.ServerTimestamp;
                float remainingS = remainingMs / 1000f;

                if (remainingS > 0f)
                    SetCountdownUI($"Match starts in {Mathf.CeilToInt(remainingS)}s");
                else
                    SetCountdownUI("Starting...");
            }
            else
            {
                SetCountdownUI("Waiting for players...");
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  MATCH START
    // ══════════════════════════════════════════════════════════

    private void StartMatch()
    {
        if (!PhotonNetwork.IsMasterClient || _gameStarting) return;
        _gameStarting = true;

        Debug.Log("[WarmupManager] Starting match!");

        if (GameManager.instance != null)
            GameManager.instance.ResetMapRotation();

        // Close room
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        // Load the first game level
        LoadingScreenManager.Show();
        PhotonNetwork.LoadLevel("level 1");
    }

    // ══════════════════════════════════════════════════════════
    //  UI
    // ══════════════════════════════════════════════════════════

    private void SetCountdownUI(string text)
    {
        if (countdownText != null) countdownText.text = text;
    }

    private void UpdatePlayerCountUI()
    {
        if (playerCountText == null || !PhotonNetwork.InRoom) return;

        int current = PhotonNetwork.CurrentRoom.PlayerCount;
        int max = PhotonNetwork.CurrentRoom.MaxPlayers;
        playerCountText.text = $"Players: {current}/{max}";
    }

    // ══════════════════════════════════════════════════════════
    //  LEAVE / CALLBACKS
    // ══════════════════════════════════════════════════════════

    private void OnClickLeave()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        // Return to the UI lobby scene
        LoadingScreenManager.Show();
        UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerCountUI();
        Debug.Log($"[WarmupManager] {newPlayer.NickName} joined the warmup.");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerCountUI();

        // If player count dropped below min, cancel countdown
        if (PhotonNetwork.IsMasterClient && _countdownActive)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount < minPlayersToStart)
            {
                _countdownActive = false;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CD_END_PROP, -1 } });
                Debug.Log("[WarmupManager] Countdown cancelled — not enough players.");
            }
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        if (playerCount >= minPlayersToStart)
        {
            object cdVal;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CD_END_PROP, out cdVal) && cdVal is int endTime && endTime > 0)
            {
                int remainingMs = endTime - PhotonNetwork.ServerTimestamp;
                _countdownTimer = Mathf.Max(0f, remainingMs / 1000f);
                _countdownActive = true;
                Debug.Log($"[WarmupManager] New Master took over countdown: {_countdownTimer:F1}s remaining.");
            }
        }
        else
        {
            _countdownActive = false;
        }
    }

    /// <summary>
    /// Returns true if we are in warmup mode (this scene).
    /// Other scripts (like PlayerHealth) can check this to decide
    /// whether death should end the round or just respawn.
    /// </summary>
    public static bool IsWarmup => Instance != null;
}
