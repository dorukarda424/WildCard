using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PlayerStats : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Base Stats")]
    [SerializeField] private float baseHealth = 100f;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float baseFireRate = 0.3f;
    [SerializeField] private int   baseMaxAmmo = 8;
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float baseBulletSpeed = 40f;
    [SerializeField] private float baseReloadSpeed = 1.5f;
    
    private Dictionary<StatType, float> _flatBonuses = new Dictionary<StatType, float>();
    private Dictionary<StatType, float> _percentBonuses = new Dictionary<StatType, float>();
    
    public SpecialEffect ActiveEffects { get; private set; } = SpecialEffect.None;
    
    private List<string> _appliedCardIds = new List<string>();
    public IReadOnlyList<string> AppliedCardIds => _appliedCardIds;
    
    public float MaxHealth    => GetStat(StatType.Health, baseHealth);
    public float Damage       => GetStat(StatType.Damage, baseDamage);
    public float FireRate     => GetStat(StatType.FireRate, baseFireRate);
    public int   MaxAmmo      => Mathf.RoundToInt(GetStat(StatType.MaxAmmo, baseMaxAmmo));
    public float MoveSpeed    => GetStat(StatType.MoveSpeed, baseMoveSpeed);
    public float BulletSpeed  => GetStat(StatType.BulletSpeed, baseBulletSpeed);
    public float ReloadSpeed  => GetStat(StatType.ReloadSpeed, baseReloadSpeed);
    
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
    
    public void ResetStats()
    {
        _flatBonuses.Clear();
        _percentBonuses.Clear();
        ActiveEffects = SpecialEffect.None;
        _appliedCardIds.Clear();

        Debug.Log($"[PlayerStats] Stats reset for {gameObject.name}");
    }
    
    public bool HasEffect(SpecialEffect effect)
    {
        return (ActiveEffects & effect) != 0;
    }
    
    private float GetStat(StatType type, float baseValue)
    {
        float flat = 0f;
        float percent = 0f;

        if (_flatBonuses.TryGetValue(type, out float f)) flat = f;
        if (_percentBonuses.TryGetValue(type, out float p)) percent = p;
        
        float result = (baseValue + flat) * (1f + percent);
        return Mathf.Max(result, 0.01f); // never zero or negative
    }

    private void AddBonus(Dictionary<StatType, float> dict, StatType type, float value)
    {
        if (dict.ContainsKey(type))
            dict[type] += value;
        else
            dict[type] = value;
    }
    

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
    
    public void ApplyCardNetworked(CardData card)
    {
        ApplyCard(card);
        photonView.RPC(nameof(RPC_ApplyCard), RpcTarget.Others, card.CardId);
    }
    
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
        CardDatabase db = Resources.Load<CardDatabase>("CardDatabase");
        if (db != null) return db;
        
        var managers = FindObjectsByType<CardSelectionManager>(FindObjectsSortMode.None);
        if (managers.Length > 0)
            return managers[0].cardDatabase;

        return null;
    }
}
