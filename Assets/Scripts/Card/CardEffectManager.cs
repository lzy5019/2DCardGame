using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum CardEffectResult
{
    Failed,
    Applied,
    Pending
}

public class CardEffectManager : NetworkBehaviour
{
    public static CardEffectManager Instance;

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
    #endregion

    [Server]
    public HandCardDrawFxMode GetDrawFxMode(string cardId)
    {
        switch (cardId)
        {
            case "19002":
                return HandCardDrawFxMode.ShowcaseThenDisappear;

            default:
                return HandCardDrawFxMode.ToHand;
        }
    }

    #region 卡牌效果
    [Server]    // 出牌+杀怪
    public CardEffectResult ResolveCardEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out PlayerState player))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            case "00001":   // 夜刀
                player.AddMana(1);
                return CardEffectResult.Applied;

            case "00002":   // 黑角
                player.AddAttack(1);
                return CardEffectResult.Applied;

            case "00003":   // 克洛丝
                player.AddAttack(2);
                return CardEffectResult.Applied;

            case "00004":   // 芬
                player.AddMana(2);
                return CardEffectResult.Applied;

            case "00005":   // 梓兰
                {
                if (ShopState.Instance == null || CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                List<string> optionCardIds = new List<string>();
                List<int> optionPayloads = new List<int>();

                for (int i = 0; i < ShopState.Instance.centerCardIds.Count; i++)
                {
                    string centerCardId = ShopState.Instance.centerCardIds[i];
                    if (string.IsNullOrEmpty(centerCardId))
                        continue;

                    CardData centerCardData = CardDatabase.Instance.GetCardById(centerCardId);
                    if (centerCardData == null || centerCardData.cardSprite == null)
                        continue;

                    optionCardIds.Add(centerCardId);
                    optionPayloads.Add(i);
                }

                if (optionCardIds.Count == 0)
                {
                    if (!player.isWizard)
                    {
                        player.DrawCards(1);
                        player.isWizard = true;
                    }

                    return CardEffectResult.Applied;
                }

                player.BeginSelection(
                    PendingSelectionType.WizardDiscardOneCenterCard,
                    "放逐中场1张卡",
                    1,
                    1,
                    optionCardIds,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            case "00006":   // U酱
                {
                if (CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                List<string> optionCardIds = new List<string>();
                List<int> optionPayloads = new List<int>();

                for (int i = 0; i < player.handCardIds.Count; i++)
                {
                    string handCardId = player.handCardIds[i];
                    if (string.IsNullOrEmpty(handCardId))
                        continue;

                    CardData handCardData = CardDatabase.Instance.GetCardById(handCardId);
                    if (handCardData == null || handCardData.cardSprite == null)
                        continue;

                    optionCardIds.Add(handCardId);
                    optionPayloads.Add(i);
                }

                if (optionCardIds.Count == 0)
                    return CardEffectResult.Failed;

                player.BeginSelection(
                    PendingSelectionType.BanishOneHandCard,
                    "放逐1张手牌",
                    1,
                    1,
                    optionCardIds,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            // ----莱茵生命----
            case "10001":   // 缪尔赛思
                {
                if (CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                string previousCardId = player.GetPreviousPlayedCardId();
                if (string.IsNullOrEmpty(previousCardId))
                    return CardEffectResult.Applied;
                if (previousCardId == cardId)
                    return CardEffectResult.Applied;

                CardData previousCardData = CardDatabase.Instance.GetCardById(previousCardId);
                if (previousCardData == null)
                    return CardEffectResult.Applied;
                if (previousCardData.cardType != CardType.Operator && previousCardData.cardType != CardType.Basic)
                    return CardEffectResult.Applied;

                return ResolveCardEffect(playerIndex, previousCardId);
            }

            case "10002":   // 多萝西
                {
                if (MatchManager.Instance == null)
                    return CardEffectResult.Failed;

                List<int> optionPlayerIndices = new List<int>();
                List<int> optionPayloads = new List<int>();

                for (int i = 0; i < MatchManager.Instance.playerList.Count; i++)
                {
                    PlayerState targetPlayer = MatchManager.Instance.playerList[i];
                    if (targetPlayer == null)
                        continue;

                    optionPlayerIndices.Add(targetPlayer.playerIndex);
                    optionPayloads.Add(targetPlayer.playerIndex);
                }

                if (optionPlayerIndices.Count == 0)
                {
                    player.ShowHintToOwner("没有可选择的玩家");
                    return CardEffectResult.Applied;
                }

                player.BeginPlayerSelection(
                    PendingSelectionType.DorothyChooseOnePlayer,
                    "选择1名玩家",
                    1,
                    1,
                    optionPlayerIndices,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            case "10003":   // 淬羽赫默
                {
                if (CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                if (player.drawPile.Count == 0)
                {
                    player.RebuildDrawPileFromDiscard();
                }

                if (player.drawPile.Count == 0)
                {
                    player.ShowHintToOwner("抽牌堆没有可查看的卡");
                    return CardEffectResult.Applied;
                }

                List<string> optionCardIds = new List<string>();
                List<int> optionPayloads = new List<int>();
                int previewCount = Mathf.Min(3, player.drawPile.Count);

                for (int i = 0; i < previewCount; i++)
                {
                    string topCardId = player.drawPile[i];
                    if (string.IsNullOrEmpty(topCardId))
                        continue;

                    CardData topCardData = CardDatabase.Instance.GetCardById(topCardId);
                    if (topCardData == null || topCardData.cardSprite == null)
                        continue;

                    optionCardIds.Add(topCardId);
                    optionPayloads.Add(i);
                }

                if (optionCardIds.Count == 0)
                {
                    player.ShowHintToOwner("抽牌堆顶没有可展示的卡");
                    return CardEffectResult.Applied;
                }

                //查看抽牌堆顶三张牌，并选择一张加入手牌
                //剩下的卡若为莱茵生命Rhine，一并加入手牌
                //否则放入弃牌堆
                player.BeginSelection(
                    PendingSelectionType.RhinePreviewTopThree,
                    "选择1张加入手牌",
                    1,
                    1,
                    optionCardIds,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            case "10004":   // 溯光星源
                {
                if (CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                List<string> optionCardIds = new List<string>();
                List<int> optionPayloads = new List<int>();

                for (int i = 0; i < player.handCardIds.Count; i++)
                {
                    string handCardId = player.handCardIds[i];
                    if (string.IsNullOrEmpty(handCardId))
                        continue;

                    CardData handCardData = CardDatabase.Instance.GetCardById(handCardId);
                    if (handCardData == null || handCardData.cardSprite == null)
                        continue;
                    if (handCardData.cardCategory != CardCategory.Basic)
                        continue;
                    if (!TryGetRandomBasicUpgradeCardId(handCardId, out _))
                        continue;

                    optionCardIds.Add(handCardId);
                    optionPayloads.Add(i);
                }

                if (optionCardIds.Count == 0)
                {
                    player.ShowHintToOwner("没有可升级的基础牌");
                    return CardEffectResult.Applied;
                }

                player.BeginSelection(
                    PendingSelectionType.UpgradeOneBasicHandCard,
                    "选择1张基础牌升级",
                    1,
                    1,
                    optionCardIds,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            case "10005":   // 伊芙利特
                //选择一名玩家（包括自己）
                //选择查看其手牌中至多两张牌
                //若存在基本牌，则选择至多一张，随机变成低一级的基本牌
                return CardEffectResult.Applied;

            case "10006":   // 麦哲伦
                {
                if (CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                List<string> optionCardIds = new List<string>();
                List<int> optionPayloads = new List<int>();

                for (int i = 0; i < player.handCardIds.Count; i++)
                {
                    string handCardId = player.handCardIds[i];
                    if (string.IsNullOrEmpty(handCardId))
                        continue;

                    CardData handCardData = CardDatabase.Instance.GetCardById(handCardId);
                    if (handCardData == null || handCardData.cardSprite == null)
                        continue;
                    if (!HasTransformCandidate(handCardId, handCardData.cost, false))
                        continue;

                    optionCardIds.Add(handCardId);
                    optionPayloads.Add(i);
                }

                if (optionCardIds.Count == 0)
                {
                    player.ShowHintToOwner("没有可变化的手牌");
                    return CardEffectResult.Applied;
                }

                player.BeginSelection(
                    PendingSelectionType.TransformOneHandCardSameCost,
                    "选择1张手牌变化",
                    1,
                    1,
                    optionCardIds,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            case "10007":   // 娜斯提
                {
                if (CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                int manaToSpend = Mathf.Max(0, player.mana);
                List<string> optionCardIds = new List<string>();
                List<int> optionPayloads = new List<int>();

                for (int i = 0; i < player.handCardIds.Count; i++)
                {
                    string handCardId = player.handCardIds[i];
                    if (string.IsNullOrEmpty(handCardId))
                        continue;

                    CardData handCardData = CardDatabase.Instance.GetCardById(handCardId);
                    if (handCardData == null || handCardData.cardSprite == null)
                        continue;
                    if (!HasTransformCandidate(handCardId, handCardData.cost + manaToSpend, true))
                        continue;

                    optionCardIds.Add(handCardId);
                    optionPayloads.Add(i);
                }

                if (optionCardIds.Count == 0)
                {
                    player.ShowHintToOwner("没有可变化的手牌");
                    return CardEffectResult.Applied;
                }

                player.BeginSelection(
                    PendingSelectionType.TransformOneHandCardByMana,
                    "选择1张手牌升变",
                    1,
                    1,
                    optionCardIds,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            case "10008":   // 赫默
                //选择一名玩家（包括自己）
                //将其弃牌堆洗回抽牌堆，并洗牌
                //然后打出这牌的玩家抽一张牌
                return CardEffectResult.Applied;

            case "19001":   // 神经损伤
                return player.BanishPlayedCardById(cardId);

            // ----敌人部分----
            case "90001":   // 赞助无人机
                player.AddMana(2);
                player.AddScore(2);
                return CardEffectResult.Applied;

            case "90002":   // 岩壳蟹
                player.AddScore(3);
                return CardEffectResult.Applied;

            case "91001":   // 囊海爬行者
            {
                player.AddScore(4);

                if (MatchManager.Instance == null)
                    return CardEffectResult.Failed;

                for (int i = 0; i < MatchManager.Instance.playerList.Count; i++)
                {
                    PlayerState targetPlayer = MatchManager.Instance.playerList[i];
                    if (targetPlayer == null)
                        continue;

                    TryAddDerivedCardsToPlayerDeck(targetPlayer, cardId, 1);
                }

                return CardEffectResult.Applied;
            }

            case "91002":   // 枯朽吞噬者
                {
                player.AddScore(2);

                if (CardDatabase.Instance == null)
                    return CardEffectResult.Failed;

                List<string> optionCardIds = new List<string>();
                List<int> optionPayloads = new List<int>();

                for (int i = 0; i < player.discardPile.Count; i++)
                {
                    string discardCardId = player.discardPile[i];
                    if (string.IsNullOrEmpty(discardCardId))
                        continue;

                    CardData discardCardData = CardDatabase.Instance.GetCardById(discardCardId);
                    if (discardCardData == null || discardCardData.cardSprite == null)
                        continue;

                    optionCardIds.Add(discardCardId);
                    optionPayloads.Add(i);
                }

                if (optionCardIds.Count == 0)
                {
                    player.ShowHintToOwner("弃牌堆没有可放逐的卡");
                    return CardEffectResult.Applied;
                }

                player.BeginSelection(
                    PendingSelectionType.BanishOneDiscardPileCard,
                    "放逐弃牌堆1张卡",
                    1,
                    1,
                    optionCardIds,
                    optionPayloads
                );

                return CardEffectResult.Pending;
            }

            case "91003":   // 超新星术师
                player.AddScore(2);
                player.DrawCards(2);
                return CardEffectResult.Applied;

            case "91004":   // 钵海收割者
            {
                player.AddScore(5);

                TryAddDerivedCardsToPlayerDeck(player, cardId, 2);
                return CardEffectResult.Applied;
            }

            case "92001":   // 皇帝的利刃
            {
                player.AddScore(5);

                if (MatchManager.Instance == null)
                    return CardEffectResult.Failed;

                for (int i = 0; i < MatchManager.Instance.playerList.Count; i++)
                {
                    PlayerState otherPlayer = MatchManager.Instance.playerList[i];
                    if (otherPlayer == null || otherPlayer == player)
                        continue;
                    if (otherPlayer.handCardIds.Count == 0)
                        continue;

                    int randomHandIndex = Random.Range(0, otherPlayer.handCardIds.Count);
                    otherPlayer.DiscardHandCardByIndex(randomHandIndex, out _);
                }

                return CardEffectResult.Applied;
            }

            case "92002":
                player.AddScore(3);
                // 给其余所有玩家施放诅咒【流逝】
                return CardEffectResult.Pending;

            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]    // 装备场地
    public CardEffectResult ResolveEquipEnterEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out _))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]    // 进入中场
    public CardEffectResult ResolveCenterEnterEffect(int playerIndex, string cardId, int slotIndex)
    {
        TryGetPlayer(playerIndex, out PlayerState player);

        switch (cardId)
        {
            // 在这里补充“进入中场商店时触发”的卡牌效果。
            // 如需使用当前回合玩家，上面的 player 在开局补商店时可能为 null。
            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]    // 进入手牌
    public CardEffectResult ResolveHandEnterEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out PlayerState player))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            // 在这里补充“进入手牌时触发”的卡牌效果。
            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]    // 从抽牌堆进入手牌
    public CardEffectResult ResolveHandEnterFromDrawEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out PlayerState player))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            case "19002":   // 共振装置
            {
                player.AddScore(-1);

                int handIndex = player.GetLastHandCardIndex(cardId);
                if (handIndex < 0)
                    return CardEffectResult.Failed;

                CardEffectResult banishEffectResult = player.BanishHandCardByIndex(handIndex, out _);
                if (banishEffectResult == CardEffectResult.Failed)
                    return CardEffectResult.Failed;

                player.DrawCards(1);
                return CardEffectResult.Applied;
            }

            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]    // 从弃牌堆进入手牌
    public CardEffectResult ResolveHandEnterFromDiscardEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out PlayerState player))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            // 在这里补充“从弃牌堆进入手牌时触发”的卡牌效果。
            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]    // 使用场地
    public CardEffectResult ResolveEquipUseEffect(int playerIndex, string cardId, int equipmentIndex)
    {
        if (!TryGetPlayer(playerIndex, out PlayerState player))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            default:
                return CardEffectResult.Failed;
        }
    }

    [Server]    // 使用武器
    public CardEffectResult ResolveWeaponUseEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out _))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            default:
                return CardEffectResult.Failed;
        }
    }

    [Server]    // 武器离开装备区
    public CardEffectResult ResolveEquipLeaveToDiscardEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out _))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]    // 放逐牌
    public CardEffectResult ResolveBanishEnterEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out _))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            default:
                return CardEffectResult.Applied;
        }
    }
    #endregion

    #region 辅助方法
    [Server]
    private bool TryGetPlayer(int playerIndex, out PlayerState player)
    {
        player = null;

        if (playerIndex < 0)
            return false;
        if (MatchManager.Instance == null)
            return false;
        if (playerIndex >= MatchManager.Instance.playerList.Count)
            return false;

        player = MatchManager.Instance.playerList[playerIndex];
        return player != null;
    }

    [Server]
    public bool TryAddDerivedCardsToPlayerDeck(PlayerState targetPlayer, string sourceCardId, int repeatCount)
    {
        if (targetPlayer == null)
            return false;
        if (repeatCount <= 0)
            return false;
        if (CardDatabase.Instance == null)
            return false;

        CardData sourceCardData = CardDatabase.Instance.GetCardById(sourceCardId);
        if (sourceCardData == null || sourceCardData.derivedCards == null || sourceCardData.derivedCards.Count == 0)
            return false;

        bool addedAny = false;

        for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
        {
            for (int i = 0; i < sourceCardData.derivedCards.Count; i++)
            {
                CardData derivedCardData = sourceCardData.derivedCards[i];
                if (derivedCardData == null || string.IsNullOrEmpty(derivedCardData.cardId))
                    continue;

                targetPlayer.AddCardToOwned(derivedCardData.cardId);
                targetPlayer.AddCardToDiscard(derivedCardData.cardId);
                addedAny = true;
            }
        }

        return addedAny;
    }

    [Server]
    public bool TryGetRandomBasicUpgradeCardId(string sourceCardId, out string upgradedCardId)
    {
        upgradedCardId = "";

        string[] upgradePool = GetBasicUpgradePool(sourceCardId);
        if (upgradePool == null || upgradePool.Length == 0)
            return false;

        upgradedCardId = upgradePool[Random.Range(0, upgradePool.Length)];
        return !string.IsNullOrEmpty(upgradedCardId);
    }

    [Server]
    public bool HasTransformCandidate(string sourceCardId, int targetCost, bool allowLowerCostFallback)
    {
        return TryGetTransformCandidatePool(sourceCardId, targetCost, allowLowerCostFallback, out _);
    }

    [Server]
    public bool TryGetRandomTransformCardId(string sourceCardId, int targetCost, bool allowLowerCostFallback, out string transformedCardId)
    {
        transformedCardId = "";

        if (!TryGetTransformCandidatePool(sourceCardId, targetCost, allowLowerCostFallback, out List<CardData> candidatePool))
            return false;

        float totalWeight = 0f;
        for (int i = 0; i < candidatePool.Count; i++)
        {
            totalWeight += GetTransformWeight(candidatePool[i]);
        }

        if (totalWeight <= 0f)
            return false;

        float roll = Random.value * totalWeight;
        for (int i = 0; i < candidatePool.Count; i++)
        {
            CardData candidate = candidatePool[i];
            roll -= GetTransformWeight(candidate);
            if (roll <= 0f)
            {
                transformedCardId = candidate.cardId;
                return !string.IsNullOrEmpty(transformedCardId);
            }
        }

        transformedCardId = candidatePool[candidatePool.Count - 1].cardId;
        return !string.IsNullOrEmpty(transformedCardId);
    }

    private string[] GetBasicUpgradePool(string sourceCardId)
    {
        switch (sourceCardId)
        {
            case "00100":
            case "00101":
                return new[] { "00001", "00002" };

            case "00001":
            case "00002":
                return new[] { "00003", "00004" };

            case "00003":
            case "00004":
                return new[] { "00005", "00006" };

            case "00005":
            case "00006":
                return new[] { "00007" };

            case "00102":
                return new[] { "00100", "00101", "00001", "00002", "00003", "00004", "00005", "00006", "00007" };

            default:
                return System.Array.Empty<string>();
        }
    }

    private bool TryGetTransformCandidatePool(string sourceCardId, int targetCost, bool allowLowerCostFallback, out List<CardData> candidatePool)
    {
        candidatePool = new List<CardData>();

        if (CardDatabase.Instance == null)
            return false;

        int searchCost = Mathf.Max(0, targetCost);
        while (searchCost >= 0)
        {
            candidatePool.Clear();

            for (int i = 0; i < CardDatabase.Instance.allCards.Count; i++)
            {
                CardData candidate = CardDatabase.Instance.allCards[i];
                if (!IsValidTransformCandidate(candidate, sourceCardId, searchCost))
                    continue;

                candidatePool.Add(candidate);
            }

            if (candidatePool.Count > 0)
                return true;
            if (!allowLowerCostFallback)
                break;

            searchCost--;
        }

        return false;
    }

    private bool IsValidTransformCandidate(CardData candidate, string sourceCardId, int targetCost)
    {
        if (candidate == null)
            return false;
        if (string.IsNullOrEmpty(candidate.cardId))
            return false;
        if (candidate.cost != targetCost)
            return false;
        if (candidate.cardType == CardType.Enemy || candidate.cardType == CardType.Buff || candidate.cardType == CardType.Debuff)
            return false;
        if (candidate.cardCategory == CardCategory.Enemy || candidate.cardCategory == CardCategory.Buff || candidate.cardCategory == CardCategory.Debuff)
            return false;

        return true;
    }

    private float GetTransformWeight(CardData candidate)
    {
        if (candidate == null)
            return 0f;

        switch (candidate.cardType)
        {
            case CardType.Commission:
                return 0.4f;

            case CardType.Reward:
                return 0.1f;

            default:
                return 1f;
        }
    }
    #endregion
}

