using Mirror;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;

public class PlayerState : NetworkBehaviour
{
    private const int MaxEquippedFieldCardCount = 25;
    [Header("身份信息")]
    [SyncVar] public int playerIndex = -1;
    [SyncVar] public string playerName = "";
    [SyncVar] public string steamId = "";

    [Header("数值状态")]
    [SyncVar] public int score = 0;
    [SyncVar] public int mana = 0;
    [SyncVar] public int attack = 0;
    [SyncVar] public int handCount = 0;

    [Header("玩家状态")]
    [SyncVar] public bool isReady = false;
    [SyncVar] public bool isMyTurn = false;
    [SyncVar] public bool isWizard = false;

    [Header("牌堆数据")]
    public readonly SyncList<string> drawPile = new SyncList<string>();
    public readonly SyncList<string> discardPile = new SyncList<string>();
    public readonly SyncList<string> handCardIds = new SyncList<string>();
    public readonly SyncList<string> playedCardIds = new SyncList<string>();
    public readonly SyncList<string> ownedCardIds = new SyncList<string>();
    public readonly SyncList<string> banishCardIds = new SyncList<string>();    // 放逐堆
    public readonly SyncList<string> equippedCardIds = new SyncList<string>();
    public readonly SyncList<bool> equippedCardUsedFlags = new SyncList<bool>();
    [SyncVar] public string equippedWeaponCardId = "";
    [SyncVar] public bool equippedWeaponUsed = false;
    public readonly SyncList<string> playedEquipmentIds = new SyncList<string>();
    public readonly SyncList<string> activeStatusCardIds = new SyncList<string>();  // 状态栏

    [Header("本地玩家界面")]
    [SerializeField] private GameObject playerCanvas;
    public readonly SyncList<int> activeStatusStackCounts = new SyncList<int>();
    public readonly SyncList<int> activeStatusRemainingTurns = new SyncList<int>();
    public readonly SyncList<int> activeStatusAttackCleanseValues = new SyncList<int>();
    public readonly SyncList<int> activeStatusManaCleanseValues = new SyncList<int>();

    private int localPendingDrawDisplayCount = 0;
    private int localPendingScoreCompensation = 0;
    private int localPendingHandRevealCount = 0;
    private int localPendingReshuffleDrawCompensation = 0;
    private int localPendingReshuffleDiscardCompensation = 0;

    // 选择状态分为服务器端保存的负载，以及本地客户端界面展示的映射。
    [Header("待处理选择")]
    [SerializeField] private PendingSelectionType pendingSelectionType = PendingSelectionType.None;
    private readonly List<int> pendingSelectionPayloads = new List<int>();  // 服务器端等待选择时保存的权威负载。
    private readonly List<int> localSelectionPayloads = new List<int>();    // 玩家确认选择时，本地选项到负载的映射。
    private int pendingMinSelectCount = 1;
    private int pendingMaxSelectCount = 1;
    private int pendingTargetPlayerIndex = -1;
    private string pendingEquipReplacementCardId = "";
    private int pendingEquipReplacementHandIndex = -1;

    [Header("待处理公共动作")]
    private bool hasPendingPublicAction = false;
    private string pendingPublicCardId = "";
    private PublicActionType pendingPublicActionType = PublicActionType.PlayCard;
    private readonly List<string> playedCardHistoryIds = new List<string>();

    #region 玩家初始化
    [Server]
    public void InitPlayer(int index)
    {
        playerIndex = index;
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player" + (index + 1);
        }

        if (string.IsNullOrEmpty(steamId))
        {
            steamId = "NoSteamId";
        }

        score = 0;
        mana = 0;
        attack = 0;
        handCount = 0;

        isReady = false;
        isMyTurn = false;

