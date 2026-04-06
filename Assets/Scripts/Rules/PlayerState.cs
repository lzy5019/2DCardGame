using Mirror;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;

public class PlayerState : NetworkBehaviour
{
    [Header("玩家身份")]
    [SyncVar] public int playerIndex = -1;
    [SyncVar] public string playerName = "";
    [SyncVar] public string steamId = "";

    [Header("公开资源")]
    [SyncVar] public int score = 0;
    [SyncVar] public int mana = 0;
    [SyncVar] public int attack = 0;
    [SyncVar] public int handCount = 0;

    [Header("状态")]
    [SyncVar] public bool isReady = false;
    [SyncVar] public bool isMyTurn = false;
    [SyncVar] public bool isWizard = false;

    [Header("牌堆数据")]
    public readonly SyncList<string> drawPile = new SyncList<string>();
    public readonly SyncList<string> discardPile = new SyncList<string>();
    public readonly SyncList<string> handCardIds = new SyncList<string>();
    public readonly SyncList<string> playedCardIds = new SyncList<string>();
    public readonly SyncList<string> ownedCardIds = new SyncList<string>();
    public readonly SyncList<string> equippedCardIds = new SyncList<string>();      // 装备
    public readonly SyncList<bool> equippedCardUsedFlags = new SyncList<bool>();    // 装备是否被使用
    [SyncVar] public string equippedWeaponCardId = "";
    [SyncVar] public bool equippedWeaponUsed = false;
    public readonly SyncList<string> playedEquipmentIds = new SyncList<string>();

    [Header("本地玩家专属UI")]
    [SerializeField] private GameObject playerCanvas;

    // 回调函数用
    [Header("待处理选择")]
    [SerializeField] private PendingSelectionType pendingSelectionType = PendingSelectionType.None;
    private readonly List<int> pendingSelectionPayloads = new List<int>();  // 用于校验
    private readonly List<int> localSelectionPayloads = new List<int>();    // 映射表：玩家选择→实际效果
    private int pendingMinSelectCount = 1;
    private int pendingMaxSelectCount = 1;

