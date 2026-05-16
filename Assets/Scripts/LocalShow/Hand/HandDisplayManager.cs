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
    private readonly List<PendingIncomingVisualEvent> pendingIncomingVisualEvents = new List<PendingIncomingVisualEvent>();
    private readonly List<PendingDrawCardView> pendingDrawCardViews = new List<PendingDrawCardView>();
    private readonly List<PendingIncomingHandExileEvent> pendingIncomingHandExileEvents = new List<PendingIncomingHandExileEvent>();
    private readonly List<PendingRemovedHandView> pendingRemovedHandViews = new List<PendingRemovedHandView>();
    private readonly List<PendingLocalPlayRequest> pendingLocalPlayRequests = new List<PendingLocalPlayRequest>();
    #endregion

    private const float PendingDrawFxMatchTimeout = 12f;
    private const float PendingHandExileMatchTimeout = 0.5f;
    private const float PendingLocalPlayRequestTimeout = 1.25f;
    private int nextLocalPlayFxRequestId = 1;

    private sealed class PendingIncomingVisualEvent
    {
        public HandCardDrawFxRequestType requestType;
        public string cardId;
        public HandCardDrawFxMode mode;
        public HandCardPileToHandFxSourceType sourceType;
        public int movedCardCount;
        public float expireTime;
    }

    private sealed class PendingDrawCardView
    {
        public GameObject cardObject;
        public string cardId;
        public float expireTime;
    }

    private sealed class PendingIncomingHandExileEvent
    {
        public int handIndex;
        public string cardId;
        public float expireTime;
    }

    private sealed class PendingRemovedHandView
    {
        public int handIndex;
        public string cardId;
        public GameObject cardObject;
        public float expireTime;
    }

    private sealed class PendingLocalPlayRequest
    {
        public int requestId;
        public int handIndex;
        public string cardId;
        public Vector2 releaseScreenPosition;
        public float expireTime;
    }

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

    private void Update()
    {
        if (pendingIncomingVisualEvents.Count > 0 ||
            pendingDrawCardViews.Count > 0 ||
            pendingIncomingHandExileEvents.Count > 0 ||
            pendingRemovedHandViews.Count > 0 ||
            pendingLocalPlayRequests.Count > 0)
        {
            CleanupPendingDrawFxState();
            TryProcessPendingHandExileFx();
        }
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

        pendingIncomingVisualEvents.Clear();
        pendingDrawCardViews.Clear();
        pendingIncomingHandExileEvents.Clear();
        pendingLocalPlayRequests.Clear();

        for (int i = 0; i < pendingRemovedHandViews.Count; i++)
        {
            PendingRemovedHandView removedView = pendingRemovedHandViews[i];
            if (removedView != null && removedView.cardObject != null)
            {
                Destroy(removedView.cardObject);
            }
        }

        pendingRemovedHandViews.Clear();
        ClearHand();
    }

    private void OnHandCardsChanged(SyncList<string>.Operation op, int index, string item)
    {
        switch (op)
        {
            case SyncList<string>.Operation.OP_ADD:
                RearrangeAfterHandAdd();
                break;

            case SyncList<string>.Operation.OP_REMOVEAT:
                if (index >= 0 && index < cardObjects.Count)
                {
                    HandleCardRemoved(index, item);
                }
                else
                {
                    RefreshHand();
                }
                break;

            case SyncList<string>.Operation.OP_CLEAR:
                ClearHand();
                return;

            case SyncList<string>.Operation.OP_SET:
                if (index >= 0 && index < cardObjects.Count)
                {
                    string newCardId = item;
                    if (playerState != null && index >= 0 && index < playerState.handCardIds.Count)
                    {
                        newCardId = playerState.handCardIds[index];
                    }

                    RearrangeAfterTransform(index, newCardId);
                }
                else
                {
                    RefreshHand();
                }
                break;

            case SyncList<string>.Operation.OP_INSERT:
            default:
                RefreshHand();
                break;
        }
    }
    #endregion

    #region 手牌渲染
    public void NotifyIncomingDrawFx(string cardId, HandCardDrawFxMode mode)
    {
        CleanupPendingDrawFxState();

        pendingIncomingVisualEvents.Add(new PendingIncomingVisualEvent
        {
            requestType = HandCardDrawFxRequestType.DrawCard,
            cardId = cardId,
            mode = mode,
            expireTime = Time.unscaledTime + PendingDrawFxMatchTimeout
        });

        TryProcessPendingVisualEvents();
    }

    public void NotifyIncomingPileToHandFx(string cardId, HandCardPileToHandFxSourceType sourceType)
    {
        CleanupPendingDrawFxState();

        pendingIncomingVisualEvents.Add(new PendingIncomingVisualEvent
        {
            requestType = HandCardDrawFxRequestType.PileToHand,
            cardId = cardId,
            sourceType = sourceType,
            expireTime = Time.unscaledTime + PendingDrawFxMatchTimeout
        });

        TryProcessPendingVisualEvents();
    }

    public void NotifyIncomingReshuffleFx(int movedCardCount)
    {
        if (movedCardCount <= 0)
            return;

        CleanupPendingDrawFxState();

        pendingIncomingVisualEvents.Add(new PendingIncomingVisualEvent
        {
            requestType = HandCardDrawFxRequestType.Reshuffle,
            movedCardCount = movedCardCount,
            expireTime = Time.unscaledTime + PendingDrawFxMatchTimeout
        });

        TryProcessPendingVisualEvents();
    }

    public bool IsIncomingDrawFxBusy()
    {
        CleanupPendingDrawFxState();
        return pendingIncomingVisualEvents.Count > 0 ||
               pendingDrawCardViews.Count > 0 ||
               pendingIncomingHandExileEvents.Count > 0 ||
               pendingRemovedHandViews.Count > 0 ||
               pendingLocalPlayRequests.Count > 0 ||
               HandCardDrawFxUI.IsBusy ||
               HandCardPileToHandFxUI.IsBusy ||
               HandCardExileFxUI.IsBusy ||
               HandCardPlayFxUI.IsBusy;
    }

    public void NotifyIncomingHandExileFx(int handIndex, string cardId)
    {
        CleanupPendingDrawFxState();

        pendingIncomingHandExileEvents.Add(new PendingIncomingHandExileEvent
        {
            handIndex = handIndex,
            cardId = cardId,
            expireTime = Time.unscaledTime + PendingHandExileMatchTimeout
        });

        TryProcessPendingHandExileFx();
    }

    public int BeginLocalPlayCardFx(string cardId, int handIndex, Vector2 releaseScreenPosition)
    {
        if (!ShouldUsePlayCardFx(cardId))
            return -1;

        CleanupPendingDrawFxState();

        int requestId = nextLocalPlayFxRequestId++;
        if (nextLocalPlayFxRequestId <= 0)
        {
            nextLocalPlayFxRequestId = 1;
        }

        pendingLocalPlayRequests.Add(new PendingLocalPlayRequest
        {
            requestId = requestId,
            handIndex = handIndex,
            cardId = cardId,
            releaseScreenPosition = releaseScreenPosition,
            expireTime = Time.unscaledTime + PendingLocalPlayRequestTimeout
        });

        return requestId;
    }

    public void NotifyPlayedCardResolveFx(int requestId, string cardId, PlayedCardResolveDestinationType destinationType)
    {
        if (requestId <= 0)
            return;

        HandCardPlayFxUI.NotifyResolved(requestId, cardId, destinationType);
    }

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

    public void RearrangeAfterHandAdd()
    {
        CleanupPendingDrawFxState();

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
        TrackPendingDrawCardView(cardObj, cardId);
        TryProcessPendingVisualEvents();
    }

    public void RearrangeAfterTransform(int handIndex, string newCardId)
    {
        if (handIndex < 0 || handIndex >= cardObjects.Count)
        {
            RefreshHand();
            return;
        }
        if (string.IsNullOrEmpty(newCardId) || CardDatabase.Instance == null)
        {
            RefreshHand();
            return;
        }

        GameObject cardObj = cardObjects[handIndex];
        if (cardObj == null)
        {
            RefreshHand();
            return;
        }

        HandCardUI cardUI = cardObj.GetComponent<HandCardUI>();
        string oldCardId = cardUI != null ? cardUI.cardId : "";

        PlayTransformFxByIndex(handIndex, oldCardId, newCardId);
    }

    public void PlayTransformFxByIndex(int handIndex, string oldCardId, string newCardId)
    {
        if (handIndex < 0 || handIndex >= cardObjects.Count)
        {
            RefreshHand();
            return;
        }
        if (string.IsNullOrEmpty(newCardId) || CardDatabase.Instance == null)
        {
            RefreshHand();
            return;
        }

        GameObject cardObj = cardObjects[handIndex];
        if (cardObj == null)
        {
            RefreshHand();
            return;
        }

        bool startedFx = HandCardTransformFxUI.TryPlay(
            cardObj,
            oldCardId,
            newCardId,
            () => ApplyCardVisual(cardObj, handIndex, newCardId)
        );

        if (!startedFx)
        {
            ApplyCardVisual(cardObj, handIndex, newCardId);
        }

        UpdateHandLayout();
    }

    private void HandleCardRemoved(int handIndex, string removedCardId)
    {
        GameObject removedCardObj = cardObjects[handIndex];
        string resolvedCardId = removedCardId;

        HandCardUI removedCardUI = removedCardObj != null ? removedCardObj.GetComponent<HandCardUI>() : null;
        if (string.IsNullOrEmpty(resolvedCardId) && removedCardUI != null)
        {
            resolvedCardId = removedCardUI.cardId;
        }

        cardObjects.RemoveAt(handIndex);

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

        if (removedCardObj != null)
        {
            if (TryConsumePendingLocalPlayRequest(handIndex, resolvedCardId, out PendingLocalPlayRequest playRequest))
            {
                StartQueuedPlayCardFx(playRequest.requestId, removedCardObj, resolvedCardId, playRequest.releaseScreenPosition);
                UpdateHandLayout();
                return;
            }

            pendingRemovedHandViews.Add(new PendingRemovedHandView
            {
                handIndex = handIndex,
                cardId = resolvedCardId,
                cardObject = removedCardObj,
                expireTime = Time.unscaledTime + PendingHandExileMatchTimeout
            });

            ConcealCardImmediately(removedCardObj);
        }

        UpdateHandLayout();
        TryProcessPendingHandExileFx();
    }

    public void ClearHand()
    {
        pendingDrawCardViews.Clear();
        pendingIncomingHandExileEvents.Clear();

        for (int i = 0; i < pendingRemovedHandViews.Count; i++)
        {
            PendingRemovedHandView removedView = pendingRemovedHandViews[i];
            if (removedView != null && removedView.cardObject != null)
            {
                Destroy(removedView.cardObject);
            }
        }

        pendingRemovedHandViews.Clear();

        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] != null)
            {
                Destroy(cardObjects[i]);
            }
        }

        cardObjects.Clear();
    }

    private void CleanupPendingDrawFxState()
    {
        float now = Time.unscaledTime;

        for (int i = pendingDrawCardViews.Count - 1; i >= 0; i--)
        {
            PendingDrawCardView view = pendingDrawCardViews[i];
            if (view == null || view.cardObject == null)
            {
                pendingDrawCardViews.RemoveAt(i);
                continue;
            }

            if (view.expireTime <= now)
            {
                pendingDrawCardViews.RemoveAt(i);
            }
        }

        for (int i = pendingIncomingVisualEvents.Count - 1; i >= 0; i--)
        {
            PendingIncomingVisualEvent visualEvent = pendingIncomingVisualEvents[i];
            if (visualEvent == null || visualEvent.expireTime <= now)
            {
                ResolveExpiredPendingVisualEvent(visualEvent);
                pendingIncomingVisualEvents.RemoveAt(i);
            }
        }

        for (int i = pendingIncomingHandExileEvents.Count - 1; i >= 0; i--)
        {
            PendingIncomingHandExileEvent exileEvent = pendingIncomingHandExileEvents[i];
            if (exileEvent == null || exileEvent.expireTime <= now)
            {
                pendingIncomingHandExileEvents.RemoveAt(i);
            }
        }

        for (int i = pendingLocalPlayRequests.Count - 1; i >= 0; i--)
        {
            PendingLocalPlayRequest playRequest = pendingLocalPlayRequests[i];
            if (playRequest == null || playRequest.expireTime <= now)
            {
                pendingLocalPlayRequests.RemoveAt(i);
            }
        }

        for (int i = pendingRemovedHandViews.Count - 1; i >= 0; i--)
        {
            PendingRemovedHandView removedView = pendingRemovedHandViews[i];
            if (removedView == null || removedView.cardObject == null)
            {
                pendingRemovedHandViews.RemoveAt(i);
                continue;
            }

            if (removedView.expireTime <= now)
            {
                Destroy(removedView.cardObject);
                pendingRemovedHandViews.RemoveAt(i);
            }
        }
    }

    private void ResolveExpiredPendingVisualEvent(PendingIncomingVisualEvent visualEvent)
    {
        if (playerState == null || visualEvent == null)
            return;

        if (visualEvent.requestType == HandCardDrawFxRequestType.Reshuffle)
        {
            playerState.NotifyLocalReshuffleVisualResolved(visualEvent.movedCardCount);
            return;
        }

        if (visualEvent.requestType == HandCardDrawFxRequestType.PileToHand)
            return;

        playerState.NotifyLocalDrawVisualStarted(visualEvent.cardId, visualEvent.mode);
        playerState.NotifyLocalDrawVisualResolved(visualEvent.cardId, visualEvent.mode);
    }

    private void TrackPendingDrawCardView(GameObject cardObj, string cardId)
    {
        if (cardObj == null)
            return;

        pendingDrawCardViews.Add(new PendingDrawCardView
        {
            cardObject = cardObj,
            cardId = cardId,
            expireTime = Time.unscaledTime + PendingDrawFxMatchTimeout
        });
    }

    private void TryProcessPendingHandExileFx()
    {
        while (pendingIncomingHandExileEvents.Count > 0)
        {
            PendingIncomingHandExileEvent exileEvent = pendingIncomingHandExileEvents[0];
            if (exileEvent == null)
            {
                pendingIncomingHandExileEvents.RemoveAt(0);
                continue;
            }

            if (!TryConsumePendingRemovedHandView(exileEvent.handIndex, exileEvent.cardId, out PendingRemovedHandView removedView))
                return;

            pendingIncomingHandExileEvents.RemoveAt(0);
            StartQueuedHandExileFx(removedView.cardObject, string.IsNullOrEmpty(exileEvent.cardId) ? removedView.cardId : exileEvent.cardId);
        }
    }

    private bool TryConsumePendingRemovedHandView(int handIndex, string cardId, out PendingRemovedHandView removedView)
    {
        removedView = null;

        for (int i = 0; i < pendingRemovedHandViews.Count; i++)
        {
            PendingRemovedHandView candidate = pendingRemovedHandViews[i];
            if (candidate == null || candidate.cardObject == null)
            {
                pendingRemovedHandViews.RemoveAt(i);
                i--;
                continue;
            }

            bool handIndexMatches = candidate.handIndex == handIndex;
            bool cardIdMatches = string.IsNullOrEmpty(cardId) || candidate.cardId == cardId;
            if (!handIndexMatches || !cardIdMatches)
                continue;

            removedView = candidate;
            pendingRemovedHandViews.RemoveAt(i);
            return true;
        }

        for (int i = 0; i < pendingRemovedHandViews.Count; i++)
        {
            PendingRemovedHandView candidate = pendingRemovedHandViews[i];
            if (candidate == null || candidate.cardObject == null)
            {
                pendingRemovedHandViews.RemoveAt(i);
                i--;
                continue;
            }

            if (!string.IsNullOrEmpty(cardId) && candidate.cardId != cardId)
                continue;

            removedView = candidate;
            pendingRemovedHandViews.RemoveAt(i);
            return true;
        }

        return false;
    }

    private bool TryConsumePendingLocalPlayRequest(int handIndex, string cardId, out PendingLocalPlayRequest playRequest)
    {
        playRequest = null;

        for (int i = 0; i < pendingLocalPlayRequests.Count; i++)
        {
            PendingLocalPlayRequest candidate = pendingLocalPlayRequests[i];
            if (candidate == null)
            {
                pendingLocalPlayRequests.RemoveAt(i);
                i--;
                continue;
            }

            bool handIndexMatches = candidate.handIndex == handIndex;
            bool cardIdMatches = string.IsNullOrEmpty(cardId) || candidate.cardId == cardId;
            if (!handIndexMatches || !cardIdMatches)
                continue;

            playRequest = candidate;
            pendingLocalPlayRequests.RemoveAt(i);
            return true;
        }

        return false;
    }

    private void TryProcessPendingVisualEvents()
    {
        while (pendingIncomingVisualEvents.Count > 0)
        {
            PendingIncomingVisualEvent visualEvent = pendingIncomingVisualEvents[0];
            if (visualEvent == null)
            {
                pendingIncomingVisualEvents.RemoveAt(0);
                continue;
            }

            if (visualEvent.requestType == HandCardDrawFxRequestType.Reshuffle)
            {
                pendingIncomingVisualEvents.RemoveAt(0);
                StartQueuedReshuffleFx(visualEvent.movedCardCount);
                continue;
            }

            PendingDrawCardView view = ConsumePendingDrawCardView(visualEvent.cardId);
            if (view == null)
                return;

            pendingIncomingVisualEvents.RemoveAt(0);
            HandCardUI handCardUI = view.cardObject != null ? view.cardObject.GetComponent<HandCardUI>() : null;
            string resolvedCardId = handCardUI != null ? handCardUI.cardId : visualEvent.cardId;
            if (visualEvent.requestType == HandCardDrawFxRequestType.PileToHand)
            {
                StartQueuedPileToHandFx(view.cardObject, resolvedCardId, visualEvent.sourceType);
            }
            else
            {
                StartQueuedDrawFx(view.cardObject, resolvedCardId, visualEvent.mode);
            }
        }
    }

    private PendingDrawCardView ConsumePendingDrawCardView(string expectedCardId)
    {
        if (pendingDrawCardViews.Count <= 0)
            return null;

        if (!string.IsNullOrEmpty(expectedCardId))
        {
            for (int i = 0; i < pendingDrawCardViews.Count; i++)
            {
                PendingDrawCardView matchingView = pendingDrawCardViews[i];
                if (matchingView == null || matchingView.cardObject == null)
                {
                    pendingDrawCardViews.RemoveAt(i);
                    i--;
                    continue;
                }

                if (matchingView.cardId != expectedCardId)
                    continue;

                pendingDrawCardViews.RemoveAt(i);
                return matchingView;
            }
        }

        for (int i = 0; i < pendingDrawCardViews.Count; i++)
        {
            PendingDrawCardView view = pendingDrawCardViews[i];
            if (view == null || view.cardObject == null)
            {
                pendingDrawCardViews.RemoveAt(i);
                i--;
                continue;
            }

            pendingDrawCardViews.RemoveAt(i);
            return view;
        }

        return null;
    }

    private void StartQueuedReshuffleFx(int movedCardCount)
    {
        if (movedCardCount <= 0)
            return;

        bool queued = HandCardDrawFxUI.TryQueueReshuffle(
            movedCardCount,
            null,
            () =>
            {
                if (playerState != null)
                {
                    playerState.NotifyLocalReshuffleVisualResolved(movedCardCount);
                }
            });

        if (queued)
            return;

        if (playerState != null)
        {
            playerState.NotifyLocalReshuffleVisualResolved(movedCardCount);
        }
    }

    private bool StartQueuedDrawFx(GameObject cardObj, string cardId, HandCardDrawFxMode mode)
    {
        if (cardObj == null)
            return false;
        if (string.IsNullOrEmpty(cardId))
            return false;

        if (HandCardDrawFxUI.TryQueue(
            cardObj,
            cardId,
            mode,
            () =>
            {
                if (playerState != null)
                {
                    playerState.NotifyLocalDrawVisualStarted(cardId, mode);
                }
            },
            () =>
            {
                if (playerState != null)
                {
                    playerState.NotifyLocalDrawVisualResolved(cardId, mode);
                }
            }))
            return true;

        if (playerState != null)
        {
            playerState.NotifyLocalDrawVisualStarted(cardId, mode);
        }

        if (mode == HandCardDrawFxMode.ToHand)
        {
            RevealCardImmediately(cardObj);
        }
        else
        {
            ConcealCardImmediately(cardObj);
        }

        if (playerState != null)
        {
            playerState.NotifyLocalDrawVisualResolved(cardId, mode);
        }

        return false;
    }

    private bool StartQueuedHandExileFx(GameObject cardObj, string cardId)
    {
        if (cardObj == null)
            return false;

        if (string.IsNullOrEmpty(cardId))
        {
            Destroy(cardObj);
            return false;
        }

        bool queued = HandCardExileFxUI.TryQueueFromHand(cardObj, cardId, null, () =>
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        });

        if (!queued && cardObj != null)
        {
            Destroy(cardObj);
        }

        return queued;
    }

    private bool StartQueuedPileToHandFx(GameObject cardObj, string cardId, HandCardPileToHandFxSourceType sourceType)
    {
        if (cardObj == null)
            return false;
        if (string.IsNullOrEmpty(cardId))
            return false;

        if (HandCardPileToHandFxUI.TryQueue(cardObj, cardId, sourceType))
            return true;

        RevealCardImmediately(cardObj);
        return false;
    }

    private bool StartQueuedPlayCardFx(int requestId, GameObject cardObj, string cardId, Vector2 releaseScreenPosition)
    {
        if (cardObj == null)
            return false;
        if (string.IsNullOrEmpty(cardId))
        {
            Destroy(cardObj);
            return false;
        }

        bool started = HandCardPlayFxUI.TryBegin(requestId, cardObj, cardId, releaseScreenPosition);
        if (!started && cardObj != null)
        {
            Destroy(cardObj);
        }

        return started;
    }

    private void ConcealCardImmediately(GameObject cardObj)
    {
        if (cardObj == null)
            return;

        CanvasGroup canvasGroup = cardObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = cardObj.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        CardPreviewTrigger previewTrigger = cardObj.GetComponent<CardPreviewTrigger>();
        if (previewTrigger != null)
        {
            previewTrigger.enabled = false;
        }
    }

    private void RevealCardImmediately(GameObject cardObj, CanvasGroup preferredCanvasGroup = null, bool previewEnabled = true)
    {
        if (cardObj == null)
            return;

        CanvasGroup canvasGroup = preferredCanvasGroup != null ? preferredCanvasGroup : cardObj.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        CardPreviewTrigger previewTrigger = cardObj.GetComponent<CardPreviewTrigger>();
        if (previewTrigger != null)
        {
            previewTrigger.enabled = previewEnabled;
        }
    }

    private void ApplyCardVisual(GameObject cardObj, int handIndex, string cardId)
    {
        if (cardObj == null || CardDatabase.Instance == null)
            return;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null || cardData.cardSprite == null)
            return;

        cardObj.name = "HandCard_" + handIndex;

        Image image = cardObj.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = cardData.cardSprite;
            image.preserveAspect = true;
        }

        HandCardUI cardUI = cardObj.GetComponent<HandCardUI>();
        if (cardUI != null)
        {
            cardUI.handIndex = handIndex;
            cardUI.cardId = cardId;
        }
    }

    private bool ShouldUsePlayCardFx(string cardId)
    {
        if (string.IsNullOrEmpty(cardId) || CardDatabase.Instance == null)
            return false;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
            return false;

        return cardData.cardType != CardType.Support && cardData.cardType != CardType.Weapon;
    }
    #endregion
}
