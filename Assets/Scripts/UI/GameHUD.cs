using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections.Generic;

/// <summary>
/// In-game HUD displaying health, ammo, round info, kill feed, and scoreboard.
/// Binds to local player's PlayerHealth, PlayerCombat, and the RoundManager.
/// </summary>
public class GameHUD : MonoBehaviour
{
    private const int MaxstatValue = 100;
    
    [Header("Network")]
    [SerializeField] private TextMeshProUGUI pingText;

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
    [SerializeField] private Transform scoreboardContent; // Vertical Layout Group olan konteyner
    [SerializeField] private GameObject scoreboardRowPrefab; // Oyuncu başına oluşturulacak satır prefab'ı

    [Header("Match Result")]
    [SerializeField] private GameObject matchResultPanel;
    [SerializeField] private TextMeshProUGUI matchResultText;

    [Header("Card Effects Display")]
    [SerializeField] private TextMeshProUGUI activeCardsText;
    
    [Header("Kill Cam")]
    [SerializeField] private GameObject killCamOverlay;

    private PlayerHealth _localHealth;
    private PlayerCombat _localCombat;
    private PlayerStats _localStats;
    private string[] _killFeedEntries;
    private int _killFeedIndex;
    
    // Satırları bellekte tutmak için (performans için sürekli Destroy/Instantiate yapmamak adına)
    private Dictionary<int, GameObject> _scoreboardRows = new Dictionary<int, GameObject>();

    // Ping timer (oyun başladığında hemen yazsın diye 3 ile başlatıyoruz)
    private float _pingUpdateTimer = 3f;

    private void Start()
    {
        _killFeedEntries = new string[maxKillFeedEntries];

        if (scoreboardPanel != null) scoreboardPanel.SetActive(false);
        if (matchResultPanel != null) matchResultPanel.SetActive(false);
        healthBar.maxValue = MaxstatValue;
        
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
        UpdatePingUI();
        UpdateKillCamUI();

        // Scoreboard toggle (Tab)
        if (scoreboardPanel != null)
        {
            bool showBoard = Input.GetKey(KeyCode.Tab);
            scoreboardPanel.SetActive(showBoard);
            if (showBoard) UpdateScoreboard();
        }
    }

    private void UpdateKillCamUI()
    {
        if (killCamOverlay == null) return;

        // PlayerCamera.Instance is usually the local player's camera
        if (PlayerCamera.Instance != null)
        {
            // Kill Cam is disabled for now, so we keep the overlay hidden
            killCamOverlay.SetActive(false);
        }
    }

    // ────────── Ping ──────────

    private void UpdatePingUI()
    {
        if (pingText == null) return;

        _pingUpdateTimer += Time.deltaTime;

        // Her 3 saniyede bir ping değerini güncelle
        if (_pingUpdateTimer >= 3f)
        {
            pingText.text = $"Ping: {PhotonNetwork.GetPing()} ms";
            _pingUpdateTimer = 0f;
        }
    }

    // ────────── Find Local Player ──────────

    private void FindLocalPlayer()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        PlayerHealth found = null;

        foreach (var player in players)
        {
            // Offline / direct scene entry — no Photon room, just grab the first one
            if (!PhotonNetwork.InRoom)
            {
                found = player;
                break;
            }

            // Online mode — only bind to OUR player, never a remote one
            var pv = player.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                found = player;
                break;
            }
        }

        if (found != null && found != _localHealth)
        {
            // Unsubscribe from old player (prevents event handler leak)
            if (_localHealth != null)
                _localHealth.OnHealthChanged -= OnHealthChanged;

            _localHealth = found;
            _localCombat = found.GetComponent<PlayerCombat>();
            _localStats = found.GetComponent<PlayerStats>();

            _localHealth.OnHealthChanged += OnHealthChanged;
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
        if (ScoreManager.Instance == null || scoreboardContent == null || scoreboardRowPrefab == null) return;

        var leaderboard = ScoreManager.Instance.GetLeaderboard();

        // Önce mevcut satırların hepsini sakla, böylece ihtiyacımız olmayanları gizleyebiliriz
        HashSet<int> activeActorNumbers = new HashSet<int>();

        foreach (var (actorNumber, score) in leaderboard)
        {
            activeActorNumbers.Add(actorNumber);
            GameObject rowObj;

            // Eğer bu oyuncu için bir satır daha önce oluşturulmadıysa oluştur
            if (!_scoreboardRows.TryGetValue(actorNumber, out rowObj))
            {
                rowObj = Instantiate(scoreboardRowPrefab, scoreboardContent);
                _scoreboardRows[actorNumber] = rowObj;
            }

            rowObj.SetActive(true);

            // Prefab içindeki elementleri sıralamasına (index) göre buluyoruz.
            // Sıra: 0=İsim, 1=Kill, 2=Death, 3=Win, 4=Ping
            var texts = rowObj.GetComponentsInChildren<TextMeshProUGUI>();

            if (texts.Length >= 4)
            {
                string name = GetPlayerName(actorNumber);

                // Get rank from Photon custom properties
                int rank = GetPlayerRank(actorNumber);
                string rankLabel = GetRankLabel(rank);

                texts[0].text = $"{rankLabel} {name}";
                texts[1].text = score.kills.ToString();
                texts[2].text = score.deaths.ToString();
                texts[3].text = score.roundWins.ToString();

                // Eğer 5. Text (Ping Texti) prefabınızda mevcutsa:
                if (texts.Length >= 5)
                {
                    // Sadece sana ait olan pingi alabilirsin (Diğerlerinin pingini bilmek için sunucudan çekmek gerekir)
                    if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        texts[4].text = PhotonNetwork.GetPing().ToString() + " ms";
                    }
                    else
                    {
                        // Başka oyuncuların pingini eğer sync'lemediysen "?" veya "---" yapabilirsin.
                        texts[4].text = "--- ms"; 
                    }
                }
            }
            
            // Eğer ben representsen satırı belirginleştir
            var image = rowObj.GetComponent<Image>();
            if (image != null)
            {
                image.color = (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber) 
                              ? new Color(0.2f, 0.6f, 0.2f, 0.8f) // Bensem yeşilimsi
                              : new Color(0.1f, 0.1f, 0.1f, 0.66f); // Başkasıysa koyu
            }
        }

        // Oyundan çıkanlar varsa satırını gizle
        foreach (var kvp in _scoreboardRows)
        {
            if (!activeActorNumbers.Contains(kvp.Key))
            {
                kvp.Value.SetActive(false);
            }
        }
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

    private int GetPlayerRank(int actorNumber)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
            {
                if (player.CustomProperties.TryGetValue("rank", out object rankVal) && rankVal is int r)
                    return r;
            }
        }
        return 0;
    }

    /// <summary>
    /// Converts a numeric rank value into a colored display label.
    /// Must match the tiers in LobbyManager.GetRankLabel().
    /// </summary>
    private string GetRankLabel(int rankValue)
    {
        if (rankValue >= 5000) return "<color=#FF4500>Challenger</color>";
        if (rankValue >= 4000) return "<color=#E040FB>Master</color>";
        if (rankValue >= 3000) return "<color=#00BCD4>Diamond</color>";
        if (rankValue >= 2000) return "<color=#FFD700>Gold</color>";
        if (rankValue >= 1000) return "<color=#C0C0C0>Silver</color>";
        return "<color=#CD7F32>Bronze</color>";
    }
}
