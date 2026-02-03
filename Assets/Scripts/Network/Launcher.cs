using UnityEngine;
using Photon.Pun;

public class Launcher : MonoBehaviourPunCallbacks
{
    [SerializeField] Transform[] spawnPoints;
    public string playerPrefabName = "Player";

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
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
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        Vector3 spawnPos = Vector3.zero;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            spawnPos = spawnPoints[randomIndex].position;
        }

        PhotonNetwork.Instantiate(playerPrefabName, spawnPos, Quaternion.identity);
        Debug.Log("Player instantiated at: " + spawnPos);
    }
}