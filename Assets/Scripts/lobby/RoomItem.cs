using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
    public TextMeshProUGUI roomNameText;
    private LobbyManager manager;
    private string roomName;

    void Start()
    {

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnClickItem);
        }
    }

    public void SetRoomInfo(string _roomName, LobbyManager _manager)
    {
        roomName = _roomName;
        roomNameText.text = _roomName;
        manager = _manager;
    }

    public void OnClickItem()
    {
        Debug.Log("Clicked on room: " + roomName);

        if (manager != null)
        {
            manager.JoinRoom(roomName);
        }
        else
        {
            Debug.LogError("LobbyManager ref is null in RoomItem!");
        }
    }
}