        drawPile.Clear();
        discardPile.Clear();
        handCardIds.Clear();
        playedCardIds.Clear();
        ownedCardIds.Clear();
        banishCardIds.Clear();
        equippedCardIds.Clear();
        equippedCardUsedFlags.Clear();
        equippedWeaponCardId = "";
        equippedWeaponUsed = false;
        ClearStatusCardIds();
        playedCardHistoryIds.Clear();
        StatusEffectManager.Instance?.ClearRuntimeStates(this);
        ResetPendingSelectionContext();
        ClearPendingPublicAction();
    }
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (MatchManager.Instance != null)
        {
            Debug.Log("Registered to MatchManager: " + playerName + " netId=" + netId);
            MatchManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogError("MatchManager.Instance is null, register failed");
        }
    }
    public override void OnStopServer()
    {
        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.UnregisterPlayer(this);
        }

        base.OnStopServer();
    }
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ResetLocalDrawDisplayState();

        if (SteamManager.Initialized)
        {
            string localSteamName = SteamFriends.GetPersonaName();
            string localSteamId = SteamUser.GetSteamID().ToString();
            this.gameObject.name = localSteamName;
            CmdSetSteamProfile(localSteamName, localSteamId);
        }

        if (HandDisplayManager.Instance != null)
        {
            HandDisplayManager.Instance.RegisterLocalPlayer(this);
        }

        if (PlayerEndTurn.Instance != null)
        {
            PlayerEndTurn.Instance.RegisterLocalPlayer(this);
        }

        if (ShopPanelUI.Instance != null)
        {
            ShopPanelUI.Instance.RegisterLocalPlayer(this);
        }

        if (PileBrowserUI.Instance != null)
        {
            PileBrowserUI.Instance.RegisterLocalPlayer(this);
        }

        if (PileCountUI.Instance != null)
        {
            PileCountUI.Instance.RegisterLocalPlayer(this);
        }

        if (EquipmentZoneUI.Instance != null)
        {
            EquipmentZoneUI.Instance.RegisterLocalPlayer(this);
        }

        if (LocalTurnStartFxSpawner.Instance != null)
        {
            LocalTurnStartFxSpawner.Instance.RegisterLocalPlayer(this);
        }

        if (StatusAreaUI.Instance != null)
        {
            StatusAreaUI.Instance.RegisterLocalPlayer(this);
        }
    }
    [Command]
    private void CmdSetSteamProfile(string newName, string newSteamId)
    {
        playerName = newName;
        steamId = newSteamId;
    }

    #endregion

    private void Start()
    {
        // 只有本地玩家应保留自己的个人界面画布可见。
        if (!isLocalPlayer && playerCanvas != null)
        {
            playerCanvas.SetActive(false);
        }
    }

    #region 回合流程
    [Server]
    public void StartTurn()
    {
        isMyTurn = true;
        for (int i = 0; i < equippedCardUsedFlags.Count; i++)
        {
            equippedCardUsedFlags[i] = false;
        }

        equippedWeaponUsed = false;
        StatusEffectManager.Instance?.ResolveTurnStartStatuses(this);
    }

    [Server]
    public void EndTurn()
    {
        CancelPendingSelection();
        ClearPendingPublicAction();
        StatusEffectManager.Instance?.ResolveTurnEndStatuses(this);

        isMyTurn = false;
        mana = 0;
        attack = 0;
        isWizard = false;

        for (int i = 0; i < playedCardIds.Count; i++)
        {
            discardPile.Add(playedCardIds[i]);
        }

        playedCardIds.Clear();
        playedEquipmentIds.Clear();
        playedCardHistoryIds.Clear();

        for (int i = 0; i < handCardIds.Count; i++)
        {
            discardPile.Add(handCardIds[i]);
        }

        handCardIds.Clear();

        DrawCards(5);
        UpdateHandCount();
    }
    public void RequestEndTurn()
    {
        if (!isLocalPlayer) return;

        if (SelectionUI.Instance != null && SelectionUI.Instance.isSelecting)
        {
            if (HintManager.Instance != null)
            {
                HintManager.Instance.ShowHint("还有选择未完成");
            }
            return;
        }

        CmdRequestEndTurn();
    }
    [Command]
    private void CmdRequestEndTurn()
    {
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.gameStarted) return;

        PlayerState currentPlayer = MatchManager.Instance.GetCurrentPlayer();
        if (currentPlayer == null) return;
        if (currentPlayer != this) return;

        MatchManager.Instance.EndCurrentTurn();
    }

    public void RequestReportPublicActionQueueDrained(int waitId)
    {
        if (!isLocalPlayer)
            return;

        CmdReportPublicActionQueueDrained(waitId);
    }

    [Command]
    private void CmdReportPublicActionQueueDrained(int waitId)
    {
        if (MatchManager.Instance == null)
            return;

        MatchManager.Instance.ReportPublicActionQueueDrained(this, waitId);
    }

    // 延迟结算的效果会先缓存对应的公共动作，直到相关选择完成。
    private bool HandleEffectResultForPublicAction(CardEffectResult effectResult, string cardId, PublicActionType actionType)
    {
        if (effectResult == CardEffectResult.Failed)
            return false;

        if (effectResult == CardEffectResult.Applied)
        {
            BroadcastPublicAction(cardId, actionType);
        }
        else if (effectResult == CardEffectResult.Pending)
        {
            CachePendingPublicAction(cardId, actionType);
        }

        return true;
    }

    private void BroadcastPublicAction(string cardId, PublicActionType actionType)
    {
        if (MatchManager.Instance == null)
            return;

        MatchManager.Instance.BroadcastPublicAction(playerIndex, cardId, actionType);
    }

    private void BroadcastPresentationEvent(PresentationEvent presentationEvent)
    {
        if (MatchManager.Instance == null)
            return;

        MatchManager.Instance.BroadcastPresentationEvent(presentationEvent);
    }

    private void CachePendingPublicAction(string cardId, PublicActionType actionType)
    {
        hasPendingPublicAction = true;
        pendingPublicCardId = cardId;
        pendingPublicActionType = actionType;
    }

    private void ClearPendingPublicAction()
    {
        hasPendingPublicAction = false;
        pendingPublicCardId = "";
        pendingPublicActionType = PublicActionType.PlayCard;
    }

    private string[] WrapCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return Array.Empty<string>();

        return new[] { cardId };
    }

    [TargetRpc]
    private void TargetPlayShopResultFx(NetworkConnectionToClient target, int slotIndex, string cardId, bool isBaseShop)
    {
        if (ShopPanelUI.Instance == null)
            return;

        ShopPanelUI.Instance.PlayLocalShopResultFx(slotIndex, cardId, isBaseShop);
    }

    [TargetRpc]
    private void TargetPlayShopExileFx(NetworkConnectionToClient target, int slotIndex, string cardId, bool isBaseShop)
    {
        if (ShopPanelUI.Instance == null)
            return;

        ShopPanelUI.Instance.PlayLocalShopExileFx(slotIndex, cardId, isBaseShop);
    }

    [Server]
    public void ShowHintToOwner(string message)
    {
        if (connectionToClient == null)
            return;

        TargetShowHint(connectionToClient, message);
    }

    [TargetRpc]
    private void TargetShowHint(NetworkConnectionToClient target, string message)
    {
        if (HintManager.Instance == null)
            return;

        HintManager.Instance.ShowHint(message);
    }

    [TargetRpc]
    private void TargetPlayHandTransformFx(NetworkConnectionToClient target, int handIndex, string oldCardId, string newCardId)
    {
        if (HandDisplayManager.Instance == null)
            return;

        HandDisplayManager.Instance.PlayTransformFxByIndex(handIndex, oldCardId, newCardId);
    }

    [TargetRpc]
    private void TargetNotifyIncomingDrawFx(NetworkConnectionToClient target, string cardId, int drawFxModeRaw)
    {
        HandCardDrawFxMode drawFxMode = HandCardDrawFxMode.ToHand;
        if (Enum.IsDefined(typeof(HandCardDrawFxMode), drawFxModeRaw))
        {
            drawFxMode = (HandCardDrawFxMode)drawFxModeRaw;
        }

        NotifyLocalIncomingDrawVisual(cardId, drawFxMode);

        if (HandDisplayManager.Instance == null)
        {
            NotifyLocalDrawVisualStarted(cardId, drawFxMode);
            NotifyLocalDrawVisualResolved(cardId, drawFxMode);
            return;
        }

        HandDisplayManager.Instance.NotifyIncomingDrawFx(cardId, drawFxMode);
    }

    [TargetRpc]
    private void TargetNotifyIncomingPileToHandFx(NetworkConnectionToClient target, string cardId, int sourceTypeRaw)
    {
        if (HandDisplayManager.Instance == null)
            return;

        HandCardPileToHandFxSourceType sourceType = HandCardPileToHandFxSourceType.DiscardPile;
        if (Enum.IsDefined(typeof(HandCardPileToHandFxSourceType), sourceTypeRaw))
        {
            sourceType = (HandCardPileToHandFxSourceType)sourceTypeRaw;
        }

        HandDisplayManager.Instance.NotifyIncomingPileToHandFx(cardId, sourceType);
    }

    [TargetRpc]
    private void TargetNotifyIncomingReshuffleFx(NetworkConnectionToClient target, int movedCardCount)
    {
        NotifyLocalIncomingReshuffleVisual(movedCardCount);

        if (HandDisplayManager.Instance == null)
        {
            NotifyLocalReshuffleVisualResolved(movedCardCount);
            return;
        }

        HandDisplayManager.Instance.NotifyIncomingReshuffleFx(movedCardCount);
    }

    [TargetRpc]
    private void TargetNotifyIncomingHandExileFx(NetworkConnectionToClient target, int handIndex, string cardId)
    {
        if (HandDisplayManager.Instance == null)
            return;

        HandDisplayManager.Instance.NotifyIncomingHandExileFx(handIndex, cardId);
    }

    [TargetRpc]
    private void TargetPlayPileExileFx(NetworkConnectionToClient target, string cardId, int sourceTypeRaw)
    {
        HandCardExileFxSource sourceType = HandCardExileFxSource.DiscardPile;
        if (Enum.IsDefined(typeof(HandCardExileFxSource), sourceTypeRaw))
        {
            sourceType = (HandCardExileFxSource)sourceTypeRaw;
        }

        if (sourceType == HandCardExileFxSource.Hand)
            return;

        HandCardExileFxUI.TryQueueFromPile(cardId, sourceType);
    }

    public int GetDisplayedDrawPileCount()
    {
        if (!isLocalPlayer)
            return drawPile.Count;

        return Mathf.Max(0, drawPile.Count + Mathf.Max(0, localPendingDrawDisplayCount) - Mathf.Max(0, localPendingReshuffleDrawCompensation));
    }

    public int GetDisplayedDiscardPileCount()
    {
        if (!isLocalPlayer)
            return discardPile.Count;

        return Mathf.Max(0, discardPile.Count + Mathf.Max(0, localPendingReshuffleDiscardCompensation));
    }

    public int GetDisplayedScore()
    {
        if (!isLocalPlayer)
            return score;

        return score + Mathf.Max(0, localPendingScoreCompensation);
    }

    public int GetDisplayedHandCount()
    {
        if (!isLocalPlayer)
            return handCount;

        return Mathf.Max(0, handCount - Mathf.Max(0, localPendingHandRevealCount));
    }

    private void ResetLocalDrawDisplayState()
    {
        localPendingDrawDisplayCount = 0;
        localPendingScoreCompensation = 0;
        localPendingHandRevealCount = 0;
        localPendingReshuffleDrawCompensation = 0;
        localPendingReshuffleDiscardCompensation = 0;
        RefreshLocalDrawDisplayUi();
    }

    private void RefreshLocalDrawDisplayUi()
    {
        if (!isLocalPlayer)
            return;

        if (PileCountUI.Instance != null)
        {
            PileCountUI.Instance.RefreshCounts();
        }
    }

    public void NotifyLocalIncomingDrawVisual(string cardId, HandCardDrawFxMode drawFxMode)
    {
        if (!isLocalPlayer)
            return;

        localPendingDrawDisplayCount++;

        if (drawFxMode == HandCardDrawFxMode.ToHand)
        {
            localPendingHandRevealCount++;
        }

        if (cardId == "19002")
        {
            localPendingScoreCompensation++;
        }

        RefreshLocalDrawDisplayUi();
    }

    public void NotifyLocalDrawVisualStarted(string cardId, HandCardDrawFxMode drawFxMode)
    {
        if (!isLocalPlayer)
            return;

        localPendingDrawDisplayCount = Mathf.Max(0, localPendingDrawDisplayCount - 1);
        RefreshLocalDrawDisplayUi();
    }

    public void NotifyLocalDrawVisualResolved(string cardId, HandCardDrawFxMode drawFxMode)
    {
        if (!isLocalPlayer)
            return;

        if (drawFxMode == HandCardDrawFxMode.ToHand)
        {
            localPendingHandRevealCount = Mathf.Max(0, localPendingHandRevealCount - 1);
        }

        if (cardId == "19002")
        {
            localPendingScoreCompensation = Mathf.Max(0, localPendingScoreCompensation - 1);
        }

        RefreshLocalDrawDisplayUi();
    }

    public void NotifyLocalIncomingReshuffleVisual(int movedCardCount)
    {
        if (!isLocalPlayer || movedCardCount <= 0)
            return;

        localPendingReshuffleDrawCompensation += movedCardCount;
        localPendingReshuffleDiscardCompensation += movedCardCount;
        RefreshLocalDrawDisplayUi();
    }

    public void NotifyLocalReshuffleVisualResolved(int movedCardCount)
    {
        if (!isLocalPlayer || movedCardCount <= 0)
            return;

        localPendingReshuffleDrawCompensation = Mathf.Max(0, localPendingReshuffleDrawCompensation - movedCardCount);
        localPendingReshuffleDiscardCompensation = Mathf.Max(0, localPendingReshuffleDiscardCompensation - movedCardCount);
        RefreshLocalDrawDisplayUi();
    }
    #endregion

    #region 资源操作
    [Server]
    public void AddMana(int amount)
    {
        mana += amount;
    }
    [Server]
    public void AddAttack(int amount)
    {
        attack += amount;
    }
    [Server]
    public void AddScore(int amount)
    {
        score += amount;
    }

    [Server]
    public bool SpendMana(int amount)
    {
        if (mana < amount) return false;

        mana -= amount;
        return true;
    }
    [Server]
    public bool SpendAttack(int amount)
    {
        if (attack < amount) return false;

        attack -= amount;
        return true;
    }
    #endregion

    #region 打牌请求

    // 手牌操作先由本地发起请求，再由服务器进行权威结算。
    public void RequestPlayCard(int handIndex)      
    {
        if (!isLocalPlayer)
            return;
        if (handIndex < 0 || handIndex >= handCardIds.Count)
            return;

        if (!isMyTurn)
        {
            if (HintManager.Instance != null)
            {
                HintManager.Instance.ShowHint("不是你的回合");
            }
            return;
        }

        if (SelectionUI.Instance != null && SelectionUI.Instance.isSelecting)
        {
            if (HintManager.Instance != null)
            {
                HintManager.Instance.ShowHint("还有选择未完成");
            }
            return;
        }

        string cardId = handCardIds[handIndex];
        if (cardId == "00006" && handCardIds.Count <= 1)
        {
            if (HintManager.Instance != null)
            {
                HintManager.Instance.ShowHint("没有可放逐的手牌");
            }
            return;
        }
        
        CmdRequestPlayCard(handIndex);
    }
    [Command]
    public void CmdRequestPlayCard(int handIndex)
    {
        if (!isMyTurn) return;
        if (handIndex < 0 || handIndex >= handCardIds.Count) return;

        string cardId = handCardIds[handIndex];
        if (string.IsNullOrEmpty(cardId)) return;
        if (CardDatabase.Instance == null) return;

        CardData card = CardDatabase.Instance.GetCardById(cardId);
        if (card == null) return;
        if (cardId == "00006" && handCardIds.Count <= 1) return;

        switch (card.cardType)
        {
            case CardType.Field:
                if (!EquipCardFromHand(cardId, handIndex))
                    return;
                break;

            case CardType.Weapon:
                if (!EquipWeaponFromHand(cardId, handIndex))
                    return;
                break;

            default:
                {
                    if (!PlayCardFromHand(cardId, handIndex))
                        return;

                    CardEffectResult effectResult = CardEffectManager.Instance.ResolveCardEffect(playerIndex, cardId);
                    if (!HandleEffectResultForPublicAction(effectResult, cardId, PublicActionType.PlayCard))
                        return;

                    break;
                }
                
        }
    }
    #endregion

    #region 商店购买请求
    public void RequestBuyCard(int slotIndex)
    {
        if (!isLocalPlayer) return;
        if (!isMyTurn) return;

        if (slotIndex < 5)
        {
            CmdRequestBuyCenterCard(slotIndex);
        }
        else
        {
            CmdRequestBuyBaseCard(slotIndex - 5);
        }
    }
    // 购买中央商店卡牌时，可能会在槽位补牌前先结算卡牌效果。
    [Command]
    private void CmdRequestBuyCenterCard(int slotIndex)
    {
        PlayerState currentPlayer = MatchManager.Instance.GetCurrentPlayer();
        if (currentPlayer != this) return;

        string cardId = ShopState.Instance.centerCardIds[slotIndex];
        CardData card = CardDatabase.Instance.GetCardById(cardId);
        if (card.cardType == CardType.Enemy)
        {
            if (SpendAttack(card.cost))
            {
                CardEffectResult effectResult = CardEffectManager.Instance.ResolveCardEffect(playerIndex, cardId);
                if (!HandleEffectResultForPublicAction(effectResult, cardId, PublicActionType.DefeatCenterMonster))
                    return;

                TargetPlayShopResultFx(connectionToClient, slotIndex, cardId, false);
            }
            else { return; }
        }
        else
        {
            if (SpendMana(card.cost))
            {
                AddCardToOwned(cardId);
                AddCardToDiscard(cardId);
                TargetPlayShopResultFx(connectionToClient, slotIndex, cardId, false);
                BroadcastPublicAction(cardId, PublicActionType.BuyCenterCard);
            }
            else { return; }
        }

        ShopState.Instance.RemoveCenterCard(slotIndex);
    }
    [Command]
    private void CmdRequestBuyBaseCard(int baseIndex)
    {
        PlayerState currentPlayer = MatchManager.Instance.GetCurrentPlayer();
        if (currentPlayer != this) return;

        string cardId = ShopState.Instance.baseCardIds[baseIndex];
        CardData card = CardDatabase.Instance.GetCardById(cardId);
        if (card.cardType == CardType.Enemy)
        {
            if (SpendAttack(card.cost))
            {
                currentPlayer.AddScore(1);
                TargetPlayShopResultFx(connectionToClient, baseIndex, cardId, true);
                BroadcastPublicAction(cardId, PublicActionType.DefeatBaseMonster);
            }
            else { return; }
        }
        else if (card.cardCategory == CardCategory.Basic)
        {
            if (SpendMana(card.cost))
            {
                AddCardToOwned(cardId);
                AddCardToDiscard(cardId);
                TargetPlayShopResultFx(connectionToClient, baseIndex, cardId, true);
                BroadcastPublicAction(cardId, PublicActionType.BuyBaseCard);
            }
            else { return; }
        }
    }
    
    [Server]
    public void AddCardToOwned(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        ownedCardIds.Add(cardId);
    }

    [Server]
    public bool RemoveCardFromOwned(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        int index = ownedCardIds.IndexOf(cardId);
        if (index < 0) return false;

        ownedCardIds.RemoveAt(index);
        return true;
    }
    [Server]
    public void AddCardToDiscard(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        discardPile.Add(cardId);
    }

    [Server]
    public void AddCardToDiscardTop(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        // 当前项目里弃牌堆新增卡默认都追加到末尾，这里沿用同一套“堆顶”语义。
        discardPile.Add(cardId);
    }

    [Server]
    public CardEffectResult AddCardToBanish(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return CardEffectResult.Failed;

        RemoveCardFromOwned(cardId);
        banishCardIds.Add(cardId);

        if (CardEffectManager.Instance == null)
            return CardEffectResult.Applied;

        return CardEffectManager.Instance.ResolveBanishEnterEffect(playerIndex, cardId);
    }
    [Server]
    public void AddCardToDrawPile(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        drawPile.Add(cardId);
    }

    [Server]
    public void AddStatusCardId(string statusCardId)
    {
        if (string.IsNullOrEmpty(statusCardId))
            return;

        activeStatusCardIds.Add(statusCardId);
    }

    [Server]
    public void AddStatusDisplayData(int stackCount, int remainingTurns, int attackCleanseValue, int manaCleanseValue)
    {
        activeStatusStackCounts.Add(stackCount);
        activeStatusRemainingTurns.Add(remainingTurns);
        activeStatusAttackCleanseValues.Add(attackCleanseValue);
        activeStatusManaCleanseValues.Add(manaCleanseValue);
    }

    [Server]
    public void UpdateStatusDisplayDataAt(int index, int stackCount, int remainingTurns, int attackCleanseValue, int manaCleanseValue)
    {
        if (index < 0 || index >= activeStatusStackCounts.Count)
            return;
        if (index >= activeStatusRemainingTurns.Count || index >= activeStatusAttackCleanseValues.Count || index >= activeStatusManaCleanseValues.Count)
            return;

        activeStatusStackCounts[index] = stackCount;
        activeStatusRemainingTurns[index] = remainingTurns;
        activeStatusAttackCleanseValues[index] = attackCleanseValue;
        activeStatusManaCleanseValues[index] = manaCleanseValue;
    }

    [Server]
    public void RemoveStatusDisplayDataAt(int index)
    {
        if (index < 0)
            return;
        if (index >= activeStatusStackCounts.Count || index >= activeStatusRemainingTurns.Count || index >= activeStatusAttackCleanseValues.Count || index >= activeStatusManaCleanseValues.Count)
            return;

        activeStatusStackCounts.RemoveAt(index);
        activeStatusRemainingTurns.RemoveAt(index);
        activeStatusAttackCleanseValues.RemoveAt(index);
        activeStatusManaCleanseValues.RemoveAt(index);
    }

    [Server]
    public bool RemoveStatusCardId(string statusCardId)
    {
        if (string.IsNullOrEmpty(statusCardId))
            return false;

        int index = activeStatusCardIds.IndexOf(statusCardId);
        if (index < 0)
            return false;

        activeStatusCardIds.RemoveAt(index);
        return true;
    }

    [Server]
    public bool HasStatusCardId(string statusCardId)
    {
        if (string.IsNullOrEmpty(statusCardId))
            return false;

        return activeStatusCardIds.IndexOf(statusCardId) >= 0;
    }

    [Server]
    public int GetStatusCardCount(string statusCardId)
    {
        if (string.IsNullOrEmpty(statusCardId))
            return 0;

        int count = 0;
        for (int i = 0; i < activeStatusCardIds.Count; i++)
        {
            if (activeStatusCardIds[i] == statusCardId)
            {
                count++;
            }
        }

        return count;
    }

    [Server]
    public void ClearStatusCardIds()
    {
        activeStatusCardIds.Clear();
        activeStatusStackCounts.Clear();
        activeStatusRemainingTurns.Clear();
        activeStatusAttackCleanseValues.Clear();
        activeStatusManaCleanseValues.Clear();
    }
    #endregion

    #region 牌堆管理
    [Server]
    public void BuildStartDeck(List<string> startCards)
    {
        drawPile.Clear();
        discardPile.Clear();
        handCardIds.Clear();
        playedCardIds.Clear();
        ownedCardIds.Clear();
        banishCardIds.Clear();
        ClearStatusCardIds();
        playedCardHistoryIds.Clear();
        StatusEffectManager.Instance?.ClearRuntimeStates(this);

        if (startCards == null) return;

        for (int i = 0; i < startCards.Count; i++)
        {
            string cardId = startCards[i];

            if (string.IsNullOrEmpty(cardId)) continue;

            drawPile.Add(cardId);
            ownedCardIds.Add(cardId);
        }

        ShuffleDrawPile();
        UpdateHandCount();
    }

    [Server]
    public void ShuffleDrawPile()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, drawPile.Count);

            string temp = drawPile[i];
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = temp;
        }
    }
    [Server]
    public void RebuildDrawPileFromDiscard()
    {
        if (discardPile.Count == 0) return;

        int movedCardCount = discardPile.Count;
        if (connectionToClient != null)
        {
            TargetNotifyIncomingReshuffleFx(connectionToClient, movedCardCount);
        }

        for (int i = 0; i < discardPile.Count; i++)
        {
            drawPile.Add(discardPile[i]);
        }

        discardPile.Clear();
        ShuffleDrawPile();
    }

    #endregion
    #region 抽牌操作
    [Server]
    public string DrawOneCard()
    {
        if (drawPile.Count == 0)
        {
            RebuildDrawPileFromDiscard();
        }

        if (drawPile.Count == 0)
        {
            HintManager.Instance.ShowHint("抽牌堆为空");
            return "";
        }

        string cardId = drawPile[0];
        if (connectionToClient != null)
        {
            HandCardDrawFxMode drawFxMode = HandCardDrawFxMode.ToHand;
            if (CardEffectManager.Instance != null)
            {
                drawFxMode = CardEffectManager.Instance.GetDrawFxMode(cardId);
            }

            TargetNotifyIncomingDrawFx(connectionToClient, cardId, (int)drawFxMode);
        }

        drawPile.RemoveAt(0);
        EnterHandCard(cardId, false, true);
        return cardId;
    }
    [Server]
    public void DrawCards(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            string cardId = DrawOneCard();

            if (string.IsNullOrEmpty(cardId))
                return;
        }
    }
    #endregion

    [Server]
    public bool PlayCardFromHand(string cardId, int index)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        handCardIds.RemoveAt(index);
        playedCardIds.Add(cardId);
        playedCardHistoryIds.Add(cardId);

        UpdateHandCount();
        return true;
    }

    [Server]
    public string GetPreviousPlayedCardId()
    {
        if (playedCardHistoryIds.Count < 2)
            return "";

        return playedCardHistoryIds[playedCardHistoryIds.Count - 2];
    }

    [Server]
    public CardEffectResult BanishPlayedCardById(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;

        int playedIndex = playedCardIds.IndexOf(cardId);
        if (playedIndex < 0)
            return CardEffectResult.Failed;

        if (connectionToClient != null)
        {
            TargetPlayPileExileFx(connectionToClient, cardId, (int)HandCardExileFxSource.PlayedPile);
        }

        playedCardIds.RemoveAt(playedIndex);
        return AddCardToBanish(cardId);
    }

    [Server]
    public bool MovePlayedCardToDrawPileBottom(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return false;

        for (int i = playedCardIds.Count - 1; i >= 0; i--)
        {
            if (playedCardIds[i] != cardId)
                continue;

            playedCardIds.RemoveAt(i);
            drawPile.Add(cardId);
            return true;
        }

        return false;
    }

    [Server]
    private bool EquipCardFromHand(string cardId, int handIndex)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        if (equippedCardIds.Count >= MaxEquippedFieldCardCount)
        {
            return BeginReplaceEquippedCardSelection(cardId, handIndex);
        }

        CardEffectResult effectResult = EquipCardFromHandInternal(cardId, handIndex);
        return HandleEffectResultForPublicAction(effectResult, cardId, PublicActionType.EquipCard);
    }

    [Server]
    private bool EquipWeaponFromHand(string cardId, int handIndex)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        handCardIds.RemoveAt(handIndex);

        if (!string.IsNullOrEmpty(equippedWeaponCardId))
        {
            string oldWeaponCardId = equippedWeaponCardId;

            equippedWeaponCardId = "";
            equippedWeaponUsed = false;

            discardPile.Add(oldWeaponCardId);
            CardEffectManager.Instance.ResolveEquipLeaveToDiscardEffect(playerIndex, oldWeaponCardId);
        }

        equippedWeaponCardId = cardId;
        equippedWeaponUsed = false;
        playedEquipmentIds.Add(cardId);
        playedCardHistoryIds.Add(cardId);

        UpdateHandCount();

        CardEffectResult effectResult = CardEffectManager.Instance.ResolveEquipEnterEffect(playerIndex, cardId);
        return HandleEffectResultForPublicAction(effectResult, cardId, PublicActionType.EquipWeapon);
    }

    [Server]
    private bool BeginReplaceEquippedCardSelection(string incomingCardId, int incomingHandIndex)
    {
        if (string.IsNullOrEmpty(incomingCardId))
            return false;
        if (incomingHandIndex < 0 || incomingHandIndex >= handCardIds.Count)
            return false;

        List<string> optionCardIds = new List<string>();
        List<int> optionPayloads = new List<int>();

        for (int i = 0; i < equippedCardIds.Count; i++)
        {
            string equippedCardId = equippedCardIds[i];
            if (string.IsNullOrEmpty(equippedCardId))
                continue;

            optionCardIds.Add(equippedCardId);
            optionPayloads.Add(i);
        }

        if (optionCardIds.Count == 0)
            return false;

        pendingEquipReplacementCardId = incomingCardId;
        pendingEquipReplacementHandIndex = incomingHandIndex;

        BeginSelection(
            PendingSelectionType.ReplaceOneEquippedCard,
            "Select 1 equipment to replace",
            1,
            1,
            optionCardIds,
            optionPayloads
        );

        return true;
    }

    [Server]
    private CardEffectResult EquipCardFromHandInternal(string cardId, int handIndex)
    {
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;
        if (handIndex < 0 || handIndex >= handCardIds.Count)
            return CardEffectResult.Failed;

        handCardIds.RemoveAt(handIndex);

        equippedCardIds.Add(cardId);
        equippedCardUsedFlags.Add(false);
        playedEquipmentIds.Add(cardId);
        playedCardHistoryIds.Add(cardId);
        UpdateHandCount();

        if (CardEffectManager.Instance == null)
            return CardEffectResult.Applied;

        return CardEffectManager.Instance.ResolveEquipEnterEffect(playerIndex, cardId);
    }

    [Server]
    public bool DiscardEquippedCardByIndex(int equipmentIndex, out string discardedCardId)
    {
        discardedCardId = "";
        if (equipmentIndex < 0 || equipmentIndex >= equippedCardIds.Count)
            return false;

        discardedCardId = equippedCardIds[equipmentIndex];
        if (string.IsNullOrEmpty(discardedCardId))
            return false;

        equippedCardIds.RemoveAt(equipmentIndex);
        if (equipmentIndex < equippedCardUsedFlags.Count)
        {
            equippedCardUsedFlags.RemoveAt(equipmentIndex);
        }

        discardPile.Add(discardedCardId);
        CardEffectManager.Instance?.ResolveEquipLeaveToDiscardEffect(playerIndex, discardedCardId);
        return true;
    }

    [Server]
    private bool TryResolvePendingEquipReplacementHandIndex(out int resolvedHandIndex)
    {
        resolvedHandIndex = -1;

        if (string.IsNullOrEmpty(pendingEquipReplacementCardId))
            return false;
        if (pendingEquipReplacementHandIndex < 0 || pendingEquipReplacementHandIndex >= handCardIds.Count)
            return false;
        if (handCardIds[pendingEquipReplacementHandIndex] != pendingEquipReplacementCardId)
            return false;

        resolvedHandIndex = pendingEquipReplacementHandIndex;
        return true;
    }

    [Server]
    public bool RemoveCardFromHand(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        int index = handCardIds.IndexOf(cardId);
        if (index < 0) return false;

        handCardIds.RemoveAt(index);
        UpdateHandCount();
        return true;
    }

    [Server]
    public void DiscardHandCard(string cardId)
    {
        if (RemoveCardFromHand(cardId))
        {
            discardPile.Add(cardId);
        }
    }

    [Server]
    public bool TransformHandCardByIndex(int handIndex, string newCardId, out string oldCardId)
    {
        oldCardId = "";

        if (handIndex < 0 || handIndex >= handCardIds.Count)
            return false;
        if (string.IsNullOrEmpty(newCardId))
            return false;

        oldCardId = handCardIds[handIndex];
        if (string.IsNullOrEmpty(oldCardId))
            return false;

        bool isSameCardTransform = oldCardId == newCardId;
        if (!isSameCardTransform)
        {
            handCardIds[handIndex] = newCardId;
        }

        RemoveCardFromOwned(oldCardId);
        AddCardToOwned(newCardId);
        UpdateHandCount();

        if (CardEffectManager.Instance != null)
        {
            CardEffectResult transformEffectResult = CardEffectManager.Instance.ResolveHandTransformEffect(playerIndex, oldCardId, newCardId);
            if (transformEffectResult == CardEffectResult.Failed)
            {
                Debug.LogWarning($"Failed to resolve transform effect for card {oldCardId} -> {newCardId}.");
            }
        }

        if (isSameCardTransform && connectionToClient != null)
        {
            TargetPlayHandTransformFx(connectionToClient, handIndex, oldCardId, newCardId);
        }

        return true;
    }

    [Server]
    public bool DiscardHandCardByIndex(int handIndex, out string cardId)
    {
        cardId = "";

        if (handIndex < 0 || handIndex >= handCardIds.Count)
            return false;

        cardId = handCardIds[handIndex];
        if (string.IsNullOrEmpty(cardId))
            return false;

        handCardIds.RemoveAt(handIndex);
        discardPile.Add(cardId);
        UpdateHandCount();
        return true;
    }

    [Server]
    public CardEffectResult BanishHandCardByIndex(int handIndex, out string cardId, bool playExileFx = true)
    {
        cardId = "";

        if (handIndex < 0 || handIndex >= handCardIds.Count)
            return CardEffectResult.Failed;

        cardId = handCardIds[handIndex];
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;

        if (playExileFx && connectionToClient != null)
        {
            TargetNotifyIncomingHandExileFx(connectionToClient, handIndex, cardId);
        }

        handCardIds.RemoveAt(handIndex);
        UpdateHandCount();

        return AddCardToBanish(cardId);
    }

    [Server]
    public int GetLastHandCardIndex(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return -1;

        for (int i = handCardIds.Count - 1; i >= 0; i--)
        {
            if (handCardIds[i] == cardId)
                return i;
        }

        return -1;
    }

    [Server]
    public CardEffectResult MoveDiscardCardToHandByIndex(int discardIndex, out string cardId)
    {
        cardId = "";

        if (discardIndex < 0 || discardIndex >= discardPile.Count)
            return CardEffectResult.Failed;

        cardId = discardPile[discardIndex];
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;

        if (connectionToClient != null)
        {
            TargetNotifyIncomingPileToHandFx(connectionToClient, cardId, (int)HandCardPileToHandFxSourceType.DiscardPile);
        }

        discardPile.RemoveAt(discardIndex);
        return EnterHandCard(cardId, true, false); 
    }

    [Server]
    public CardEffectResult MoveDrawPileCardToHandByIndex(int drawIndex, out string cardId)
    {
        cardId = "";

        if (drawIndex < 0 || drawIndex >= drawPile.Count)
            return CardEffectResult.Failed;

        cardId = drawPile[drawIndex];
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;

        if (connectionToClient != null)
        {
            HandCardDrawFxMode drawFxMode = HandCardDrawFxMode.ToHand;
            if (CardEffectManager.Instance != null)
            {
                drawFxMode = CardEffectManager.Instance.GetDrawFxMode(cardId);
            }

            TargetNotifyIncomingDrawFx(connectionToClient, cardId, (int)drawFxMode);
        }

        drawPile.RemoveAt(drawIndex);
        return EnterHandCard(cardId, false, true);
    }

    [Server]
    public CardEffectResult BanishDiscardPileCardByIndex(int discardIndex, out string cardId)
    {
        cardId = "";

        if (discardIndex < 0 || discardIndex >= discardPile.Count)
            return CardEffectResult.Failed;

        cardId = discardPile[discardIndex];
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;

        if (connectionToClient != null)
        {
            TargetPlayPileExileFx(connectionToClient, cardId, (int)HandCardExileFxSource.DiscardPile);
        }

        discardPile.RemoveAt(discardIndex);
        return AddCardToBanish(cardId);
    }

    [Server]
    public CardEffectResult BanishDrawPileCardByIndex(int drawIndex, out string cardId)
    {
        cardId = "";

        if (drawIndex < 0 || drawIndex >= drawPile.Count)
            return CardEffectResult.Failed;

        cardId = drawPile[drawIndex];
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;

        if (connectionToClient != null)
        {
            TargetPlayPileExileFx(connectionToClient, cardId, (int)HandCardExileFxSource.DrawPile);
        }

        drawPile.RemoveAt(drawIndex);
        return AddCardToBanish(cardId);
    }

    [Server]
    private void UpdateHandCount()
    {
        handCount = handCardIds.Count;
    }

    [Server]
    private CardEffectResult EnterHandCard(string cardId, bool fromDiscardPile, bool fromDrawPile)
    {
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;

        handCardIds.Add(cardId);
        UpdateHandCount();

        CardEffectResult effectResult = ResolveHandEnterEffect(cardId);
        if (effectResult == CardEffectResult.Failed)
            return CardEffectResult.Failed;

        CardEffectResult drawEnterEffectResult = CardEffectResult.Applied;
        if (fromDrawPile)
        {
            drawEnterEffectResult = ResolveHandEnterFromDrawEffect(cardId);
            if (drawEnterEffectResult == CardEffectResult.Failed)
                return CardEffectResult.Failed;
        }

        CardEffectResult discardEnterEffectResult = CardEffectResult.Applied;
        if (fromDiscardPile)
        {
            discardEnterEffectResult = ResolveHandEnterFromDiscardEffect(cardId);
            if (discardEnterEffectResult == CardEffectResult.Failed)
                return CardEffectResult.Failed;
        }

        if (effectResult == CardEffectResult.Pending ||
            drawEnterEffectResult == CardEffectResult.Pending ||
            discardEnterEffectResult == CardEffectResult.Pending)
            return CardEffectResult.Pending;

        return CardEffectResult.Applied;
    }

    [Server]
    private CardEffectResult ResolveHandEnterEffect(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;
        if (CardEffectManager.Instance == null)
            return CardEffectResult.Applied;

        CardEffectResult effectResult = CardEffectManager.Instance.ResolveHandEnterEffect(playerIndex, cardId);
        if (effectResult == CardEffectResult.Failed)
        {
            Debug.LogWarning($"Failed to resolve hand enter effect for card {cardId}.");
        }

        return effectResult;
    }

    [Server]
    private CardEffectResult ResolveHandEnterFromDrawEffect(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;
        if (CardEffectManager.Instance == null)
            return CardEffectResult.Applied;

        CardEffectResult effectResult = CardEffectManager.Instance.ResolveHandEnterFromDrawEffect(playerIndex, cardId);
        if (effectResult == CardEffectResult.Failed)
        {
            Debug.LogWarning($"Failed to resolve draw-to-hand enter effect for card {cardId}.");
        }

        return effectResult;
    }

    [Server]
    private CardEffectResult ResolveHandEnterFromDiscardEffect(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return CardEffectResult.Failed;
        if (CardEffectManager.Instance == null)
            return CardEffectResult.Applied;

        CardEffectResult effectResult = CardEffectManager.Instance.ResolveHandEnterFromDiscardEffect(playerIndex, cardId);
        if (effectResult == CardEffectResult.Failed)
        {
            Debug.LogWarning($"Failed to resolve discard-to-hand enter effect for card {cardId}.");
        }

        return effectResult;
    }

    #region 装备激活
    public void RequestUseWeapon()
    {
        if (!isLocalPlayer) return;
        if (!isMyTurn) return;

        if (SelectionUI.Instance != null && SelectionUI.Instance.isSelecting)
            return;

        CmdRequestUseWeapon();
    }
    [Command]
    private void CmdRequestUseWeapon()
    {
        if (!isMyTurn) return;
        if (string.IsNullOrEmpty(equippedWeaponCardId)) return;
        if (equippedWeaponUsed) return;

        CardEffectResult effectResult = CardEffectManager.Instance.ResolveWeaponUseEffect(playerIndex, equippedWeaponCardId);
        if (!HandleEffectResultForPublicAction(effectResult, equippedWeaponCardId, PublicActionType.UseWeapon))
            return;
    }

    public void RequestUseEquipment(int equipmentIndex)
    {
        if (!isLocalPlayer) return;
        if (!isMyTurn) return;

        if (SelectionUI.Instance != null && SelectionUI.Instance.isSelecting)
            return;

        CmdRequestUseEquipment(equipmentIndex);
    }

    public void RequestUseStatus(string statusCardId)
    {
        if (!isLocalPlayer) return;
        if (string.IsNullOrEmpty(statusCardId)) return;
        if (!isMyTurn)
        {
            if (HintManager.Instance != null)
            {
                HintManager.Instance.ShowHint("不是你的回合");
            }

            return;
        }

        CmdRequestUseStatus(statusCardId);
    }
    [Command]
    private void CmdRequestUseEquipment(int equipmentIndex)
    {
        if (!isMyTurn) return;
        if (equipmentIndex < 0 || equipmentIndex >= equippedCardIds.Count) return;
        if (equipmentIndex < equippedCardUsedFlags.Count && equippedCardUsedFlags[equipmentIndex]) return;

        string cardId = equippedCardIds[equipmentIndex];
        CardEffectResult effectResult = CardEffectManager.Instance.ResolveEquipUseEffect(playerIndex, cardId, equipmentIndex);
        if (!HandleEffectResultForPublicAction(effectResult, cardId, PublicActionType.UseEquipment))
            return;
    }

    [Command]
    private void CmdRequestUseStatus(string statusCardId)
    {
        if (!isMyTurn) return;
        if (string.IsNullOrEmpty(statusCardId)) return;
        if (StatusEffectManager.Instance == null) return;

        if (StatusEffectManager.Instance.TryActivateStatusButton(this, statusCardId, out string failureHint))
            return;

        if (!string.IsNullOrEmpty(failureHint))
        {
            ShowHintToOwner(failureHint);
        }
    }
    #endregion

    #region 选择流程
    // 服务器会在拥有该玩家的客户端打开选择界面，并等待基于负载的响应。
    [Server]
    public void BeginSelection(
    PendingSelectionType selectionType,
    string title,
    int minCount,
    int maxCount,
    List<string> optionCardIds,
    List<int> optionPayloads,
    List<bool> optionInteractables = null)
    {
        pendingSelectionType = selectionType;
        pendingMinSelectCount = minCount;
        pendingMaxSelectCount = maxCount;

        pendingSelectionPayloads.Clear();
        pendingSelectionPayloads.AddRange(optionPayloads);

        TargetOpenSelection(
            connectionToClient,
            title,
            minCount,
            maxCount,
            optionCardIds.ToArray(),
            optionPayloads.ToArray(),
            optionInteractables != null ? optionInteractables.ToArray() : null
        );
    }

    [Server]
    public void BeginPlayerSelection(
    PendingSelectionType selectionType,
    string title,
    int minCount,
    int maxCount,
    List<int> optionPlayerIndices,
    List<int> optionPayloads)
    {
        pendingSelectionType = selectionType;
        pendingMinSelectCount = minCount;
        pendingMaxSelectCount = maxCount;

        pendingSelectionPayloads.Clear();
        pendingSelectionPayloads.AddRange(optionPayloads);

        TargetOpenPlayerSelection(
            connectionToClient,
            title,
            minCount,
            maxCount,
            optionPlayerIndices.ToArray(),
            optionPayloads.ToArray()
        );
    }

    [TargetRpc]
    private void TargetOpenSelection(
    NetworkConnectionToClient target,
    string title,
    int minCount,
    int maxCount,
    string[] optionCardIds,
    int[] optionPayloads,
    bool[] optionInteractables)
    {
        if (SelectionUI.Instance == null || CardDatabase.Instance == null)
            return;

        localSelectionPayloads.Clear();
        List<Sprite> optionSprites = new List<Sprite>();
        List<bool> filteredInteractables = new List<bool>();

        for (int i = 0; i < optionCardIds.Length; i++)
        {
            string cardId = optionCardIds[i];
            if (string.IsNullOrEmpty(cardId))
                continue;
            if (i < 0 || i >= optionPayloads.Length)
                continue;

            CardData cardData = CardDatabase.Instance.GetCardById(cardId);
            if (cardData == null || cardData.cardSprite == null)
                continue;

            optionSprites.Add(cardData.cardSprite);
            localSelectionPayloads.Add(optionPayloads[i]);
            filteredInteractables.Add(optionInteractables == null || i >= optionInteractables.Length || optionInteractables[i]);
        }

        if (optionSprites.Count == 0)
            return;

        SelectionUI.Instance.ShowSelection(
            title,
            optionSprites,
            minCount,
            maxCount,
            filteredInteractables,
            OnSelectionConfirmed
        );
    }

    [TargetRpc]
    private void TargetOpenPlayerSelection(
    NetworkConnectionToClient target,
    string title,
    int minCount,
    int maxCount,
    int[] optionPlayerIndices,
    int[] optionPayloads)
    {
        if (SelectionUI.Instance == null || MatchManager.Instance == null)
            return;

        localSelectionPayloads.Clear();
        List<PlayerState> optionPlayers = new List<PlayerState>();

        for (int i = 0; i < optionPlayerIndices.Length; i++)
        {
            if (i < 0 || i >= optionPayloads.Length)
                continue;
            if (!TryGetClientPlayerByIndex(optionPlayerIndices[i], out PlayerState optionPlayer))
                continue;

            optionPlayers.Add(optionPlayer);
            localSelectionPayloads.Add(optionPayloads[i]);
        }

        if (optionPlayers.Count == 0)
            return;

        SelectionUI.Instance.ShowPlayerSelection(
            title,
            optionPlayers,
            minCount,
            maxCount,
            OnSelectionConfirmed
        );
    }

    private void OnSelectionConfirmed(List<int> selectedIndexes)
    {
        if (selectedIndexes == null)
            return;

        List<int> selectedPayloads = new List<int>();

        for (int i = 0; i < selectedIndexes.Count; i++)
        {
            int optionIndex = selectedIndexes[i];

            if (optionIndex < 0 || optionIndex >= localSelectionPayloads.Count)
                return;

            selectedPayloads.Add(localSelectionPayloads[optionIndex]);
        }

        CmdSubmitSelection(selectedPayloads.ToArray());
    }

    private bool TryGetClientPlayerByIndex(int targetPlayerIndex, out PlayerState player)
    {
        player = null;

        if (MatchManager.Instance == null)
            return false;

        for (int i = 0; i < MatchManager.Instance.playerList.Count; i++)
        {
            PlayerState candidate = MatchManager.Instance.playerList[i];
            if (candidate == null)
                continue;
            if (candidate.playerIndex != targetPlayerIndex)
                continue;

            player = candidate;
            return true;
        }

        return false;
    }

    // 服务器会根据开启选择时记录的映射来解析玩家选中的负载。
    [Command]
    private void CmdSubmitSelection(int[] selectedPayloads)
    {
        if (pendingSelectionType == PendingSelectionType.None)
            return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.gameStarted) return;
        if (MatchManager.Instance.GetCurrentPlayer() != this) return;
        if (!isMyTurn) return;
        if (selectedPayloads == null)
            return;
        if (selectedPayloads.Length < pendingMinSelectCount || selectedPayloads.Length > pendingMaxSelectCount)
            return;

        bool selectionResolvedSuccessfully = false;
        bool selectionContinues = false;
        // 某些卡牌会在选择完成后追加第二段公共演出，例如术士的放逐动画。
        PresentationEvent? followUpPresentationEvent = null;

        switch (pendingSelectionType)
        {
            case PendingSelectionType.WizardDiscardOneCenterCard:
                {
                    int slotIndex = selectedPayloads[0];

                    if (ShopState.Instance == null)
                        return;

                    if (slotIndex < 0 || slotIndex >= ShopState.Instance.centerCardIds.Count)
                        return;

                    string removedCardId = ShopState.Instance.centerCardIds[slotIndex];
                    if (string.IsNullOrEmpty(removedCardId))
                        return;

                    TargetPlayShopExileFx(connectionToClient, slotIndex, removedCardId, false);
                    ShopState.Instance.DiscardCenterCard(slotIndex);

                    if (!isWizard)
                    {
                        DrawCards(1);
                        isWizard = true;
                    }

                    followUpPresentationEvent = PresentationEvent.CreateRemoveCards(
                        playerIndex,
                        PresentationStyle.FireDissolve,
                        WrapCardId(pendingPublicCardId),
                        new[] { removedCardId },
                        $"{playerName} triggered a banish presentation for {removedCardId}"
                    );

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.BanishOneHandCard:
                {
                    int handIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(handIndex))
                        return;

                    CardEffectResult banishEffectResult = BanishHandCardByIndex(handIndex, out string banishedCardId);
                    if (banishEffectResult == CardEffectResult.Failed)
                        return;

                    followUpPresentationEvent = PresentationEvent.CreateRemoveCards(
                        playerIndex,
                        PresentationStyle.FireDissolve,
                        WrapCardId(pendingPublicCardId),
                        new[] { banishedCardId },
                        $"{playerName} triggered a banish presentation for {banishedCardId}"
                    );

                    if (banishEffectResult == CardEffectResult.Pending)
                    {
                        if (MatchManager.Instance != null)
                        {
                            BroadcastPresentationEvent(followUpPresentationEvent.Value);
                        }

                        selectionContinues = true;
                        break;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.BanishOneDiscardPileCard:
                {
                    int discardIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(discardIndex))
                        return;

                    CardEffectResult banishEffectResult = BanishDiscardPileCardByIndex(discardIndex, out string banishedCardId);
                    if (banishEffectResult == CardEffectResult.Failed)
                        return;

                    followUpPresentationEvent = PresentationEvent.CreateRemoveCards(
                        playerIndex,
                        PresentationStyle.FireDissolve,
                        WrapCardId(pendingPublicCardId),
                        new[] { banishedCardId },
                        $"{playerName} triggered a banish presentation for {banishedCardId}"
                    );

                    if (banishEffectResult == CardEffectResult.Pending)
                    {
                        if (MatchManager.Instance != null)
                        {
                            BroadcastPresentationEvent(followUpPresentationEvent.Value);
                        }

                        selectionContinues = true;
                        break;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.BanishTwoDiscardPileCards:
                {
                    List<int> discardIndices = new List<int>();

                    for (int i = 0; i < selectedPayloads.Length; i++)
                    {
                        int discardIndex = selectedPayloads[i];
                        if (!pendingSelectionPayloads.Contains(discardIndex))
                            return;
                        if (discardIndices.Contains(discardIndex))
                            return;

                        discardIndices.Add(discardIndex);
                    }

                    discardIndices.Sort();

                    List<string> banishedCardIds = new List<string>();
                    bool nestedSelectionStarted = false;
                    for (int i = discardIndices.Count - 1; i >= 0; i--)
                    {
                        int discardIndex = discardIndices[i];
                        CardEffectResult banishEffectResult = BanishDiscardPileCardByIndex(discardIndex, out string banishedCardId);
                        if (banishEffectResult == CardEffectResult.Failed)
                            return;
                        if (banishEffectResult == CardEffectResult.Pending)
                            nestedSelectionStarted = true;

                        banishedCardIds.Add(banishedCardId);
                    }

                    banishedCardIds.Reverse();
                    followUpPresentationEvent = PresentationEvent.CreateRemoveCards(
                        playerIndex,
                        PresentationStyle.FireDissolve,
                        WrapCardId(pendingPublicCardId),
                        banishedCardIds.ToArray(),
                        $"{playerName} triggered a banish presentation for {banishedCardIds.Count} cards"
                    );

                    if (nestedSelectionStarted)
                    {
                        if (MatchManager.Instance != null)
                        {
                            BroadcastPresentationEvent(followUpPresentationEvent.Value);
                        }

                        selectionContinues = true;
                        break;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.BanishUpToThreeDiscardPileCards:
                {
                    List<int> discardIndices = new List<int>();

                    for (int i = 0; i < selectedPayloads.Length; i++)
                    {
                        int discardIndex = selectedPayloads[i];
                        if (!pendingSelectionPayloads.Contains(discardIndex))
                            return;
                        if (discardIndices.Contains(discardIndex))
                            return;

                        discardIndices.Add(discardIndex);
                    }

                    if (discardIndices.Count == 0)
                    {
                        selectionResolvedSuccessfully = true;
                        break;
                    }

                    discardIndices.Sort();

                    List<string> banishedCardIds = new List<string>();
                    bool nestedSelectionStarted = false;
                    for (int i = discardIndices.Count - 1; i >= 0; i--)
                    {
                        int discardIndex = discardIndices[i];
                        CardEffectResult banishEffectResult = BanishDiscardPileCardByIndex(discardIndex, out string banishedCardId);
                        if (banishEffectResult == CardEffectResult.Failed)
                            return;
                        if (banishEffectResult == CardEffectResult.Pending)
                            nestedSelectionStarted = true;

                        banishedCardIds.Add(banishedCardId);
                    }

                    banishedCardIds.Reverse();
                    followUpPresentationEvent = PresentationEvent.CreateRemoveCards(
                        playerIndex,
                        PresentationStyle.FireDissolve,
                        WrapCardId(pendingPublicCardId),
                        banishedCardIds.ToArray(),
                        $"{playerName} triggered a banish presentation for {banishedCardIds.Count} cards"
                    );

                    if (nestedSelectionStarted)
                    {
                        if (MatchManager.Instance != null)
                        {
                            BroadcastPresentationEvent(followUpPresentationEvent.Value);
                        }

                        selectionContinues = true;
                        break;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.AgorBanishOneHandCard:
                {
                    int handIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(handIndex))
                        return;

                    CardEffectResult banishEffectResult = BanishHandCardByIndex(handIndex, out string banishedCardId);
                    if (banishEffectResult == CardEffectResult.Failed)
                        return;

                    if (CardDatabase.Instance == null)
                        return;

                    CardData banishedCardData = CardDatabase.Instance.GetCardById(banishedCardId);
                    if (banishedCardData != null)
                    {
                        if (banishedCardData.cardCategory == CardCategory.Basic)
                        {
                            AddAttack(2);
                        }
                        else if (banishedCardData.cardCategory == CardCategory.Agor)
                        {
                            AddAttack(4);
                        }
                    }

                    followUpPresentationEvent = PresentationEvent.CreateRemoveCards(
                        playerIndex,
                        PresentationStyle.FireDissolve,
                        WrapCardId(pendingPublicCardId),
                        new[] { banishedCardId },
                        $"{playerName} triggered a banish presentation for {banishedCardId}"
                    );

                    if (banishEffectResult == CardEffectResult.Pending)
                    {
                        if (MatchManager.Instance != null)
                        {
                            BroadcastPresentationEvent(followUpPresentationEvent.Value);
                        }

                        selectionContinues = true;
                        break;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.AgorTransformOneCenterCardToEnemy:
                {
                    int slotIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(slotIndex))
                        return;
                    if (CardEffectManager.Instance == null || ShopState.Instance == null)
                        return;
                    if (!CardEffectManager.Instance.TryGetRandomEnemyCardIdUnweighted(out string enemyCardId))
                        return;
                    if (!ShopState.Instance.ReplaceCenterCard(slotIndex, enemyCardId, true))
                        return;

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.LateranoChooseManaOrAttack:
                {
                    int choiceIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(choiceIndex))
                        return;

                    switch (choiceIndex)
                    {
                        case 0:
                            if (CardEffectManager.Instance != null)
                            {
                                CardEffectManager.Instance.ApplyLateranoChoiceEffect(this, "30005", choiceIndex);
                            }
                            else
                            {
                                AddMana(2);
                            }
                            break;

                        case 1:
                            if (CardEffectManager.Instance != null)
                            {
                                CardEffectManager.Instance.ApplyLateranoChoiceEffect(this, "30005", choiceIndex);
                            }
                            else
                            {
                                AddAttack(2);
                            }
                            break;

                        default:
                            return;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.LateranoMoveTwoDiscardCardsToHand:
                {
                    List<int> discardIndices = new List<int>();

                    for (int i = 0; i < selectedPayloads.Length; i++)
                    {
                        int discardIndex = selectedPayloads[i];
                        if (!pendingSelectionPayloads.Contains(discardIndex))
                            return;
                        if (discardIndices.Contains(discardIndex))
                            return;

                        discardIndices.Add(discardIndex);
                    }

                    discardIndices.Sort();

                    bool nestedSelectionStarted = false;
                    for (int i = discardIndices.Count - 1; i >= 0; i--)
                    {
                        int discardIndex = discardIndices[i];
                        CardEffectResult moveResult = MoveDiscardCardToHandByIndex(discardIndex, out _);
                        if (moveResult == CardEffectResult.Failed)
                            return;
                        if (moveResult == CardEffectResult.Pending)
                            nestedSelectionStarted = true;
                    }

                    if (nestedSelectionStarted)
                    {
                        selectionContinues = true;
                        break;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.ReplaceOneEquippedCard:
                {
                    int equipmentIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(equipmentIndex))
                        return;
                    if (!TryResolvePendingEquipReplacementHandIndex(out int handIndex))
                        return;

                    string incomingCardId = pendingEquipReplacementCardId;
                    if (string.IsNullOrEmpty(incomingCardId))
                        return;
                    if (!DiscardEquippedCardByIndex(equipmentIndex, out _))
                        return;

                    CardEffectResult effectResult = EquipCardFromHandInternal(incomingCardId, handIndex);
                    if (effectResult == CardEffectResult.Failed)
                        return;

                    ResetPendingEquipReplacementContext();

                    if (!HandleEffectResultForPublicAction(effectResult, incomingCardId, PublicActionType.EquipCard))
                        return;

                    if (effectResult == CardEffectResult.Pending)
                    {
                        selectionContinues = true;
                        break;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.RhinePreviewTopThree:
                {
                    int selectedDrawIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(selectedDrawIndex))
                        return;
                    if (CardDatabase.Instance == null)
                        return;

                    List<int> previewIndices = new List<int>();
                    List<string> previewCardIds = new List<string>();

                    for (int i = 0; i < pendingSelectionPayloads.Count; i++)
                    {
                        int drawIndex = pendingSelectionPayloads[i];
                        if (drawIndex < 0 || drawIndex >= drawPile.Count)
                            return;

                        string previewCardId = drawPile[drawIndex];
                        if (string.IsNullOrEmpty(previewCardId))
                            return;

                        previewIndices.Add(drawIndex);
                        previewCardIds.Add(previewCardId);
                    }

                    for (int i = previewIndices.Count - 1; i >= 0; i--)
                    {
                        int drawIndex = previewIndices[i];
                        if (drawIndex < 0 || drawIndex >= drawPile.Count)
                            return;

                        drawPile.RemoveAt(drawIndex);
                    }

                    for (int i = 0; i < previewCardIds.Count; i++)
                    {
                        int originalDrawIndex = previewIndices[i];
                        string previewCardId = previewCardIds[i];

                        bool shouldEnterHand = originalDrawIndex == selectedDrawIndex;
                        if (!shouldEnterHand)
                        {
                            CardData previewCardData = CardDatabase.Instance.GetCardById(previewCardId);
                            shouldEnterHand = previewCardData != null && previewCardData.cardCategory == CardCategory.Rhine;
                        }

                        if (shouldEnterHand)
                        {
                            CardEffectResult enterResult = EnterHandCard(previewCardId, false, false);
                            if (enterResult == CardEffectResult.Failed)
                                return;
                        }
                        else
                        {
                            discardPile.Add(previewCardId);
                        }
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.UpgradeOneBasicHandCard:
                {
                    int handIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(handIndex))
                        return;
                    if (CardEffectManager.Instance == null)
                        return;
                    if (handIndex < 0 || handIndex >= handCardIds.Count)
                        return;

                    string sourceCardId = handCardIds[handIndex];
                    if (!CardEffectManager.Instance.TryGetRandomBasicUpgradeCardId(sourceCardId, out string upgradedCardId))
                        return;
                    if (!TransformHandCardByIndex(handIndex, upgradedCardId, out _))
                        return;

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.TransformOneHandCardSameCost:
                {
                    int handIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(handIndex))
                        return;
                    if (CardEffectManager.Instance == null || CardDatabase.Instance == null)
                        return;
                    if (handIndex < 0 || handIndex >= handCardIds.Count)
                        return;

                    string sourceCardId = handCardIds[handIndex];
                    CardData sourceCardData = CardDatabase.Instance.GetCardById(sourceCardId);
                    if (sourceCardData == null)
                        return;
                    if (!CardEffectManager.Instance.TryGetRandomTransformCardId(sourceCardId, sourceCardData.cost, false, out string transformedCardId))
                        return;
                    if (!TransformHandCardByIndex(handIndex, transformedCardId, out _))
                        return;

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.TransformOneHandCardByMana:
                {
                    int handIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(handIndex))
                        return;
                    if (CardEffectManager.Instance == null || CardDatabase.Instance == null)
                        return;
                    if (handIndex < 0 || handIndex >= handCardIds.Count)
                        return;

                    string sourceCardId = handCardIds[handIndex];
                    CardData sourceCardData = CardDatabase.Instance.GetCardById(sourceCardId);
                    if (sourceCardData == null)
                        return;

                    int manaToSpend = Mathf.Max(0, mana);
                    if (!CardEffectManager.Instance.TryGetRandomTransformCardId(sourceCardId, sourceCardData.cost + manaToSpend, true, out string transformedCardId))
                        return;
                    if (!SpendMana(manaToSpend))
                        return;
                    if (!TransformHandCardByIndex(handIndex, transformedCardId, out _))
                        return;

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.DorothyChooseOnePlayer:
                {
                    int targetPlayerIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(targetPlayerIndex))
                        return;
                    if (CardEffectManager.Instance == null)
                        return;
                    if (!TryGetClientPlayerByIndex(targetPlayerIndex, out PlayerState targetPlayer))
                        return;
                    if (!CardEffectManager.Instance.TryAddDerivedCardsToPlayerDeck(targetPlayer, pendingPublicCardId, 2))
                        return;

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.IfritChooseOnePlayer:
                {
                    int targetPlayerIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(targetPlayerIndex))
                        return;
                    if (!TryGetClientPlayerByIndex(targetPlayerIndex, out PlayerState targetPlayer))
                        return;
                    if (CardDatabase.Instance == null)
                        return;

                    pendingTargetPlayerIndex = targetPlayerIndex;

                    CardData cardBackData = null;
                    for (int i = 0; i < CardDatabase.Instance.allCards.Count; i++)
                    {
                        CardData candidate = CardDatabase.Instance.allCards[i];
                        if (candidate != null && candidate.cardId == "99999")
                        {
                            cardBackData = candidate;
                            break;
                        }
                    }
                    if (cardBackData == null || cardBackData.cardSprite == null)
                        return;

                    List<string> optionCardIds = new List<string>();
                    List<int> optionPayloads = new List<int>();

                    for (int i = 0; i < targetPlayer.handCardIds.Count; i++)
                    {
                        string handCardId = targetPlayer.handCardIds[i];
                        if (string.IsNullOrEmpty(handCardId))
                            continue;

                        optionCardIds.Add(cardBackData.cardId);
                        optionPayloads.Add(i);
                    }

                    if (optionCardIds.Count == 0)
                    {
                        ShowHintToOwner("目标没有手牌可查看");
                        selectionResolvedSuccessfully = true;
                        break;
                    }

                    int blindPickCount = Mathf.Min(2, optionCardIds.Count);

                    BeginSelection(
                        PendingSelectionType.IfritInspectTargetHandCards,
                        "选择至多2张手牌查看",
                        1,
                        blindPickCount,
                        optionCardIds,
                        optionPayloads
                    );

                    selectionContinues = true;
                    break;
                }

            case PendingSelectionType.IfritInspectTargetHandCards:
                {
                    if (!TryGetClientPlayerByIndex(pendingTargetPlayerIndex, out PlayerState targetPlayer))
                        return;
                    if (CardEffectManager.Instance == null || CardDatabase.Instance == null)
                        return;

                    List<string> optionCardIds = new List<string>();
                    List<int> optionPayloads = new List<int>();
                    List<bool> optionInteractables = new List<bool>();
                    bool hasDowngradeCandidate = false;

                    for (int i = 0; i < selectedPayloads.Length; i++)
                    {
                        int handIndex = selectedPayloads[i];
                        if (!pendingSelectionPayloads.Contains(handIndex))
                            return;
                        if (handIndex < 0 || handIndex >= targetPlayer.handCardIds.Count)
                            return;

                        string handCardId = targetPlayer.handCardIds[handIndex];
                        if (string.IsNullOrEmpty(handCardId))
                            continue;

                        CardData handCardData = CardDatabase.Instance.GetCardById(handCardId);
                        if (handCardData == null || handCardData.cardSprite == null)
                            continue;

                        optionCardIds.Add(handCardId);
                        optionPayloads.Add(handIndex);
                        bool canDowngrade =
                            handCardData.cardCategory == CardCategory.Basic &&
                            CardEffectManager.Instance.TryGetRandomBasicDowngradeCardId(handCardId, out _);
                        optionInteractables.Add(canDowngrade);
                        if (canDowngrade)
                        {
                            hasDowngradeCandidate = true;
                        }
                    }

                    if (optionCardIds.Count == 0)
                    {
                        selectionResolvedSuccessfully = true;
                        break;
                    }

                    BeginSelection(
                        PendingSelectionType.IfritRevealTargetHandCards,
                        "选择至多1张基础牌退化",
                        0,
                        hasDowngradeCandidate ? 1 : 0,
                        optionCardIds,
                        optionPayloads,
                        optionInteractables
                    );

                    selectionContinues = true;
                    break;
                }

            case PendingSelectionType.IfritRevealTargetHandCards:
                {
                    if (selectedPayloads.Length == 0)
                    {
                        selectionResolvedSuccessfully = true;
                        break;
                    }

                    if (!TryGetClientPlayerByIndex(pendingTargetPlayerIndex, out PlayerState revealTargetPlayer))
                        return;
                    if (CardEffectManager.Instance == null)
                        return;

                    int selectedRevealHandIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(selectedRevealHandIndex))
                        return;
                    if (selectedRevealHandIndex < 0 || selectedRevealHandIndex >= revealTargetPlayer.handCardIds.Count)
                        return;

                    string revealedSourceCardId = revealTargetPlayer.handCardIds[selectedRevealHandIndex];
                    if (!CardEffectManager.Instance.TryGetRandomBasicDowngradeCardId(revealedSourceCardId, out string revealedDowngradedCardId))
                        return;
                    if (!revealTargetPlayer.TransformHandCardByIndex(selectedRevealHandIndex, revealedDowngradedCardId, out _))
                        return;

                    selectionResolvedSuccessfully = true;
                    break;

#if false
                    if (!TryGetClientPlayerByIndex(pendingTargetPlayerIndex, out PlayerState targetPlayer))
                        return;
                    if (CardEffectManager.Instance == null || CardDatabase.Instance == null)
                        return;

                    List<string> optionCardIds = new List<string>();
                    List<int> optionPayloads = new List<int>();

                    for (int i = 0; i < pendingSelectionPayloads.Count; i++)
                    {
                        int handIndex = pendingSelectionPayloads[i];
                        if (handIndex < 0 || handIndex >= targetPlayer.handCardIds.Count)
                            return;

                        string handCardId = targetPlayer.handCardIds[handIndex];
                        if (string.IsNullOrEmpty(handCardId))
                            continue;

                        CardData handCardData = CardDatabase.Instance.GetCardById(handCardId);
                        if (handCardData == null || handCardData.cardSprite == null)
                            continue;
                        if (handCardData.cardCategory != CardCategory.Basic)
                            continue;
                        if (!CardEffectManager.Instance.TryGetRandomBasicDowngradeCardId(handCardId, out _))
                            continue;

                        optionCardIds.Add(handCardId);
                        optionPayloads.Add(handIndex);
                    }

                    if (optionCardIds.Count == 0)
                    {
                        selectionResolvedSuccessfully = true;
                        break;
                    }

                    BeginSelection(
                        PendingSelectionType.IfritDowngradeViewedBasicCard,
                        "选择至多1张基础牌退化",
                        0,
                        1,
                        optionCardIds,
                        optionPayloads
                    );

                    selectionContinues = true;
                    break;
#endif
                }

            case PendingSelectionType.IfritDowngradeViewedBasicCard:
                {
                    if (selectedPayloads.Length == 0)
                    {
                        selectionResolvedSuccessfully = true;
                        break;
                    }
                    if (!TryGetClientPlayerByIndex(pendingTargetPlayerIndex, out PlayerState targetPlayer))
                        return;
                    if (CardEffectManager.Instance == null)
                        return;

                    int handIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(handIndex))
                        return;
                    if (handIndex < 0 || handIndex >= targetPlayer.handCardIds.Count)
                        return;

                    string sourceCardId = targetPlayer.handCardIds[handIndex];
                    if (!CardEffectManager.Instance.TryGetRandomBasicDowngradeCardId(sourceCardId, out string downgradedCardId))
                        return;
                    if (!targetPlayer.TransformHandCardByIndex(handIndex, downgradedCardId, out _))
                        return;

                    selectionResolvedSuccessfully = true;
                    break;
                }

            case PendingSelectionType.StellarSourceChooseOnePlayer:
                {
                    int targetPlayerIndex = selectedPayloads[0];
                    if (!pendingSelectionPayloads.Contains(targetPlayerIndex))
                        return;
                    if (!TryGetClientPlayerByIndex(targetPlayerIndex, out PlayerState targetPlayer))
                        return;

                    targetPlayer.RebuildDrawPileFromDiscard();
                    DrawCards(1);

                    selectionResolvedSuccessfully = true;
                    break;
                }
        }

        if (selectionContinues)
            return;

        if (selectionResolvedSuccessfully && hasPendingPublicAction && MatchManager.Instance != null)
        {
            BroadcastPublicAction(pendingPublicCardId, pendingPublicActionType);
        }

        if (selectionResolvedSuccessfully && followUpPresentationEvent.HasValue && MatchManager.Instance != null)
        {
            BroadcastPresentationEvent(followUpPresentationEvent.Value);
        }

        // 服务器完成本次选择结算后，重置选择状态。
        pendingSelectionType = PendingSelectionType.None;
        pendingSelectionPayloads.Clear();
        pendingMinSelectCount = 1;
        pendingMaxSelectCount = 1;
        ResetPendingSelectionContext();
        ClearPendingPublicAction();
    }

    [Server]
    public void CancelPendingSelection()
    {
        pendingSelectionType = PendingSelectionType.None;
        pendingSelectionPayloads.Clear();
        pendingMinSelectCount = 1;
        pendingMaxSelectCount = 1;
        ResetPendingSelectionContext();
        ClearPendingPublicAction();

        if (connectionToClient != null)
        {
            TargetCancelSelection(connectionToClient);
        }
    }

    [TargetRpc]
    private void TargetCancelSelection(NetworkConnectionToClient target)
    {
        if (SelectionUI.Instance != null)
        {
            SelectionUI.Instance.CloseSelection();
        }

        localSelectionPayloads.Clear();
    }

    [Server]
    private void ResetPendingSelectionContext()
    {
        pendingTargetPlayerIndex = -1;
        ResetPendingEquipReplacementContext();
    }

    [Server]
    private void ResetPendingEquipReplacementContext()
    {
        pendingEquipReplacementCardId = "";
        pendingEquipReplacementHandIndex = -1;
    }
    #endregion
}

public enum PendingSelectionType
{
    None,                               // No pending selection
    RhinePreviewTopThree,               // Inspect top three cards and choose one
    UpgradeOneBasicHandCard,            // Upgrade one basic hand card
    TransformOneHandCardSameCost,       // Transform one hand card into a same-cost card
    TransformOneHandCardByMana,         // Transform one hand card using current mana
    DorothyChooseOnePlayer,             // Dorothy: choose one player
    IfritChooseOnePlayer,               // Ifrit: choose one player
    IfritInspectTargetHandCards,        // Ifrit: inspect target player hand cards
    IfritRevealTargetHandCards,         // Ifrit: reveal target player hand cards
    IfritDowngradeViewedBasicCard,      // Ifrit: downgrade one inspected basic card
    StellarSourceChooseOnePlayer,       // Stellar Source: choose one player
    BanishOneDiscardPileCard,           // Banish one discard pile card
    BanishOneHandCard,                  // Banish one hand card
    WizardDiscardOneCenterCard,         // Azling: banish one center card
    BanishTwoDiscardPileCards,          // Agor: banish two discard pile cards
    BanishUpToThreeDiscardPileCards,    // Agor: banish up to three discard pile cards
    AgorBanishOneHandCard,              // Agor: banish one hand card
    AgorTransformOneCenterCardToEnemy,  // Agor: transform one center card into an enemy
    LateranoChooseManaOrAttack,         // Laterano: choose +2 mana or +2 attack
    LateranoMoveTwoDiscardCardsToHand,  // Laterano: move two discard pile cards to hand
    ReplaceOneEquippedCard              // Equipment: choose one equipped field card to replace
}


