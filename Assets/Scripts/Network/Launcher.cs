using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class Launcher : MonoBehaviourPunCallbacks
{
    [SerializeField] Transform[] spawnPoints;
    void Start()
    {

        if (GameManager.instance != null)
        {
            PhotonNetwork.NickName = GameManager.instance.loggedInPlayerName;
        }
        else
        {

            PhotonNetwork.NickName = "EditorPlayer";
        }

        Debug.Log("player connecting... player: " + PhotonNetwork.NickName);
        PhotonNetwork.ConnectUsingSettings();
    }


    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected");
        PhotonNetwork.JoinLobby(); 
    }


    public override void OnJoinedLobby()
    {
        Debug.Log("Joined to lobby");
        PhotonNetwork.JoinRandomRoom();
    }


    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("No room to join creating a room");

        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 4 });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined to the room! current player count: " + PhotonNetwork.CurrentRoom.PlayerCount);


        int randomIndex = Random.Range(0, spawnPoints.Length);

        Vector3 spawnPos = spawnPoints[randomIndex].position;

        PhotonNetwork.Instantiate("Player", spawnPos, Quaternion.identity);
    }
}