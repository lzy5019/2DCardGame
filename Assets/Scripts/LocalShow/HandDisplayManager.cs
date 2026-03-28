/// <summary>
/// 手牌管理器
/// 负责读取手牌数据，生成手牌UI
/// </summary>

using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandDisplayManager : MonoBehaviour
{
    public static HandDisplayManager Instance;

    public PlayerState playerState;
    public RectTransform handArea;

    public Vector2 cardSize = new Vector2(252, 352);
    public float cardSpacing = 150f;
    public float maxRotation = 15f;
    public float curveHeight = 5f;

    private readonly List<GameObject> cardObjects = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("场景中存在多个 HandDisplayManager，已销毁重复对象");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #region 本地登记 & 监听手牌数据变化
    public void RegisterLocalPlayer(PlayerState localPlayerState)
    {
        if (localPlayerState == null)
            return;

        if (playerState == localPlayerState)
            return;

        UnregisterCurrentPlayer();

        playerState = localPlayerState;
        playerState.handCardIds.OnChange += OnHandCardsChanged;

        RefreshHand();
    }
    public void UnregisterCurrentPlayer()
    {
        if (playerState != null)
        {
            playerState.handCardIds.OnChange -= OnHandCardsChanged;
            playerState = null;
        }

        ClearHand();
    }
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterCurrentPlayer();
    }

    private void OnHandCardsChanged(SyncList<string>.Operation op, int index, string item)
    {
        RefreshHand();
    }
    #endregion

    public void RefreshHand()       // 刷新手牌显示
    {
        ClearHand();

        List<string> handCards = new List<string>(playerState.handCardIds);

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

    public void RearrangeAfterPlay(int handIndex)       // 打出牌后重新统筹
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

    public void RearrangeAfterDraw()        // 抽牌后重新统筹
    {
        List<string> handCards = new List<string>(playerState.handCardIds);

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