using System.Collections.Generic;
using UnityEngine;

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance;

    #region 数据
    public List<CardData> allCards = new List<CardData>();

    private readonly Dictionary<string, CardData> cardDict = new Dictionary<string, CardData>();
    #endregion

    #region 生命周期
    private void Awake()
    {
        Instance = this;
        LoadAllCards();
        Debug.Log("Card database loaded.");
    }
    #endregion

    #region 加载
    private void LoadAllCards()
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
    #endregion

    #region 查询
    public CardData GetCardById(string id)
    {
        return cardDict[id];
    }

    public List<CardData> GetCardsByType(CardType type)
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

    public List<CardData> GetCardsByCategory(CardCategory category)
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

    public List<CardData> GetCardsByCost(int cost)
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

    public List<CardData> GetCardsByScore(int score)
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
    #endregion
}

