using Mirror;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;

public class PlayerState : NetworkBehaviour
{
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
    public readonly SyncList<string> equippedCardIds = new SyncList<string>();
    public readonly SyncList<bool> equippedCardUsedFlags = new SyncList<bool>();
    [SyncVar] public string equippedWeaponCardId = "";
    [SyncVar] public bool equippedWeaponUsed = false;
    public readonly SyncList<string> playedEquipmentIds = new SyncList<string>();

    [Header("本地玩家界面")]
    [SerializeField] private GameObject playerCanvas;

    // 选择状态分为服务器端保存的负载，以及本地客户端界面展示的映射。
    [Header("待处理选择")]
    [SerializeField] private PendingSelectionType pendingSelectionType = PendingSelectionType.None;
    private readonly List<int> pendingSelectionPayloads = new List<int>();  // 服务器端等待选择时保存的权威负载。
    private readonly List<int> localSelectionPayloads = new List<int>();    // 玩家确认选择时，本地选项到负载的映射。
    private int pendingMinSelectCount = 1;
    private int pendingMaxSelectCount = 1;

    [Header("待处理公共动作")]
    private bool hasPendingPublicAction = false;
    private string pendingPublicCardId = "";
    private PublicActionType pendingPublicActionType = PublicActionType.PlayCard;

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
        equippedCardIds.Clear();
        equippedCardUsedFlags.Clear();
        equippedWeaponCardId = "";
        equippedWeaponUsed = false;
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
    }

    [Server]
    public void EndTurn()
    {
        CancelPendingSelection();
        ClearPendingPublicAction();

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

        switch (card.cardType)
        {
            case CardType.Equipment:
                if (!EquipCardFromHand(cardId, handIndex))
                    return;
                Debug.Log("Equip: " + cardId);
                break;

            case CardType.Weapon:
                if (!EquipWeaponFromHand(cardId, handIndex))
                    return;
                Debug.Log("Equip weapon: " + cardId);
                break;

            default:
                {
                    if (!PlayCardFromHand(cardId, handIndex))
                        return;

                    Debug.Log("Play: " + cardId);

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
        if (card.cardCategory == CardCategory.Monster)
        {
            if (SpendAttack(card.cost))
            {
                CardEffectResult effectResult = CardEffectManager.Instance.ResolveCardEffect(playerIndex, cardId);
                if (!HandleEffectResultForPublicAction(effectResult, cardId, PublicActionType.DefeatCenterMonster))
                    return;
            }
            else { return; }
        }
        else
        {
            if (SpendMana(card.cost))
            {
                AddCardToOwned(cardId);
                AddCardToDiscard(cardId);
                BroadcastPublicAction(cardId, PublicActionType.BuyCenterCard);
            }
            else { return; }
        }

        ShopState.Instance.RemoveCenterCard(slotIndex);
        Debug.Log(playerName + " bought center card: " + cardId + " slot: " + slotIndex);
    }
    [Command]
    private void CmdRequestBuyBaseCard(int baseIndex)
    {
        PlayerState currentPlayer = MatchManager.Instance.GetCurrentPlayer();
        if (currentPlayer != this) return;

        string cardId = ShopState.Instance.baseCardIds[baseIndex];
        CardData card = CardDatabase.Instance.GetCardById(cardId);
        if (card.cardCategory == CardCategory.Monster)
        {
            if (SpendAttack(card.cost))
            {
                currentPlayer.AddScore(1);
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
                BroadcastPublicAction(cardId, PublicActionType.BuyBaseCard);
            }
            else { return; }
        }

        Debug.Log(playerName + " bought base card: " + cardId + " slot: " + baseIndex);
    }
    
    [Server]
    public void AddCardToOwned(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        ownedCardIds.Add(cardId);
    }
    [Server]
    public void AddCardToDiscard(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        discardPile.Add(cardId);
    }
    [Server]
    public void AddCardToDrawPile(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        drawPile.Add(cardId);
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
        drawPile.RemoveAt(0);
        handCardIds.Add(cardId);

        UpdateHandCount();
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

        UpdateHandCount();
        return true;
    }

    [Server]
    private bool EquipCardFromHand(string cardId, int handIndex)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        handCardIds.RemoveAt(handIndex);

        equippedCardIds.Add(cardId);
        equippedCardUsedFlags.Add(false);
        playedEquipmentIds.Add(cardId);
        UpdateHandCount();

        CardEffectResult effectResult = CardEffectManager.Instance.ResolveEquipEnterEffect(playerIndex, cardId);
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

        UpdateHandCount();

        CardEffectResult effectResult = CardEffectManager.Instance.ResolveEquipEnterEffect(playerIndex, cardId);
        return HandleEffectResultForPublicAction(effectResult, cardId, PublicActionType.EquipWeapon);
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
    private void UpdateHandCount()
    {
        handCount = handCardIds.Count;
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
    List<int> optionPayloads)
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
    int[] optionPayloads)
    {
        if (SelectionUI.Instance == null || CardDatabase.Instance == null)
            return;

        localSelectionPayloads.Clear();
        localSelectionPayloads.AddRange(optionPayloads);

        List<Sprite> optionSprites = new List<Sprite>();

        for (int i = 0; i < optionCardIds.Length; i++)
        {
            string cardId = optionCardIds[i];
            if (string.IsNullOrEmpty(cardId))
                continue;

            CardData cardData = CardDatabase.Instance.GetCardById(cardId);
            if (cardData == null || cardData.cardSprite == null)
                continue;

            optionSprites.Add(cardData.cardSprite);
        }

        if (optionSprites.Count == 0)
            return;

        SelectionUI.Instance.ShowSelection(
            title,
            optionSprites,
            minCount,
            maxCount,
            OnSelectionConfirmed
        );
    }

    private void OnSelectionConfirmed(List<int> selectedIndexes)
    {
        if (selectedIndexes == null || selectedIndexes.Count == 0)
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
        if (selectedPayloads == null || selectedPayloads.Length == 0)
            return;

        bool selectionResolvedSuccessfully = false;

        switch (pendingSelectionType)
        {
            case PendingSelectionType.WizardDiscardOneCenterCard:
                {
                    int slotIndex = selectedPayloads[0];

                    if (ShopState.Instance == null)
                        return;

                    if (slotIndex < 0 || slotIndex >= ShopState.Instance.centerCardIds.Count)
                        return;

                    string cardId = ShopState.Instance.centerCardIds[slotIndex];
                    if (string.IsNullOrEmpty(cardId))
                        return;

                    ShopState.Instance.DiscardCenterCard(slotIndex);

                    if (!isWizard)
                    {
                        DrawCards(1);
                        isWizard = true;
                    }

                    selectionResolvedSuccessfully = true;
                    break;
                }
        }

        if (selectionResolvedSuccessfully && hasPendingPublicAction && MatchManager.Instance != null)
        {
            BroadcastPublicAction(pendingPublicCardId, pendingPublicActionType);
        }

        // 服务器完成本次选择结算后，重置选择状态。
        pendingSelectionType = PendingSelectionType.None;
        pendingSelectionPayloads.Clear();
        pendingMinSelectCount = 1;
        pendingMaxSelectCount = 1;
        ClearPendingPublicAction();
    }

    [Server]
    public void CancelPendingSelection()
    {
        pendingSelectionType = PendingSelectionType.None;
        pendingSelectionPayloads.Clear();
        pendingMinSelectCount = 1;
        pendingMaxSelectCount = 1;
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
    #endregion
}

public enum PendingSelectionType
{
    None,
    WizardDiscardOneCenterCard    // 供卡牌 00004 使用，用于选择一张中央卡牌并将其放逐。
}


