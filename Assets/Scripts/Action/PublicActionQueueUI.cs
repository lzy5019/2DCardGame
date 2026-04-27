using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum PublicActionType
{
    PlayCard,
    DefeatCenterMonster,
    BuyCenterCard,
    DefeatBaseMonster,
    BuyBaseCard,
    EquipCard,
    EquipWeapon,
    UseEquipment,
    UseWeapon
}

public class PublicActionQueueUI : MonoBehaviour
{
    public static PublicActionQueueUI Instance;

    #region 演出配置
    [Header("调试演出时长")]
    [SerializeField] private float playCardPresentationDuration = 1f;   // 卡牌播出时间
    [SerializeField] private float presentationGapDuration = 0.3f;  // 两个演出之间的等待时间
    [SerializeField] private float legacyActionDuration = 0.9f;
    [SerializeField] private float removeCardsDuration = 1.2f;      // 移除卡牌时间
    [SerializeField] private float gainCardsDuration = 1.0f;        // 获得卡牌时间
    [SerializeField] private float transformCardsDuration = 1.1f;   // 变化卡牌
    [SerializeField] private float moveCardsDuration = 1.0f;        // 移动卡牌
    [SerializeField] private float textOnlyDuration = 0.8f;         // 纯文本提示类

    [Header("本地演出界面")]
    [SerializeField] private PresentationPanelUI presentationPanelUI;
    #endregion

    #region 队列状态
    private readonly Queue<PresentationEvent> presentationQueue = new Queue<PresentationEvent>();
    private bool isPlaying;
    private Coroutine waitForIdleCoroutine;

    public bool IsBusy
    {
        get { return isPlaying || presentationQueue.Count > 0; }
    }
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region 队列播放
    public void Enqueue(PresentationEvent presentationEvent)
    {
        presentationQueue.Enqueue(presentationEvent);

        if (!isPlaying)
        {
            StartCoroutine(PlayQueueRoutine());
        }
    }

    private IEnumerator PlayQueueRoutine()
    {
        isPlaying = true;

        while (presentationQueue.Count > 0)
        {
            PresentationEvent presentationEvent = presentationQueue.Dequeue();
            yield return PlayPresentationRoutine(presentationEvent);

            if (presentationQueue.Count > 0)
            {
                yield return new WaitForSeconds(presentationGapDuration);
            }
        }

        isPlaying = false;
    }

    // 所有演出都从这里统一分发，后续接入真动画时只需要替换对应协程。
    private IEnumerator PlayPresentationRoutine(PresentationEvent presentationEvent)
    {
        switch (presentationEvent.presentationType)
        {
            case PresentationType.LegacyAction:
                yield return PlayLegacyActionRoutine(presentationEvent);
                break;

            case PresentationType.RemoveCards:
                yield return PlayRemoveCardsRoutine(presentationEvent);
                break;

            case PresentationType.GainCards:
                yield return PlayGainCardsRoutine(presentationEvent);
                break;

            case PresentationType.TransformCards:
                yield return PlayTransformCardsRoutine(presentationEvent);
                break;

            case PresentationType.MoveCards:
                yield return PlayMoveCardsRoutine(presentationEvent);
                break;

            case PresentationType.TextOnly:
                yield return PlayTextOnlyRoutine(presentationEvent);
                break;

            default:
                Debug.LogWarning($"Unknown presentation type: {presentationEvent.presentationType}");
                break;
        }
    }

    private IEnumerator PlayLegacyActionRoutine(PresentationEvent presentationEvent)
    {
        PublicActionType actionType = (PublicActionType)presentationEvent.legacyActionTypeValue;
        string actorLabel = GetActorLabel(presentationEvent.actorPlayerIndex);
        string sourceCards = FormatCardList(presentationEvent.sourceCardIds);
        string primaryCardId = GetPrimaryCardId(presentationEvent.sourceCardIds);
        string detail = string.IsNullOrEmpty(presentationEvent.message)
            ? $"{actorLabel} performed legacy action {GetLegacyActionLabel(actionType)} with cards: {sourceCards}"
            : presentationEvent.message;

        Debug.Log($"[Presentation][LegacyAction] {detail}");
        TryPlayCardVoice(primaryCardId, actionType);

        if (TryGetLegacyAnnouncementDescription(actionType, out string description))
        {
            yield return PlaySingleCardAnnouncementRoutine(
                presentationEvent.sourceCardIds,
                actorLabel,
                description,
                playCardPresentationDuration
            );
            yield break;
        }

        yield return new WaitForSeconds(legacyActionDuration);
    }

