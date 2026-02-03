using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

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
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }


    public void CreateRoom()
    {
        if (string.IsNullOrEmpty(createInput.text)) return;

        RoomOptions options = new RoomOptions { MaxPlayers = 4, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(createInput.text, options);
    }

    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }


    public override void OnJoinedRoom()
    {
        Debug.Log("Odaya girildi!");
        lobbyPanel.SetActive(false);
        roomPanel.SetActive(true);

        roomNameText.text = "Oda: " + PhotonNetwork.CurrentRoom.Name;

        UpdatePlayerList();


        startGameButton.interactable = PhotonNetwork.IsMasterClient;
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

            newPlayerItem.GetComponent<TextMeshProUGUI>().text = player.NickName;
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


    public void OnClickStartGame()
    {

        if (PhotonNetwork.IsMasterClient)
        {

            PhotonNetwork.LoadLevel("SampleScene");
        }
    }

    public void OnClickLeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }


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
}