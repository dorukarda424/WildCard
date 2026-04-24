using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("Panels")]
    public GameObject lobbyPanel; 
    public GameObject roomPanel;  

    [Header("Lobby UI")]
    public TMP_InputField createInput;
    public Transform roomListContent;
    public RoomItem roomItemPrefab;

    [Header("Room UI")]
    public TextMeshProUGUI roomNameText; 
    public Transform playerListContent;  
    public GameObject playerItemPrefab; 
    public Button startGameButton;       

    [Header("Auto Matchmaking")]
    [Tooltip("Minimum number of players required to start the countdown.")]
    [SerializeField] private int minPlayersToStart = 2;
    [Tooltip("Seconds to wait after minimum players reached before auto-starting.")]
    [SerializeField] private float autoStartCountdown = 30f;
    [Tooltip("UI text showing countdown or waiting status. Assign in Inspector.")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [Tooltip("UI text showing matchmaking status (Searching / Match Found). Assign in Inspector.")]
    [SerializeField] private TextMeshProUGUI statusText;

    // ── Internal state ──
    private bool _isAutoMatch = false;
    private bool _countdownActive = false;
    private float _countdownTimer;

    // Room custom property key for syncing countdown end timestamp
    private const string CD_END_PROP = "cdEnd";

    List<RoomInfo> cachedRoomList = new List<RoomInfo>();

    void Start()
    {
        lobbyPanel.SetActive(true);
        roomPanel.SetActive(false);
        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinLobby();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        // Hide auto-match UI elements until needed
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || !_isAutoMatch) return;

        UpdateAutoMatchCountdown();
    }

    // ══════════════════════════════════════════════════════════
    //  AUTO MATCHMAKING
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Called by the "Quick Play" button in the Lobby UI.
    /// Automatically finds or creates a room.
    /// </summary>
    public void OnClickQuickPlay()
    {
        _isAutoMatch = true;

        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = "Searching for match...";
        }

        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (!_isAutoMatch) return;

        Debug.Log("[AutoMatch] No open room found. Creating a new one...");
        if (statusText != null) statusText.text = "Creating match...";

        int maxP = (GameManager.instance != null) ? GameManager.instance.maxPlayers : 4;

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = (byte)maxP,
            IsVisible = true,
            IsOpen = true
        };

        // null name = Photon generates a unique GUID name
        PhotonNetwork.CreateRoom(null, options);
    }

    // ── Countdown logic (runs every frame while in auto-match room) ──

    private void UpdateAutoMatchCountdown()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;

        // ─── Master Client: drive the countdown ───
        if (PhotonNetwork.IsMasterClient)
        {
            // Room is full → start immediately
            if (playerCount >= maxPlayers)
            {
                _countdownActive = false;
                AutoStartGame();
                return;
            }

            if (playerCount >= minPlayersToStart)
            {
                if (!_countdownActive)
                {
                    // Begin countdown
                    _countdownActive = true;
                    _countdownTimer = autoStartCountdown;

                    // Sync countdown end time to all clients via room property
                    int endTimestamp = PhotonNetwork.ServerTimestamp + (int)(autoStartCountdown * 1000f);
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CD_END_PROP, endTimestamp } });

                    Debug.Log($"[AutoMatch] Countdown started: {autoStartCountdown}s");
                }

                _countdownTimer -= Time.deltaTime;

                if (_countdownTimer <= 0f)
                {
                    _countdownActive = false;
                    AutoStartGame();
                    return;
                }

                SetCountdownUI($"Game starts in {Mathf.CeilToInt(_countdownTimer)}s  ({playerCount}/{maxPlayers})");
            }
            else
            {
                // Not enough players → cancel countdown if active
                if (_countdownActive)
                {
                    _countdownActive = false;
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CD_END_PROP, -1 } });
                    Debug.Log("[AutoMatch] Countdown cancelled — not enough players.");
                }

                SetCountdownUI($"Waiting for players... ({playerCount}/{minPlayersToStart})");
            }
        }
        // ─── Non-Master Client: read countdown from room properties ───
        else
        {
            object cdVal;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CD_END_PROP, out cdVal) && cdVal is int endTime && endTime > 0)
            {
                int remainingMs = endTime - PhotonNetwork.ServerTimestamp;
                float remainingS = remainingMs / 1000f;

                if (remainingS > 0f)
                    SetCountdownUI($"Game starts in {Mathf.CeilToInt(remainingS)}s  ({playerCount}/{maxPlayers})");
                else
                    SetCountdownUI("Starting...");
            }
            else
            {
                SetCountdownUI($"Waiting for players... ({playerCount}/{minPlayersToStart})");
            }
        }
    }

    private void SetCountdownUI(string text)
    {
        if (countdownText != null) countdownText.text = text;
    }

    /// <summary>
    /// Master Client auto-starts the game (auto-match only).
    /// </summary>
    private void AutoStartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("[AutoMatch] Starting game!");

        if (GameManager.instance != null)
            GameManager.instance.ResetMapRotation();

        // Close room so no one else joins mid-game
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        PhotonNetwork.LoadLevel("level 1");
    }

    // ══════════════════════════════════════════════════════════
    //  CUSTOM LOBBY (unchanged)
    // ══════════════════════════════════════════════════════════

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public void CreateRoom()
    {
        if (string.IsNullOrEmpty(createInput.text)) return;
        _isAutoMatch = false;

        RoomOptions options = new RoomOptions { MaxPlayers = 4, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(createInput.text, options);
    }

    public void JoinRoom(string roomName)
    {
        _isAutoMatch = false;
        PhotonNetwork.JoinRoom(roomName);
    }

    // ══════════════════════════════════════════════════════════
    //  ROOM CALLBACKS (shared by both modes)
    // ══════════════════════════════════════════════════════════

    public override void OnJoinedRoom()
    {
        Debug.Log("Odaya girildi!");

        if (_isAutoMatch)
        {
            // Auto match → Load the playable warmup lobby scene
            // WarmupManager in that scene handles spawning, countdown, and match start
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Loading warmup...";
            }

            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.LoadLevel("WarmupLobby");
            }
        }
        else
        {
            // Custom lobby → show room panel with player list
            lobbyPanel.SetActive(false);
            roomPanel.SetActive(true);

            roomNameText.text = PhotonNetwork.CurrentRoom.Name;
            UpdatePlayerList();

            startGameButton.gameObject.SetActive(true);
            startGameButton.interactable = PhotonNetwork.IsMasterClient;

            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (statusText != null) statusText.gameObject.SetActive(false);
        }
    }

    void UpdatePlayerList()
    {
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject newPlayerItem = Instantiate(playerItemPrefab, playerListContent);

            // Show rank next to player name
            int rank = 0;
            if (player.CustomProperties.TryGetValue("rank", out object rankVal) && rankVal is int r)
                rank = r;

            string rankLabel = GetRankLabel(rank);
            newPlayerItem.GetComponentInChildren<TextMeshProUGUI>().text = $"{rankLabel}  {player.NickName}";
        }

        startGameButton.interactable = PhotonNetwork.IsMasterClient;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();

        // Auto-match: if player count drops below minimum, cancel countdown
        if (_isAutoMatch && PhotonNetwork.IsMasterClient)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount < minPlayersToStart && _countdownActive)
            {
                _countdownActive = false;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CD_END_PROP, -1 } });
                Debug.Log("[AutoMatch] Countdown cancelled — player left, below minimum.");
            }
        }
    }

    /// <summary>
    /// If the Master Client leaves, the new Master takes over the countdown.
    /// </summary>
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (_isAutoMatch && PhotonNetwork.IsMasterClient)
        {
            // Re-evaluate countdown based on current player count
            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            if (playerCount >= minPlayersToStart)
            {
                // Read remaining time from room property and continue
                object cdVal;
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CD_END_PROP, out cdVal) && cdVal is int endTime && endTime > 0)
                {
                    int remainingMs = endTime - PhotonNetwork.ServerTimestamp;
                    _countdownTimer = Mathf.Max(0f, remainingMs / 1000f);
                    _countdownActive = true;
                    Debug.Log($"[AutoMatch] New Master took over countdown: {_countdownTimer:F1}s remaining.");
                }
                else
                {
                    // No active countdown — start a fresh one
                    _countdownActive = false; // Will be started on next Update
                }
            }
            else
            {
                _countdownActive = false;
            }
        }

        // Update start button interactability for custom lobby
        if (!_isAutoMatch)
        {
            startGameButton.interactable = PhotonNetwork.IsMasterClient;
        }
    }

    // ──────────── MANUAL START (Custom Lobby) ────────────

    public void OnClickStartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Reset map rotation for a fresh match
            if (GameManager.instance != null)
            {
                GameManager.instance.ResetMapRotation();
            }

            PhotonNetwork.LoadLevel("level 1");
        }
    }

    public void OnClickLeaveRoom()
    {
        _isAutoMatch = false;
        _countdownActive = false;
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    // ──────────── ROOM LIST (Custom Lobby) ────────────

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList)
            {
                int index = cachedRoomList.FindIndex(x => x.Name == info.Name);
                if (index != -1) cachedRoomList.RemoveAt(index);
            }
            else
            {
                int index = cachedRoomList.FindIndex(x => x.Name == info.Name);
                if (index == -1) cachedRoomList.Add(info);
                else cachedRoomList[index] = info;
            }
        }
        UpdateRoomListUI();
    }

    void UpdateRoomListUI()
    {
        foreach (Transform child in roomListContent) Destroy(child.gameObject);
        foreach (RoomInfo room in cachedRoomList)
        {
            if (room.IsOpen && room.IsVisible && room.PlayerCount < room.MaxPlayers)
            {
                RoomItem newRoom = Instantiate(roomItemPrefab, roomListContent);
                newRoom.SetRoomInfo(room.Name, this);
            }
        }
    }

    // ──────────── RANK HELPERS ────────────

    /// <summary>
    /// When another player's custom properties update (e.g. rank syncs),
    /// refresh the player list so their rank shows immediately.
    /// </summary>
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey("rank"))
        {
            UpdatePlayerList();
        }
    }

    /// <summary>
    /// Converts a numeric rank value (from DB currency column) into a display label.
    /// Customize the thresholds and names to fit your game's ranking tiers.
    /// </summary>
    private string GetRankLabel(int rankValue)
    {
        if (rankValue >= 5000) return "<color=#FF4500>Challenger</color>";
        if (rankValue >= 4000) return "<color=#E040FB>Master</color>";
        if (rankValue >= 3000) return "<color=#00BCD4>Diamond</color>";
        if (rankValue >= 2000) return "<color=#FFD700>Gold</color>";
        if (rankValue >= 1000) return "<color=#C0C0C0>Silver</color>";
        return "<color=#CD7F32>Bronze</color>";
    }
}