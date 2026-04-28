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
        // Auto-match countdown is now handled entirely by WarmupManager.
        // Nothing to do here for auto-match.
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
            IsOpen = true,
            // Mark this room as a Quick Play room so the manual room list can filter it out
            CustomRoomProperties = new Hashtable { { "qp", true } },
            CustomRoomPropertiesForLobby = new string[] { "qp" }
        };

        // null name = Photon generates a unique GUID name
        PhotonNetwork.CreateRoom(null, options);
    }

    // NOTE: Auto-match countdown and game start logic has been moved
    // entirely to WarmupManager (in the WarmupLobby scene).
    // LobbyManager only handles joining/creating the room and loading WarmupLobby.

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

        // Detect if we joined a Quick Play room (either via button or from room list)
        bool isQuickPlayRoom = false;
        object qpVal;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("qp", out qpVal) && qpVal is bool qp && qp)
        {
            isQuickPlayRoom = true;
        }

        if (_isAutoMatch || isQuickPlayRoom)
        {
            // Auto match / Quick Play room → Load the playable warmup lobby scene
            // WarmupManager in that scene handles spawning, countdown, and match start
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Loading warmup...";
            }

            // ── Disable auto-match flag so no stale logic fires while we transition. ──
            _isAutoMatch = false;

            LoadingScreenManager.Show();

            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.LoadLevel("WarmupLobby");
            }
            // Non-master: AutomaticallySyncScene will load WarmupLobby automatically
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
    }

    /// <summary>
    /// If the Master Client leaves, update UI for custom lobby.
    /// Auto-match countdown takeover is handled by WarmupManager.
    /// </summary>
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
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

            LoadingScreenManager.Show();
            PhotonNetwork.LoadLevel("level 1");
        }
    }

    public void OnClickLeaveRoom()
    {
        _isAutoMatch = false;
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
            if (!room.IsOpen || !room.IsVisible || room.PlayerCount >= room.MaxPlayers)
                continue;

            // Skip Quick Play rooms — they should only be joined via the Quick Play button
            object qpVal;
            if (room.CustomProperties.TryGetValue("qp", out qpVal) && qpVal is bool qp && qp)
                continue;

            RoomItem newRoom = Instantiate(roomItemPrefab, roomListContent);
            newRoom.SetRoomInfo(room.Name, this);
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