/// <summary>
/// 管理中场牌组
/// 包括中场抽牌堆弃牌堆，以及补货函数
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public class ShopDeckManager : MonoBehaviour
{
    public static ShopDeckManager Instance;

    public ShopDisplay shopDisplay;

    [Header("中场牌堆")]
    public List<string> shopDeck = new List<string>();      // 当前中场抽牌堆
    public List<string> discardPile = new List<string>();   // 中场弃牌堆

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    public void Initialized()
    {
        BuildShopDeck();
        ShuffleShopDeck();
        Debug.Log("**中场牌堆初始化完成");
    }

    public void BuildShopDeck()     // 读取卡牌数据，生成中场牌堆
    {
        shopDeck.Clear();
        discardPile.Clear();

        List<CardData> allCards = CardDatabase.Instance.allCards;

        foreach (CardData card in allCards)
        {
            if (card == null)
                continue;

            if (card.cardNum <= 0)
                continue;

            for (int i = 0; i < card.cardNum; i++)
            {
                shopDeck.Add(card.cardId);
            }
        }

        Debug.Log("中场牌堆构建完成，数量：" + shopDeck.Count);
    }

    public void ShuffleShopDeck()       // 中场洗牌
    {
        for (int i = 0; i < discardPile.Count; i++)
        {
            shopDeck.Add(discardPile[i]);
        }

        discardPile.Clear();

        for (int i = 0; i < shopDeck.Count; i++)
        {
            int randomIndex = Random.Range(i, shopDeck.Count);

            string temp = shopDeck[i];
            shopDeck[i] = shopDeck[randomIndex];
            shopDeck[randomIndex] = temp;
        }

        Debug.Log("中场牌堆洗牌完成");
    }

    public string DrawShopCard()        // 抽中场牌堆
    {
        if (shopDeck.Count == 0)
        {
            if (discardPile.Count > 0)
            {
                ShuffleShopDeck();
            }
            else
            {
                Debug.Log("中场牌堆为空，且弃牌堆也为空");
                return null;
            }
        }

        string cardId = shopDeck[0];
        shopDeck.RemoveAt(0);
        return cardId;
    }

    public void RefillCard(int slotIndex)      // 补货
    {
        if(slotIndex < 5)
        {
            string newCardId = DrawShopCard();
            if (string.IsNullOrEmpty(newCardId))
            {
                Debug.Log("补货失败：中场牌堆已经没有牌了");
            }
            else 
            {
                CardData card = CardDatabase.Instance.GetCardById(newCardId);
                shopDisplay.currentCenterCards[slotIndex] = card;
                shopDisplay.RefreshCenterSlots();
            }
        }
    }
}