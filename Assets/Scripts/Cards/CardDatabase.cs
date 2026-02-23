using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central database of all available cards.
/// Create via Assets → Create → WildCard → Card Database.
/// Assign all CardData assets to the cards list.
/// </summary>
[CreateAssetMenu(fileName = "CardDatabase", menuName = "WildCard/Card Database")]
public class CardDatabase : ScriptableObject
{
    [Header("All Cards")]
    public List<CardData> cards = new List<CardData>();

    [Header("Rarity Weights (selection probability)")]
    [Tooltip("Relative weight for Common cards")]
    public float commonWeight = 70f;
    [Tooltip("Relative weight for Rare cards")]
    public float rareWeight = 25f;
    [Tooltip("Relative weight for Legendary cards")]
    public float legendaryWeight = 5f;

    private Dictionary<string, CardData> _lookup;

    /// <summary>
    /// Build the lookup dictionary on first access.
    /// </summary>
    private void BuildLookup()
    {
        _lookup = new Dictionary<string, CardData>();
        foreach (var card in cards)
        {
            if (card != null && !_lookup.ContainsKey(card.CardId))
            {
                _lookup[card.CardId] = card;
            }
        }
    }

    /// <summary>
    /// Find a card by its unique ID (asset name).
    /// Used for network sync — receive card ID, resolve to CardData.
    /// </summary>
    public CardData GetCardById(string cardId)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(cardId, out CardData card);
        return card;
    }

    /// <summary>
    /// Select random cards with rarity-weighted probability.
    /// No duplicates in the returned set.
    /// </summary>
    public List<CardData> GetRandomCards(int count)
    {
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning("CardDatabase: No cards available!");
            return new List<CardData>();
        }

        // Build weighted list
        List<(CardData card, float weight)> weighted = new List<(CardData, float)>();
        foreach (var card in cards)
        {
            if (card == null) continue;
            float weight = GetWeightForRarity(card.rarity);
            weighted.Add((card, weight));
        }

        // Weighted random selection without duplicates
        List<CardData> selected = new List<CardData>();
        List<(CardData card, float weight)> pool = new List<(CardData, float)>(weighted);

        int toSelect = Mathf.Min(count, pool.Count);

        for (int i = 0; i < toSelect; i++)
        {
            float totalWeight = 0f;
            foreach (var entry in pool) totalWeight += entry.weight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int j = 0; j < pool.Count; j++)
            {
                cumulative += pool[j].weight;
                if (roll <= cumulative)
                {
                    selected.Add(pool[j].card);
                    pool.RemoveAt(j);
                    break;
                }
            }
        }

        return selected;
    }

    private float GetWeightForRarity(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:    return commonWeight;
            case CardRarity.Rare:      return rareWeight;
            case CardRarity.Legendary: return legendaryWeight;
            default:                   return commonWeight;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only test: logs 3 random cards to the console.
    /// </summary>
    [ContextMenu("Test: Draw 3 Random Cards")]
    private void TestRandomDraw()
    {
        var drawn = GetRandomCards(3);
        foreach (var card in drawn)
        {
            Debug.Log($"[CardDatabase Test] Drew: {card.cardName} ({card.rarity})");
        }
    }
#endif
}