    private IEnumerator PlayRemoveCardsRoutine(PresentationEvent presentationEvent)
    {
        string actorLabel = GetActorLabel(presentationEvent.actorPlayerIndex);
        string sourceCards = FormatCardList(presentationEvent.sourceCardIds);
        string removedCards = FormatCardList(presentationEvent.beforeCardIds);
        string detail = string.IsNullOrEmpty(presentationEvent.message)
            ? $"{actorLabel} removed cards. Source: {sourceCards}; Removed: {removedCards}; Style: {presentationEvent.presentationStyle}"
            : presentationEvent.message;

        Debug.Log($"[Presentation][RemoveCards] {detail}");
        yield return PlaySingleCardAnnouncementRoutine(
            presentationEvent.beforeCardIds,
            actorLabel,
            "\u79fb\u9664",
            removeCardsDuration
        );
    }

    private IEnumerator PlayGainCardsRoutine(PresentationEvent presentationEvent)
    {
        string actorLabel = GetActorLabel(presentationEvent.actorPlayerIndex);
        string sourceCards = FormatCardList(presentationEvent.sourceCardIds);
        string gainedCards = FormatCardList(presentationEvent.afterCardIds);
        string detail = string.IsNullOrEmpty(presentationEvent.message)
            ? $"{actorLabel} gained cards. Source: {sourceCards}; Added: {gainedCards}; Style: {presentationEvent.presentationStyle}"
            : presentationEvent.message;

        Debug.Log($"[Presentation][GainCards] {detail}");
        yield return new WaitForSeconds(gainCardsDuration);
    }

    private IEnumerator PlayTransformCardsRoutine(PresentationEvent presentationEvent)
    {
        string actorLabel = GetActorLabel(presentationEvent.actorPlayerIndex);
        string beforeCards = FormatCardList(presentationEvent.beforeCardIds);
        string afterCards = FormatCardList(presentationEvent.afterCardIds);
        string detail = string.IsNullOrEmpty(presentationEvent.message)
            ? $"{actorLabel} transformed cards. Before: {beforeCards}; After: {afterCards}; Style: {presentationEvent.presentationStyle}"
            : presentationEvent.message;

        Debug.Log($"[Presentation][TransformCards] {detail}");
        yield return new WaitForSeconds(transformCardsDuration);
    }

    private IEnumerator PlayMoveCardsRoutine(PresentationEvent presentationEvent)
    {
        string actorLabel = GetActorLabel(presentationEvent.actorPlayerIndex);
        string beforeCards = FormatCardList(presentationEvent.beforeCardIds);
        string afterCards = FormatCardList(presentationEvent.afterCardIds);
        string detail = string.IsNullOrEmpty(presentationEvent.message)
            ? $"{actorLabel} moved cards. From: {beforeCards}; To: {afterCards}; Style: {presentationEvent.presentationStyle}"
            : presentationEvent.message;

        Debug.Log($"[Presentation][MoveCards] {detail}");
        yield return new WaitForSeconds(moveCardsDuration);
    }

    private IEnumerator PlayTextOnlyRoutine(PresentationEvent presentationEvent)
    {
        string actorLabel = GetActorLabel(presentationEvent.actorPlayerIndex);
        string detail = string.IsNullOrEmpty(presentationEvent.message)
            ? $"{actorLabel} triggered a text-only presentation."
            : presentationEvent.message;

        Debug.Log($"[Presentation][TextOnly] {detail}");
        yield return new WaitForSeconds(textOnlyDuration);
    }

    private IEnumerator PlaySingleCardAnnouncementRoutine(
        string[] primaryCardIds,
        string actorLabel,
        string description,
        float duration)
    {
        if (!ShouldShowSideAnnouncement())
        {
            yield break;
        }

        if (!EnsurePresentationPanelUI())
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        string primaryCardId = GetPrimaryCardId(primaryCardIds);
        Sprite cardSprite = GetCardSprite(primaryCardId);
        yield return presentationPanelUI.PlayCardAnnouncement(cardSprite, actorLabel, description, duration);
    }
    #endregion

    #region 队列清空确认
    // 回合切换时，服务器会等待本地演出队列清空后再进入下一回合。
    public void WaitUntilIdleThenAck(int waitId)
    {
        if (waitForIdleCoroutine != null)
        {
            StopCoroutine(waitForIdleCoroutine);
        }

        waitForIdleCoroutine = StartCoroutine(WaitUntilIdleThenAckRoutine(waitId));
    }

