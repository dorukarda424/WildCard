using UnityEngine;
using Photon.Pun;

public class Launcher : MonoBehaviourPunCallbacks
{
    [SerializeField] Transform[] spawnPoints;
    public string playerPrefabName = "Player";
    [SerializeField] private Camera menuCamera;

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            // Do not spawn here; let Level manager handle it when scene loads
            Debug.Log("Already in a room. Waiting for scene load...");
        }
        else
        {
            Debug.LogWarning("Not in a room. Connecting to server...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master.");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby. Joining random room...");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("Join random failed. Creating room...");
        PhotonNetwork.CreateRoom(null, new Photon.Realtime.RoomOptions { MaxPlayers = 4 });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room.");
        // Player spawn is now handled by the level's manager, not the lobby launcher
    }
}