using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class ShopState : NetworkBehaviour
{
    public static ShopState Instance;

    [SerializeField] private List<string> shopDeck = new List<string>();
    [SerializeField] private List<string> discardPile = new List<string>();

    // 商店展示的5张卡
    public readonly SyncList<string> centerCardIds = new SyncList<string>();

    public List<string> baseCardIds = new List<string>()
    {
        "00003",
        "00004",
        "00005",
        "00000",
        ""
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        BuildDeck();
        ShuffleDeck();
        InitCenterSlots();
        FillCenterCards();
    }

    [Server]
    private void BuildDeck()    // 构建中场牌堆
    {
        shopDeck.Clear();

        foreach (CardData card in CardDatabase.Instance.allCards)
        {
            if (card == null) continue;
            if (card.cardNum <= 0) continue;

            for (int i = 0; i < card.cardNum; i++)
            {
                shopDeck.Add(card.cardId);
            }
        }
    }
    [Server]
    private void ShuffleDeck()  // 中场洗牌
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
    }
    [Server]
    private void InitCenterSlots()  // 初始化中场5个槽位
    {
        centerCardIds.Clear();

        for (int i = 0; i < 5; i++)
        {
            centerCardIds.Add("");
        }
    }
    [Server]
    private void FillCenterCards()  // 填充中场卡牌
    {
        for (int i = 0; i < centerCardIds.Count; i++)
        {
            if (!string.IsNullOrEmpty(centerCardIds[i]))
                continue;

            string newCardId = DrawCard();

            if (string.IsNullOrEmpty(newCardId))
            {
                Debug.Log("没有可补充的中场牌了");
                return;
            }

            centerCardIds[i] = newCardId;
        }
    }
    [Server]
    private string DrawCard()   // 抽卡
    {
        if (shopDeck.Count == 0)
        {
            if (discardPile.Count > 0)
            {
                ShuffleDeck();
            }
            else
            {
                Debug.Log("中场牌堆为空，且弃牌堆也为空");
                return "";
            }
        }

        string cardId = shopDeck[0];
        shopDeck.RemoveAt(0);
        return cardId;
    }
    [Server]
    public void RemoveCenterCard(int slotIndex) // 购买牌
    {
        centerCardIds[slotIndex] = "";
        FillCenterCards();
    }
}
