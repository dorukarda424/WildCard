using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Player Info")]
    public string loggedInPlayerName;
    [Tooltip("Player rank fetched from database (currency column).")]
    public int playerRank = 0;

    [Header("Match Settings")]
    [Tooltip("Number of round wins needed to win the match.")]
    public int roundsToWin = 5;
    [Tooltip("Maximum players per room.")]
    public int maxPlayers = 4;

    [Header("Map Rotation")]
    [Tooltip("Tracks which map in the rotation we are on. Set by RoundManager.")]
    [HideInInspector] public int currentMapIndex = 0;

    [Header("Round Tracking")]
    [Tooltip("Current round number. Persisted across scene transitions by RoundManager.")]
    [HideInInspector] public int currentRound = 0;

    [Header("Local Persisted Data")]
    [Tooltip("Locally saved cards to bypass Photon network delays during quick scene transitions.")]
    public List<string> localPlayerCards = new List<string>();

    [Header("Next Scene")]
    public string nextSceneName;
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

    /// <summary>
    /// Reset map index and round counter when a new match begins.
    /// </summary>
    public void ResetMapRotation()
    {
        currentMapIndex = 0;
        currentRound = 0;
        localPlayerCards.Clear();
    }
}