    #region ·————初始化————·
    [Server]
    public void InitPlayer(int index)
    {
        playerIndex = index;
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "玩家" + (index + 1);
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
    }
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (MatchManager.Instance != null)
        {
            Debug.Log("已注册到 MatchManager: " + playerName + " netId=" + netId);
            MatchManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogError("MatchManager.Instance 是 null，注册失败");
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
    public override void OnStartLocalPlayer()       // 本机自动调用，注册
    {
        base.OnStartLocalPlayer();

        if (SteamManager.Initialized)       // 改名为steam的名字
        {
            string localSteamName = SteamFriends.GetPersonaName();
            string localSteamId = SteamUser.GetSteamID().ToString();
            this.gameObject.name = localSteamName;
            CmdSetSteamProfile(localSteamName, localSteamId);
        }

        if (HandDisplayManager.Instance != null)        // 注册手牌展示
        {
            HandDisplayManager.Instance.RegisterLocalPlayer(this);
        }

        if (PlayerEndTurn.Instance != null)             // 注册回合结束按钮
        {
            PlayerEndTurn.Instance.RegisterLocalPlayer(this);
        }

        if (ShopPanelUI.Instance != null)               // 注册商店UI
        {
            ShopPanelUI.Instance.RegisterLocalPlayer(this);
        }

        if (PileBrowserUI.Instance != null)             // 注册查询面板
        {
            PileBrowserUI.Instance.RegisterLocalPlayer(this);
        }

        if (PileCountUI.Instance != null)               // 注册数值显示
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
        // 负责保证非本地玩家UI关闭
        if (!isLocalPlayer && playerCanvas != null)
        {
            playerCanvas.SetActive(false);
        }
    }

    #region ·————回合控制————·
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
                HintManager.Instance.ShowHint("请先完成当前选择");
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
    #endregion

    #region  ·————资源修改————·
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

    #region ·————牌堆操作————·

    // 从handcardUI接收请求出牌
    #region  ·——出牌——· 
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
                HintManager.Instance.ShowHint("请先完成当前选择");
            }
            return;
        }
        
        CmdRequestPlayCard(handIndex);
    }
    [Command]
    public void CmdRequestPlayCard(int handIndex)      // 请求出牌
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
                EquipCardFromHand(cardId, handIndex);
                Debug.Log("装备：" + cardId);
                break;

            case CardType.Weapon:
                EquipWeaponFromHand(cardId, handIndex);
                Debug.Log("装备武器：" + cardId);
                break;

            default:
                PlayCardFromHand(cardId, handIndex);
                Debug.Log("打出：" + cardId);
                CardEffectManager.Instance.ResolveCardEffect(playerIndex, cardId);
                break;
        }
    }
    #endregion

    #region  ·——买牌——· 
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
                CardEffectManager.Instance.ResolveCardEffect(playerIndex ,cardId);
            }
            else { return; }
        }
        else
        {
            if (SpendMana(card.cost))
            {
                AddCardToOwned(cardId);
                AddCardToDiscard(cardId);
            }
            else { return; }
        }

        ShopState.Instance.RemoveCenterCard(slotIndex);
        Debug.Log(playerName + " 购买了中场牌：" + cardId + "  槽位：" + slotIndex);
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
            }
            else { return; }
        }
        else if (card.cardCategory == CardCategory.Basic)
        {
            if (SpendMana(card.cost))
            {
                AddCardToOwned(cardId);
                AddCardToDiscard(cardId);
            }
            else { return; }
        }

        Debug.Log(playerName + " 购买了基础牌：" + cardId + "  基础槽位：" + baseIndex);
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

    #region  ·——抽牌——·
    [Server]
    public string DrawOneCard()
    {
        if (drawPile.Count == 0)
        {
            RebuildDrawPileFromDiscard();
        }

        if (drawPile.Count == 0)
        {
            Debug.Log("玩家牌堆为空，无法抽牌");
            return "";
        }

        string cardId = drawPile[0];
        drawPile.RemoveAt(0);
        handCardIds.Add(cardId);

        UpdateHandCount();
        return cardId;
    }
    [Server]
    public void DrawCards(int amount)       // 抽牌
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
    public bool PlayCardFromHand(string cardId, int index)         // 从手牌中打出
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        handCardIds.RemoveAt(index);
        playedCardIds.Add(cardId);

        UpdateHandCount();
        return true;
    }

    [Server]
    private void EquipCardFromHand(string cardId, int handIndex)        // 装备
    {
        if (string.IsNullOrEmpty(cardId)) return;

        handCardIds.RemoveAt(handIndex);

        equippedCardIds.Add(cardId);
        equippedCardUsedFlags.Add(false);
        playedEquipmentIds.Add(cardId);
        UpdateHandCount();

        CardEffectManager.Instance.ResolveEquipEnterEffect(playerIndex, cardId);
    }

    [Server]
    private void EquipWeaponFromHand(string cardId, int handIndex)      // 武器
    {
        if (string.IsNullOrEmpty(cardId)) return;

        handCardIds.RemoveAt(handIndex);

        if (!string.IsNullOrEmpty(equippedWeaponCardId))
        {
            string oldWeaponCardId = equippedWeaponCardId;

            equippedWeaponCardId = "";
            equippedWeaponUsed = false;

            discardPile.Add(oldWeaponCardId);
            CardEffectManager.Instance.ResolveEquipLeaveToDiscardEffect(playerIndex, cardId);
        }

        equippedWeaponCardId = cardId;
        equippedWeaponUsed = false;
        playedEquipmentIds.Add(cardId);

        UpdateHandCount();

        CardEffectManager.Instance.ResolveEquipEnterEffect(playerIndex, cardId);
    }

    [Server]
    public bool RemoveCardFromHand(string cardId)       // 从手牌中放逐
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        int index = handCardIds.IndexOf(cardId);
        if (index < 0) return false;

        handCardIds.RemoveAt(index);
        UpdateHandCount();
        return true;
    }

    [Server]
    public void DiscardHandCard(string cardId)      // 从手牌中弃置
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
    #endregion

    #region ·————装备武器调用————·
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

        CardEffectManager.Instance.ResolveWeaponUseEffect(playerIndex, equippedWeaponCardId);
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
        CardEffectManager.Instance.ResolveEquipUseEffect(playerIndex, cardId, equipmentIndex);
    }
    #endregion

    #region ·————卡牌回调————·
    [Server]            // 处理选择入口，由CardEffectManager的ResolveCardEffect统一调用
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

    [TargetRpc]         // 客户端本地，由服务器点名触发
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

    private void OnSelectionConfirmed(List<int> selectedIndexes)    // 回调函数，把UI选项映射为真实选项
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

    [Command]
    private void CmdSubmitSelection(int[] selectedPayloads)         // 负责执行抉择后的效果
    {
        if (pendingSelectionType == PendingSelectionType.None)
            return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.gameStarted) return;
        if (MatchManager.Instance.GetCurrentPlayer() != this) return;
        if (!isMyTurn) return;
        if (selectedPayloads == null || selectedPayloads.Length == 0)
            return;

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

                    break;
                }
        }

        pendingSelectionType = PendingSelectionType.None;
        pendingSelectionPayloads.Clear();
        pendingMinSelectCount = 1;
        pendingMaxSelectCount = 1;
    }

    [Server]
    public void CancelPendingSelection()            // 回合结束自动取消选择事件
    {
        pendingSelectionType = PendingSelectionType.None;
        pendingSelectionPayloads.Clear();
        pendingMinSelectCount = 1;
        pendingMaxSelectCount = 1;

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
    WizardDiscardOneCenterCard    // 00004术士
}