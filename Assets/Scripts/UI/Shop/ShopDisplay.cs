using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopDisplay : MonoBehaviour
{
    [Header("商店槽位Basic")]
    [SerializeField] private List<GameObject> basicSlots = new List<GameObject>();

    [Header("商店槽位Center")]
    [SerializeField] private List<GameObject> centerSlots = new List<GameObject>();

    [Header("商店卡牌数据")]
    [SerializeField] private ShopDeckManager shopDeckManager;
    [SerializeField] private List<CardData> basicCards = new List<CardData>();
    public List<CardData> currentCenterCards = new List<CardData>();      // 展示可购买的五张卡


    public void Initialized()
    {
        for (int i = 0; i < 5; i++)
        {
            if (i < basicCards.Count)
            {
                ShopSlotUI shopSlotUI = basicSlots[i].GetComponent<ShopSlotUI>();
                Image image = basicSlots[i].GetComponent<Image>();
                basicSlots[i].SetActive(true);
                shopSlotUI.cardData = basicCards[i];
                image.sprite = basicCards[i].cardSprite;
            }
            else 
            {
                basicSlots[i].SetActive(false);
            }
        }

        DrawInitialCenterCards();
        RefreshCenterSlots();
        Debug.Log("**中场商店显示初始化完成");
    }
    private void DrawInitialCenterCards()       // 抽取五张卡
    {
        currentCenterCards.Clear();

        for (int i = 0; i < 5; i++)
        {
            string cardId = shopDeckManager.DrawShopCard();

            CardData card = CardDatabase.Instance.GetCardById(cardId);

            currentCenterCards.Add(card);
        }
    }
    public void RefreshCenterSlots()           // 展示商店卡
    {
        for (int i = 0; i < centerSlots.Count; i++)
        {
            if (i < currentCenterCards.Count && currentCenterCards[i] != null)
            {
                ShopSlotUI shopSlotUI = centerSlots[i].GetComponent<ShopSlotUI>();
                Image image = centerSlots[i].GetComponent<Image>();
                centerSlots[i].SetActive(true);
                shopSlotUI.cardData = currentCenterCards[i];
                image.sprite = currentCenterCards[i].cardSprite;
            }
            else
            {
                centerSlots[i].SetActive(false);
            }
        }
    }
}
