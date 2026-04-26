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
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // When a new scene loads, re-read scores from Photon Custom Properties
        // so we don't lose data accumulated in previous rounds.
        BootstrapFromPhoton();
    }

    /// <summary>
    /// Reads existing scores from Photon Custom Properties for every player in the room.
    /// Called on scene load to restore data that the local _scores cache lost.
    /// </summary>
    private void BootstrapFromPhoton()
    {
        if (!PhotonNetwork.InRoom) return;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            int actor = player.ActorNumber;
            var score = new PlayerScore();

            if (player.CustomProperties.TryGetValue("kills", out object k) && k is int ki)
                score.kills = ki;
            if (player.CustomProperties.TryGetValue("deaths", out object d) && d is int di)
                score.deaths = di;
            if (player.CustomProperties.TryGetValue("roundWins", out object w) && w is int wi)
                score.roundWins = wi;

            _scores[actor] = score;
        }

        Debug.Log($"[ScoreManager] Bootstrapped {_scores.Count} player scores from Photon.");
    }

    // ────────── Score Tracking ──────────

    public void RecordKill(int killerActorNumber, int victimActorNumber)
    {
        // Self-kill: only count death, no kill credit
        if (killerActorNumber == victimActorNumber)
        {
            var victimScore = GetScore(victimActorNumber);
            victimScore.deaths++;
            _scores[victimActorNumber] = victimScore;
            SyncToPhoton(victimActorNumber);
            OnKillRecorded?.Invoke(killerActorNumber, victimActorNumber);
            return;
        }

        // Update killer
        var killerScore = GetScore(killerActorNumber);
        killerScore.kills++;
        _scores[killerActorNumber] = killerScore;

        // Update victim
        var vScore = GetScore(victimActorNumber);
        vScore.deaths++;
        _scores[victimActorNumber] = vScore;

        // Sync to Photon Custom Properties
        SyncToPhoton(killerActorNumber);
        SyncToPhoton(victimActorNumber);

        OnKillRecorded?.Invoke(killerActorNumber, victimActorNumber);
        Debug.Log($"[ScoreManager] Kill: {killerActorNumber} → {victimActorNumber} | " +
                  $"Killer: {killerScore} | Victim: {vScore}");
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
        // Make sure every player in the room has at least an empty entry
        EnsureAllPlayersRegistered();

        var list = new List<(int actorNumber, PlayerScore score)>();
        foreach (var kvp in _scores)
        {
            list.Add((kvp.Key, kvp.Value));
        }
        list.Sort((a, b) => b.score.roundWins.CompareTo(a.score.roundWins));
        return list;
    }

    /// <summary>
    /// Ensures every player currently in the Photon room has a score entry,
    /// even if they haven't scored yet. Prevents players from being invisible
    /// on the scoreboard.
    /// </summary>
    private void EnsureAllPlayersRegistered()
    {
        if (!PhotonNetwork.InRoom) return;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!_scores.ContainsKey(player.ActorNumber))
            {
                _scores[player.ActorNumber] = new PlayerScore();
            }
        }
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
