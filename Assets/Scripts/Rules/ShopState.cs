using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ShopState : NetworkBehaviour
{
    public static ShopState Instance;

    #region 卡组数据
    [SerializeField] private List<string> shopDeck = new List<string>();
    [SerializeField] private List<string> discardPile = new List<string>();

    // 这五个条目表示中央商店当前可见的五个槽位。
    public readonly SyncList<string> centerCardIds = new SyncList<string>();

    public List<string> baseCardIds = new List<string>()
    {
        "00003",
        "00004",
        "00005",
        "00000",
        ""
    };
    #endregion

    #region 生命周期
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
    #endregion

    #region 构建卡组
    [Server]
    private void BuildDeck()
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
    private void ShuffleDeck()
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
    private void InitCenterSlots()
    {
        centerCardIds.Clear();

        for (int i = 0; i < 5; i++)
        {
            centerCardIds.Add("");
        }
    }
    #endregion

    #region 抽取与刷新
    [Server]
    private void FillCenterCards()
    {
        for (int i = 0; i < centerCardIds.Count; i++)
        {
            if (!string.IsNullOrEmpty(centerCardIds[i]))
                continue;

            string newCardId = DrawCard();

            if (string.IsNullOrEmpty(newCardId))
            {
                Debug.Log("No more cards are available for the center shop.");
                return;
            }

            centerCardIds[i] = newCardId;
        }
    }

    [Server]
    private string DrawCard()
    {
        if (shopDeck.Count == 0)
        {
            if (discardPile.Count > 0)
            {
                ShuffleDeck();
            }
            else
            {
                Debug.Log("Shop deck and discard pile are both empty.");
                return "";
            }
        }

        string cardId = shopDeck[0];
        shopDeck.RemoveAt(0);
        return cardId;
    }
    #endregion

    #region 槽位更新
    [Server]
    public void RemoveCenterCard(int slotIndex)
    {
        centerCardIds[slotIndex] = "";
        FillCenterCards();
    }

    [Server]
    public void DiscardCenterCard(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= centerCardIds.Count)
            return;

        string cardId = centerCardIds[slotIndex];
        if (string.IsNullOrEmpty(cardId))
            return;

        discardPile.Add(cardId);
        centerCardIds[slotIndex] = "";
        FillCenterCards();
    }
    #endregion
}

