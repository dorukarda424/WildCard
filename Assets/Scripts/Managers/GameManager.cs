using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Player Info")]
    public string loggedInPlayerName;

    [Header("Match Settings")]
    [Tooltip("Number of round wins needed to win the match.")]
    public int roundsToWin = 5;
    [Tooltip("Maximum players per room.")]
    public int maxPlayers = 4;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}