using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Tracks kills, deaths, and round wins per player.
/// Uses Photon Custom Properties for persistence.
/// </summary>
public class ScoreManager : MonoBehaviourPunCallbacks
{
    public static ScoreManager Instance { get; private set; }

    // Local cache of scores (in case Custom Properties haven't synced yet)
    private Dictionary<int, PlayerScore> _scores = new Dictionary<int, PlayerScore>();

    // ────────── Events ──────────

    public System.Action<int, int> OnKillRecorded; // killer, victim
    public System.Action<int> OnRoundWinRecorded;  // winner

    // ────────── Data ──────────

    [System.Serializable]
    public struct PlayerScore
    {
        public int kills;
        public int deaths;
        public int roundWins;

        public override string ToString() => $"K:{kills} D:{deaths} W:{roundWins}";
    }

    // ────────── Lifecycle ──────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ────────── Score Tracking ──────────

    public void RecordKill(int killerActorNumber, int victimActorNumber)
    {
        // Update killer
        var killerScore = GetScore(killerActorNumber);
        killerScore.kills++;
        _scores[killerActorNumber] = killerScore;

        // Update victim
        var victimScore = GetScore(victimActorNumber);
        victimScore.deaths++;
        _scores[victimActorNumber] = victimScore;

        // Sync to Photon Custom Properties
        SyncToPhoton(killerActorNumber);
        SyncToPhoton(victimActorNumber);

        OnKillRecorded?.Invoke(killerActorNumber, victimActorNumber);
        Debug.Log($"[ScoreManager] Kill: {killerActorNumber} → {victimActorNumber} | " +
                  $"Killer: {killerScore} | Victim: {victimScore}");
    }

    public void RecordRoundWin(int winnerActorNumber)
    {
        var score = GetScore(winnerActorNumber);
        score.roundWins++;
        _scores[winnerActorNumber] = score;

        SyncToPhoton(winnerActorNumber);
        OnRoundWinRecorded?.Invoke(winnerActorNumber);

        Debug.Log($"[ScoreManager] Round win: {winnerActorNumber} | Total wins: {score.roundWins}");
    }

    // ────────── Queries ──────────

    public PlayerScore GetScore(int actorNumber)
    {
        if (_scores.TryGetValue(actorNumber, out var score))
            return score;
        return new PlayerScore();
    }

    public int GetRoundWins(int actorNumber)
    {
        return GetScore(actorNumber).roundWins;
    }

    public int GetKills(int actorNumber)
    {
        return GetScore(actorNumber).kills;
    }

    /// <summary>
    /// Get all scores sorted by round wins (descending).
    /// </summary>
    public List<(int actorNumber, PlayerScore score)> GetLeaderboard()
    {
        var list = new List<(int actorNumber, PlayerScore score)>();
        foreach (var kvp in _scores)
        {
            list.Add((kvp.Key, kvp.Value));
        }
        list.Sort((a, b) => b.score.roundWins.CompareTo(a.score.roundWins));
        return list;
    }

    /// <summary>
    /// Reset all scores for a new match.
    /// </summary>
    public void ResetAllScores()
    {
        _scores.Clear();
        Debug.Log("[ScoreManager] All scores reset.");
    }

    // ────────── Photon Sync ──────────

    private void SyncToPhoton(int actorNumber)
    {
        var player = GetPhotonPlayer(actorNumber);
        if (player == null) return;

        var score = GetScore(actorNumber);
        var props = new ExitGames.Client.Photon.Hashtable
        {
            { "kills", score.kills },
            { "deaths", score.deaths },
            { "roundWins", score.roundWins }
        };
        player.SetCustomProperties(props);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // Update local cache from Photon properties
        int actor = targetPlayer.ActorNumber;
        var score = GetScore(actor);

        if (changedProps.TryGetValue("kills", out object k)) score.kills = (int)k;
        if (changedProps.TryGetValue("deaths", out object d)) score.deaths = (int)d;
        if (changedProps.TryGetValue("roundWins", out object w)) score.roundWins = (int)w;

        _scores[actor] = score;
    }

    private Player GetPhotonPlayer(int actorNumber)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber) return player;
        }
        return null;
    }
}
