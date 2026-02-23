using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

/// <summary>
/// In-game HUD displaying health, ammo, round info, kill feed, and scoreboard.
/// Binds to local player's PlayerHealth, PlayerCombat, and the RoundManager.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Ammo")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI reloadText;

    [Header("Round Info")]
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Kill Feed")]
    [SerializeField] private TextMeshProUGUI killFeedText;
    [SerializeField] private int maxKillFeedEntries = 5;

    [Header("Scoreboard")]
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private TextMeshProUGUI scoreboardText;

    [Header("Match Result")]
    [SerializeField] private GameObject matchResultPanel;
    [SerializeField] private TextMeshProUGUI matchResultText;

    [Header("Card Effects Display")]
    [SerializeField] private TextMeshProUGUI activeCardsText;

    private PlayerHealth _localHealth;
    private PlayerCombat _localCombat;
    private PlayerStats _localStats;
    private string[] _killFeedEntries;
    private int _killFeedIndex;

    private void Start()
    {
        _killFeedEntries = new string[maxKillFeedEntries];

        if (scoreboardPanel != null) scoreboardPanel.SetActive(false);
        if (matchResultPanel != null) matchResultPanel.SetActive(false);

        // Subscribe to RoundManager events
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnStateChanged += OnRoundStateChanged;
            RoundManager.Instance.OnPlayerKilled += OnPlayerKilled;
            RoundManager.Instance.OnMatchWon += OnMatchWon;
        }
    }

    private void OnDestroy()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnStateChanged -= OnRoundStateChanged;
            RoundManager.Instance.OnPlayerKilled -= OnPlayerKilled;
            RoundManager.Instance.OnMatchWon -= OnMatchWon;
        }
    }

    private void Update()
    {
        // Find local player if not cached
        if (_localHealth == null) FindLocalPlayer();

        UpdateHealthUI();
        UpdateAmmoUI();
        UpdateRoundUI();
        UpdateActiveCardsUI();

        // Scoreboard toggle (Tab)
        if (scoreboardPanel != null)
        {
            bool showBoard = Input.GetKey(KeyCode.Tab);
            scoreboardPanel.SetActive(showBoard);
            if (showBoard) UpdateScoreboard();
        }
    }

    // ────────── Find Local Player ──────────

    private void FindLocalPlayer()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.photonView.IsMine)
            {
                _localHealth = player;
                _localCombat = player.GetComponent<PlayerCombat>();
                _localStats = player.GetComponent<PlayerStats>();

                // Subscribe to health changes
                _localHealth.OnHealthChanged += OnHealthChanged;
                break;
            }
        }
    }

    // ────────── Health ──────────

    private void OnHealthChanged(float current, float max)
    {
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (_localHealth == null) return;

        if (healthBar != null)
        {
            healthBar.maxValue = _localHealth.MaxHealth;
            healthBar.value = _localHealth.CurrentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(_localHealth.CurrentHealth)} / {Mathf.CeilToInt(_localHealth.MaxHealth)}";
        }
    }

    // ────────── Ammo ──────────

    private void UpdateAmmoUI()
    {
        if (_localCombat == null) return;

        if (ammoText != null)
        {
            ammoText.text = $"{_localCombat.CurrentAmmo} / {_localCombat.MaxAmmo}";
        }

        if (reloadText != null)
        {
            reloadText.gameObject.SetActive(_localCombat.IsReloading);
            if (_localCombat.IsReloading)
            {
                reloadText.text = "RELOADING...";
            }
        }
    }

    // ────────── Round Info ──────────

    private void UpdateRoundUI()
    {
        if (RoundManager.Instance == null) return;

        if (roundText != null)
        {
            roundText.text = $"Round {RoundManager.Instance.CurrentRound}";
        }

        if (countdownText != null)
        {
            var state = RoundManager.Instance.CurrentState;
            if (state == RoundManager.RoundState.Countdown)
            {
                countdownText.gameObject.SetActive(true);
                countdownText.text = Mathf.CeilToInt(RoundManager.Instance.StateTimer).ToString();
            }
            else
            {
                countdownText.gameObject.SetActive(false);
            }
        }
    }

    private void OnRoundStateChanged(RoundManager.RoundState state)
    {
        if (stateText != null)
        {
            switch (state)
            {
                case RoundManager.RoundState.WaitingForPlayers:
                    stateText.text = "WAITING FOR PLAYERS...";
                    break;
                case RoundManager.RoundState.Countdown:
                    stateText.text = "GET READY!";
                    break;
                case RoundManager.RoundState.Fighting:
                    stateText.text = "FIGHT!";
                    break;
                case RoundManager.RoundState.RoundOver:
                    string winner = GetPlayerName(RoundManager.Instance.RoundWinnerActorNumber);
                    stateText.text = $"{winner} WINS THE ROUND!";
                    break;
                case RoundManager.RoundState.CardSelection:
                    stateText.text = "CHOOSE YOUR CARD";
                    break;
                case RoundManager.RoundState.MatchOver:
                    stateText.text = "MATCH OVER";
                    break;
            }
        }
    }

    // ────────── Kill Feed ──────────

    private void OnPlayerKilled(int victimActorNumber, int killerActorNumber)
    {
        string killer = GetPlayerName(killerActorNumber);
        string victim = GetPlayerName(victimActorNumber);
        string entry = $"<color=#FF4444>{killer}</color> → <color=#4488FF>{victim}</color>";

        _killFeedEntries[_killFeedIndex % maxKillFeedEntries] = entry;
        _killFeedIndex++;

        if (killFeedText != null)
        {
            var sb = new System.Text.StringBuilder();
            int start = Mathf.Max(0, _killFeedIndex - maxKillFeedEntries);
            for (int i = start; i < _killFeedIndex; i++)
            {
                sb.AppendLine(_killFeedEntries[i % maxKillFeedEntries]);
            }
            killFeedText.text = sb.ToString().TrimEnd();
        }
    }

    // ────────── Scoreboard ──────────

    private void UpdateScoreboard()
    {
        if (ScoreManager.Instance == null || scoreboardText == null) return;

        var leaderboard = ScoreManager.Instance.GetLeaderboard();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>PLAYER          K    D    W</b>");
        sb.AppendLine("─────────────────────────────");

        foreach (var (actorNumber, score) in leaderboard)
        {
            string name = GetPlayerName(actorNumber);
            if (name.Length > 14) name = name.Substring(0, 14);
            sb.AppendLine($"{name,-16}{score.kills,-5}{score.deaths,-5}{score.roundWins}");
        }

        scoreboardText.text = sb.ToString();
    }

    // ────────── Match Result ──────────

    private void OnMatchWon(int winnerActorNumber)
    {
        if (matchResultPanel != null) matchResultPanel.SetActive(true);
        if (matchResultText != null)
        {
            string winner = GetPlayerName(winnerActorNumber);
            bool isLocalWinner = PhotonNetwork.LocalPlayer.ActorNumber == winnerActorNumber;
            matchResultText.text = isLocalWinner ? "YOU WIN!" : $"{winner} WINS!";
        }
    }

    // ────────── Active Cards ──────────

    private void UpdateActiveCardsUI()
    {
        if (_localStats == null || activeCardsText == null) return;

        if (_localStats.AppliedCardIds.Count == 0)
        {
            activeCardsText.text = "";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>Active Cards:</b>");
        foreach (var cardId in _localStats.AppliedCardIds)
        {
            sb.AppendLine($"• {cardId}");
        }
        activeCardsText.text = sb.ToString().TrimEnd();
    }

    // ────────── Helpers ──────────

    private string GetPlayerName(int actorNumber)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
                return string.IsNullOrEmpty(player.NickName) ? $"Player {actorNumber}" : player.NickName;
        }
        return $"Player {actorNumber}";
    }
}