    private IEnumerator WaitUntilIdleThenAckRoutine(int waitId)
    {
        while (IsBusy)
        {
            yield return null;
        }

        waitForIdleCoroutine = null;

        if (NetworkClient.localPlayer == null)
            yield break;

        PlayerState localPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
        if (localPlayer == null)
            yield break;

        localPlayer.RequestReportPublicActionQueueDrained(waitId);
    }
    #endregion

    #region 辅助方法
    private string GetActorLabel(int actorPlayerIndex)
    {
        if (MatchManager.Instance == null)
            return $"Player{actorPlayerIndex + 1}";

        PlayerState player = MatchManager.Instance.GetPlayerByIndex(actorPlayerIndex);
        if (player == null || string.IsNullOrEmpty(player.playerName))
            return $"Player{actorPlayerIndex + 1}";

        return player.playerName;
    }

    private string GetLegacyActionLabel(PublicActionType actionType)
    {
        switch (actionType)
        {
            case PublicActionType.PlayCard:
                return "PlayCard";
            case PublicActionType.DefeatCenterMonster:
                return "DefeatCenterMonster";
            case PublicActionType.BuyCenterCard:
                return "BuyCenterCard";
            case PublicActionType.DefeatBaseMonster:
                return "DefeatBaseMonster";
            case PublicActionType.BuyBaseCard:
                return "BuyBaseCard";
            case PublicActionType.EquipCard:
                return "EquipCard";
            case PublicActionType.EquipWeapon:
                return "EquipWeapon";
            case PublicActionType.UseEquipment:
                return "UseEquipment";
            case PublicActionType.UseWeapon:
                return "UseWeapon";
            default:
                return actionType.ToString();
        }
    }

    private bool TryGetLegacyAnnouncementDescription(PublicActionType actionType, out string description)
    {
        switch (actionType)
        {
            case PublicActionType.PlayCard:
                description = "\u6253\u51fa";
                return true;

            case PublicActionType.BuyCenterCard:
            case PublicActionType.BuyBaseCard:
                description = "\u8d2d\u4e70";
                return true;

            case PublicActionType.DefeatCenterMonster:
            case PublicActionType.DefeatBaseMonster:
                description = "\u51fb\u8d25";
                return true;

            case PublicActionType.UseEquipment:
            case PublicActionType.UseWeapon:
                description = "\u4f7f\u7528";
                return true;

            default:
                description = string.Empty;
                return false;
        }
    }

    private string FormatCardList(string[] cardIds)
    {
        if (cardIds == null || cardIds.Length == 0)
            return "None";

        return string.Join(", ", cardIds);
    }

    private bool EnsurePresentationPanelUI()
    {
        if (presentationPanelUI != null)
            return true;

        if (PresentationPanelUI.Instance != null)
        {
            presentationPanelUI = PresentationPanelUI.Instance;
            return true;
        }

        GameObject panelObject = GameObject.Find("Presentation Panel");
        if (panelObject == null)
            return false;

        presentationPanelUI = panelObject.GetComponent<PresentationPanelUI>();
        if (presentationPanelUI == null)
        {
            presentationPanelUI = panelObject.AddComponent<PresentationPanelUI>();
        }

        return presentationPanelUI != null;
    }

    private bool ShouldShowSideAnnouncement()
    {
        if (NetworkClient.localPlayer == null || MatchManager.Instance == null)
            return true;

        PlayerState localPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
        PlayerState currentPlayer = MatchManager.Instance.GetCurrentPlayer();

        if (localPlayer == null || currentPlayer == null)
            return true;

        return localPlayer != currentPlayer;
    }

    private string GetPrimaryCardId(string[] cardIds)
    {
        if (cardIds == null || cardIds.Length == 0)
            return string.Empty;

        for (int i = 0; i < cardIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(cardIds[i]))
                return cardIds[i];
        }

        return string.Empty;
    }

    private Sprite GetCardSprite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId) || CardDatabase.Instance == null)
            return null;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
            return null;

        return cardData.cardSprite;
    }

    private void TryPlayCardVoice(string cardId, PublicActionType actionType)
    {
        if (GameAudioManager.Instance == null)
            return;

        GameAudioManager.Instance.TryPlayCardVoice(cardId, actionType);
    }
    #endregion
}
