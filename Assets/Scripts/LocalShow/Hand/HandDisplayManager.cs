using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 根据同步后的手牌列表构建手牌界面，并持续更新布局。
/// </summary>
public class HandDisplayManager : MonoBehaviour
{
    public static HandDisplayManager Instance;

    #region 引用
    public PlayerState playerState;
    public RectTransform handArea;
    #endregion

    #region 布局设置
    public Vector2 cardSize = new Vector2(252, 352);
    public float cardSpacing = 150f;
    public float maxRotation = 15f;
    public float curveHeight = 5f;
    #endregion

    #region 运行时状态
    private readonly List<GameObject> cardObjects = new List<GameObject>();
    private bool isWaitingFullRefresh = false;
    #endregion

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple HandDisplayManager instances were found. The duplicate was destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterCurrentPlayer();
    }
    #endregion

    #region 玩家注册
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

    private void OnHandCardsChanged(SyncList<string>.Operation op, int index, string item)
    {
        if (isWaitingFullRefresh)
        {
            return;
        }

        switch (op)
        {
            case SyncList<string>.Operation.OP_ADD:
                RearrangeAfterDraw();
                break;

            case SyncList<string>.Operation.OP_REMOVEAT:
                if (index >= 0 && index < cardObjects.Count)
                {
                    RearrangeAfterPlay(index);
                }
                else
                {
                    RefreshHand();
                }
                break;

            case SyncList<string>.Operation.OP_CLEAR:
                StartCoroutine(RefreshHandNextFrame());
                return;

            case SyncList<string>.Operation.OP_SET:
            case SyncList<string>.Operation.OP_INSERT:
            default:
                RefreshHand();
                break;
        }
    }

    private IEnumerator RefreshHandNextFrame()
    {
        isWaitingFullRefresh = true;
        yield return null;
        RefreshHand();
        isWaitingFullRefresh = false;
    }
    #endregion

    #region 手牌渲染
    public void RefreshHand()
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
            cardUI.Initialized();

            cardObjects.Add(cardObj);
        }

        UpdateHandLayout();
    }

    public void UpdateHandLayout()
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
    #endregion

    #region 增量更新
    public void RearrangeAfterPlay(int handIndex)
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

    public void ClearHand()
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
    #endregion
}
