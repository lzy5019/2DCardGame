using System.Collections.Generic;
using UnityEngine;

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance;

    public List<CardData> allCards = new List<CardData>();

    private Dictionary<string, CardData> cardDict = new Dictionary<string, CardData>();

    private void Awake()
    {
        Instance = this;
        LoadAllCards();
        Debug.Log("牌库读取完毕");
    }

    private void LoadAllCards()     // 读取所有卡牌
    {
        CardData[] loadedCards = Resources.LoadAll<CardData>("Cards");

        allCards.Clear();
        cardDict.Clear();

        foreach (CardData card in loadedCards)
        {
            allCards.Add(card);
            cardDict.Add(card.cardId, card);
        }
    }

    public CardData GetCardById(string id)      // 通过id查询卡牌
    {
        return cardDict[id];
    }

    public List<CardData> GetCardsByType(CardType type) // 通过种类查询卡牌
    {
        List<CardData> result = new List<CardData>();

        foreach (CardData card in allCards)
        {
            if (card.cardType == type)
            {
                result.Add(card);
            }
        }

        return result;
    }

    public List<CardData> GetCardsByCategory(CardCategory category)     // 通过科目查询卡牌
    {
        List<CardData> result = new List<CardData>();

        foreach (CardData card in allCards)
        {
            if (card.cardCategory == category)
            {
                result.Add(card);
            }
        }

        return result;
    }

    public List<CardData> GetCardsByCost(int cost)      // 通过费用查询卡牌
    {
        List<CardData> result = new List<CardData>();

        foreach (CardData card in allCards)
        {
            if (card.cost == cost)
            {
                result.Add(card);
            }
        }

        return result;
    }

    public List<CardData> GetCardsByScore(int score)      // 通过费用查询卡牌
    {
        List<CardData> result = new List<CardData>();

        foreach (CardData card in allCards)
        {
            if (card.scoreValue == score)
            {
                result.Add(card);
            }
        }

        return result;
    }
}