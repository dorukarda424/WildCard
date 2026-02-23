using UnityEngine;
using Photon.Pun;
using System;

/// <summary>
/// Networked health system for multiplayer FFA.
/// Handles taking damage, death, and respawning.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : MonoBehaviourPunCallbacks, IPunObservable
{
    private PlayerStats _stats;
    private float _currentHealth;
    private bool _isDead;
    private int _shieldCharges;

    // ────────── Events ──────────

    /// <summary>Fired when health changes. Args: current, max.</summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>Fired when this player dies. Args: victimActorNumber, killerActorNumber.</summary>
    public event Action<int, int> OnDied;

    /// <summary>Fired when this player respawns.</summary>
    public event Action OnRespawned;

    // ────────── Properties ──────────

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _stats != null ? _stats.MaxHealth : 100f;
    public bool IsDead => _isDead;
    public float HealthPercent => MaxHealth > 0 ? _currentHealth / MaxHealth : 0f;

    // ────────── Lifecycle ──────────

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
    }

    private void Start()
    {
        InitializeHealth();
    }

    /// <summary>
    /// Called at round start to fully heal the player.
    /// </summary>
    public void InitializeHealth()
    {
        _currentHealth = MaxHealth;
        _isDead = false;
        _shieldCharges = _stats.HasEffect(SpecialEffect.Shield) ? 1 : 0;
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    // ────────── Damage ──────────

    /// <summary>
    /// Deal damage to this player. Only processed on the owning client.
    /// Called via RPC from the attacker.
    /// </summary>
    [PunRPC]
    public void RPC_TakeDamage(float amount, int attackerActorNumber)
    {
        if (_isDead) return;

        // Shield blocks one hit
        if (_shieldCharges > 0)
        {
            _shieldCharges--;
            Debug.Log($"[PlayerHealth] Shield absorbed hit from actor {attackerActorNumber}");
            return;
        }

        _currentHealth -= amount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, MaxHealth);

        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

        Debug.Log($"[PlayerHealth] {gameObject.name} took {amount} dmg from actor {attackerActorNumber}. " +
                  $"HP: {_currentHealth}/{MaxHealth}");

        if (_currentHealth <= 0f)
        {
            Die(attackerActorNumber);
        }
    }

    /// <summary>
    /// Public method to request damage on a remote player.
    /// Call this on the attacker's client — it sends an RPC to the target.
    /// </summary>
    public void TakeDamageFromNetwork(float amount, int attackerActorNumber)
    {
        photonView.RPC(nameof(RPC_TakeDamage), RpcTarget.All, amount, attackerActorNumber);
    }

    /// <summary>
    /// Heal this player by the given amount.
    /// </summary>
    public void Heal(float amount)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    // ────────── Death & Respawn ──────────

    private void Die(int killerActorNumber)
    {
        if (_isDead) return;
        _isDead = true;

        int victimActorNumber = photonView.Owner.ActorNumber;
        Debug.Log($"[PlayerHealth] {gameObject.name} killed by actor {killerActorNumber}");

        OnDied?.Invoke(victimActorNumber, killerActorNumber);

        // Notify the RoundManager that a player has died
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnPlayerDied(victimActorNumber, killerActorNumber);
        }

        // Disable the player visuals/controls (but keep the GameObject alive for networking)
        SetPlayerActive(false);
    }

    /// <summary>
    /// Respawn this player at a given position. Called by RoundManager.
    /// </summary>
    public void Respawn(Vector3 position)
    {
        _isDead = false;
        _currentHealth = MaxHealth;
        _shieldCharges = _stats.HasEffect(SpecialEffect.Shield) ? 1 : 0;

        transform.position = position;
        SetPlayerActive(true);

        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        OnRespawned?.Invoke();

        Debug.Log($"[PlayerHealth] {gameObject.name} respawned at {position}");
    }

    private void SetPlayerActive(bool active)
    {
        // Disable/enable renderers and gameplay components
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = active;

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = active;

        // Disable the PlayerController so dead players can't move
        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = active;

        var combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.enabled = active;
    }

    // ────────── Network Sync ──────────

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_currentHealth);
            stream.SendNext(_isDead);
        }
        else
        {
            _currentHealth = (float)stream.ReceiveNext();
            _isDead = (bool)stream.ReceiveNext();
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        }
    }
}
