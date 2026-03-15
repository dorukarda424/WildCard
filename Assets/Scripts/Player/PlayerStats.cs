using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Central stat container for a player. Holds base stats and card-applied modifiers.
/// Attached to the Player prefab alongside PlayerController.
/// Stats are synced across the network via Photon serialization.
/// </summary>
public class PlayerStats : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Base Stats")]
    [SerializeField] private float baseHealth = 100f;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float baseFireRate = 0.3f;       // seconds between shots
    [SerializeField] private int   baseMaxAmmo = 8;
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float baseBulletSpeed = 40f;
    [SerializeField] private float baseReloadSpeed = 1.5f;    // seconds to reload
    [SerializeField] private float baseSprintSpeed = 10f;
    [SerializeField] private float baseCrouchSpeed = 2.5f;
    [SerializeField] private float baseJumpForce = 10f;
    [SerializeField] private float baseGravity = -20f;
    [SerializeField] private int   baseMaxJumps = 1;
    [SerializeField] private float baseMaxFallSpeed = -30f;

    // Runtime modifiers accumulated from cards
    private Dictionary<StatType, float> _flatBonuses = new Dictionary<StatType, float>();
    private Dictionary<StatType, float> _percentBonuses = new Dictionary<StatType, float>();

    // Active special effects (flags)
    public SpecialEffect ActiveEffects { get; private set; } = SpecialEffect.None;

    // Applied card IDs for network sync and display
    private List<string> _appliedCardIds = new List<string>();
    public IReadOnlyList<string> AppliedCardIds => _appliedCardIds;

    // ────────── Computed Properties ──────────

    public float MaxHealth    => GetStat(StatType.Health, baseHealth);
    public float Damage       => GetStat(StatType.Damage, baseDamage);
    public float FireRate     => GetStat(StatType.FireRate, baseFireRate);
    public int   MaxAmmo      => Mathf.RoundToInt(GetStat(StatType.MaxAmmo, baseMaxAmmo));
    public float MoveSpeed    => GetStat(StatType.MoveSpeed, baseMoveSpeed);
    public float BulletSpeed  => GetStat(StatType.BulletSpeed, baseBulletSpeed);
    public float ReloadSpeed  => GetStat(StatType.ReloadSpeed, baseReloadSpeed);
    public float SprintSpeed  => GetStat(StatType.SprintSpeed, baseSprintSpeed);
    public float CrouchSpeed  => GetStat(StatType.CrouchSpeed, baseCrouchSpeed);
    public float JumpForce    => GetStat(StatType.JumpForce, baseJumpForce);
    public float Gravity      => GetStat(StatType.Gravity, baseGravity, allowNegative: true);
    public int   MaxJumps     => Mathf.RoundToInt(GetStat(StatType.MaxJumps, baseMaxJumps));
    public float MaxFallSpeed => GetStat(StatType.MaxFallSpeed, baseMaxFallSpeed, allowNegative: true);

    /// <summary>
    /// Returns effective max jumps, accounting for DoubleJump special effect.
    /// </summary>
    public int EffectiveMaxJumps
    {
        get
        {
            int jumps = MaxJumps;
            if (HasEffect(SpecialEffect.DoubleJump)) jumps = Mathf.Max(jumps, 2);
            return jumps;
        }
    }

    // ────────── Core Methods ──────────

    /// <summary>
    /// Apply a card's modifiers and special effects to this player.
    /// </summary>
    public void ApplyCard(CardData card)
    {
        if (card == null) return;

        foreach (var mod in card.modifiers)
        {
            switch (mod.mode)
            {
                case ModifierMode.Flat:
                    AddBonus(_flatBonuses, mod.statType, mod.value);
                    break;
                case ModifierMode.Percentage:
                    AddBonus(_percentBonuses, mod.statType, mod.value);
                    break;
            }
        }

        ActiveEffects |= card.specialEffects;
        _appliedCardIds.Add(card.CardId);

        Debug.Log($"[PlayerStats] Applied card: {card.cardName} to {gameObject.name}");
    }

    /// <summary>
    /// Reset all card bonuses. Called at match start.
    /// </summary>
    public void ResetStats()
    {
        _flatBonuses.Clear();
        _percentBonuses.Clear();
        ActiveEffects = SpecialEffect.None;
        _appliedCardIds.Clear();

        Debug.Log($"[PlayerStats] Stats reset for {gameObject.name}");
    }

    /// <summary>
    /// Check if a special effect is active.
    /// </summary>
    public bool HasEffect(SpecialEffect effect)
    {
        return (ActiveEffects & effect) != 0;
    }

    // ────────── Internal Helpers ──────────

    private float GetStat(StatType type, float baseValue, bool allowNegative = false)
    {
        float flat = 0f;
        float percent = 0f;

        if (_flatBonuses.TryGetValue(type, out float f)) flat = f;
        if (_percentBonuses.TryGetValue(type, out float p)) percent = p;

        float result = (baseValue + flat) * (1f + percent);
        return allowNegative ? result : Mathf.Max(result, 0.01f);
    }

    private void AddBonus(Dictionary<StatType, float> dict, StatType type, float value)
    {
        if (dict.ContainsKey(type))
            dict[type] += value;
        else
            dict[type] = value; 
    }

    // ────────── Network Sync ──────────

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext((int)ActiveEffects);
            stream.SendNext(_appliedCardIds.Count);
            foreach (var id in _appliedCardIds)
            {
                stream.SendNext(id);
            }
        }
        else
        {
            ActiveEffects = (SpecialEffect)(int)stream.ReceiveNext();
            int count = (int)stream.ReceiveNext();
            _appliedCardIds.Clear();
            for (int i = 0; i < count; i++)
            {
                _appliedCardIds.Add((string)stream.ReceiveNext());
            }
        }
    }

    /// <summary>
    /// Called via RPC when a remote player picks a card.
    /// Rebuilds local stat modifiers from the card database.
    /// </summary>
    [PunRPC]
    public void RPC_ApplyCard(string cardId)
    {
        CardDatabase db = FindCardDatabase();
        if (db == null)
        {
            Debug.LogError("[PlayerStats] CardDatabase not found!");
            return;
        }

        CardData card = db.GetCardById(cardId);
        if (card != null)
        {
            ApplyCard(card);
        }
        else
        {
            Debug.LogError($"[PlayerStats] Card not found: {cardId}");
        }
    }

    /// <summary>
    /// Apply a card locally and sync via RPC to all players.
    /// Call this on the owning client when a card is selected.
    /// </summary>
    public void ApplyCardNetworked(CardData card)
    {
        ApplyCard(card);
        photonView.RPC(nameof(RPC_ApplyCard), RpcTarget.Others, card.CardId);
    }

    /// <summary>
    /// Reset stats locally and sync via RPC to all players.
    /// </summary>
    public void ResetStatsNetworked()
    {
        ResetStats();
        photonView.RPC(nameof(RPC_ResetStats), RpcTarget.Others);
    }

    [PunRPC]
    private void RPC_ResetStats()
    {
        ResetStats();
    }

    private CardDatabase FindCardDatabase()
    {
        // Look in Resources folder first
        CardDatabase db = Resources.Load<CardDatabase>("CardDatabase");
        if (db != null) return db;

        // Fallback: find in scene
        var managers = FindObjectsByType<CardSelectionManager>(FindObjectsSortMode.None);
        if (managers.Length > 0)
            return managers[0].cardDatabase;

        return null;
    }
}
