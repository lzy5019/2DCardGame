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

    #region 卡牌效果
    [Server]
    public CardEffectResult ResolveCardEffect(int playerIndex, string cardId)
    {
        if (!TryGetPlayer(playerIndex, out PlayerState player))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            case "00001":
                player.AddMana(1);
                return CardEffectResult.Applied;

            case "00002":
                player.AddAttack(1);
                return CardEffectResult.Applied;

            case "00003":
                player.AddAttack(2);
                return CardEffectResult.Applied;

            case "00004":
                player.AddMana(2);
                return CardEffectResult.Applied;

            case "00005":
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

            case "01002":
                player.AddAttack(6);
                return CardEffectResult.Applied;

            case "01003":
                player.DrawCards(1);
                return CardEffectResult.Applied;

            default:
                return CardEffectResult.Applied;
        }
    }

    [Server]
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

    [Server]
    public CardEffectResult ResolveEquipUseEffect(int playerIndex, string cardId, int equipmentIndex)
    {
        if (!TryGetPlayer(playerIndex, out PlayerState player))
            return CardEffectResult.Failed;

        switch (cardId)
        {
            case "01015":
                player.AddAttack(1);
                player.equippedCardUsedFlags[equipmentIndex] = true;
                return CardEffectResult.Applied;

            default:
                return CardEffectResult.Failed;
        }
    }

    [Server]
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

    [Server]
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
    #endregion
}

