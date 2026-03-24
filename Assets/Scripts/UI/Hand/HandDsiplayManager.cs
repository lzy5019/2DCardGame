/// <summary>
/// 手牌管理器
/// 负责读取手牌数据，生成手牌UI
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandDisplayManager : MonoBehaviour
{
    public PlayerDeckManager playerDeckManager;
    public RectTransform handArea;

    public Vector2 cardSize = new Vector2(252, 352);
    public float cardSpacing = 150f;
    public float maxRotation = 15f;
    public float curveHeight = 5f;

    private List<GameObject> cardObjects = new List<GameObject>();

    public void RefreshHand()       // 刷新手牌显示
    {
        ClearHand();

        List<string> handCards = playerDeckManager.handCards;

        for (int i = 0; i < handCards.Count; i++)
        {
            string cardId = handCards[i];
            CardData cardData = CardDatabase.Instance.GetCardById(cardId);

            if (cardData == null || cardData.cardSprite == null)
                continue;

            GameObject cardObj = new GameObject(
                "HandCard_" + i,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CardPreviewTrigger),
                typeof(HandCardUI)
            );

            cardObj.transform.SetParent(handArea, false);

            RectTransform rect = cardObj.GetComponent<RectTransform>();
            rect.sizeDelta = cardSize;

            Image image = cardObj.GetComponent<Image>();
            image.sprite = cardData.cardSprite;
            image.preserveAspect = true;

            HandCardUI cardUI = cardObj.GetComponent<HandCardUI>();
            cardUI.handDisplayManager = this;
            cardUI.handIndex = i;
            cardUI.cardId = cardId;
            cardUI.Initialized();       // 拿取父物体坐标

            cardObjects.Add(cardObj);
        }

        UpdateHandLayout();
    }

    public void UpdateHandLayout()      // 调整角度和牌间距
    {
        int count = cardObjects.Count;
        if (count == 0)
            return;

        float spacing = cardSpacing - 5 * count;

        HandCardUI hoveringCard = null;

        for (int i = 0; i < count; i++)
        {
            float offset = i - (count - 1) / 2f;

            float x = offset * spacing;
            float y = -Mathf.Abs(offset) * curveHeight;

            float angle = 0f;
            if (count > 1)
            {
                angle = -offset * (maxRotation * 2f / (count - 1));
            }

            HandCardUI cardUI = cardObjects[i].GetComponent<HandCardUI>();
            cardUI.targetPosition = new Vector2(x, y);
            cardUI.targetRotation = angle;

            if (cardUI.isHovering)
            {
                hoveringCard = cardUI;
            }
            else
            {
                cardObjects[i].transform.SetSiblingIndex(i);
            }
        }

        if (hoveringCard != null)
        {
            hoveringCard.transform.SetAsLastSibling();
        }
    }

    public void RearrangeAfterPlay(int handIndex)       // 打出牌后重新分布牌
    {
        GameObject playedCardObj = cardObjects[handIndex];

        cardObjects.RemoveAt(handIndex);
        Destroy(playedCardObj);

        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null)
                continue;

            cardObjects[i].name = "HandCard_" + i;

            HandCardUI cardUI = cardObjects[i].GetComponent<HandCardUI>();
            if (cardUI != null)
            {
                cardUI.handIndex = i;
            }
        }

        UpdateHandLayout();
    }

    public void RearrangeAfterDraw()
    {
        List<string> handCards = playerDeckManager.handCards;

        int newIndex = handCards.Count - 1;
        if (newIndex < 0)
            return;

        string cardId = handCards[newIndex];
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);

        if (cardData == null || cardData.cardSprite == null)
            return;

        GameObject cardObj = new GameObject(
            "HandCard_" + newIndex,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CardPreviewTrigger),
            typeof(HandCardUI)
        );

        cardObj.transform.SetParent(handArea, false);

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        rect.sizeDelta = cardSize;

        Image image = cardObj.GetComponent<Image>();
        image.sprite = cardData.cardSprite;
        image.preserveAspect = true;

        HandCardUI cardUI = cardObj.GetComponent<HandCardUI>();
        cardUI.handDisplayManager = this;
        cardUI.handIndex = newIndex;
        cardUI.cardId = cardId;
        cardUI.Initialized();

        cardObjects.Add(cardObj);

        UpdateHandLayout();
    }

    public void ClearHand()        // 清理所有手牌实例
    {
        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] != null)
            {
                Destroy(cardObjects[i]);
            }
        }

        cardObjects.Clear();
    }
}