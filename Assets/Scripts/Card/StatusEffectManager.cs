using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum StatusTriggerTiming
{
    OnApply,
    OnTurnStart,
    OnTurnEnd,
    OnRemove
}

public enum StatusDurationTickTiming
{
    None,
    OnTurnStart,
    OnTurnEnd
}

public enum StatusResourceType
{
    Mana,
    Attack,
    Score
}

[System.Serializable]
public class StatusRuntimeData
{
    public string statusCardId;
    public int stackCount;
    public int remainingTurns;
    public StatusDurationTickTiming durationTickTiming;
    public int baseAttackCleanse;
    public int remainingAttackCleanse;
    public int baseManaCleanse;
    public int remainingManaCleanse;

    public StatusRuntimeData(
        string statusCardId,
        int stackCount = 1,
        int remainingTurns = -1,
        StatusDurationTickTiming durationTickTiming = StatusDurationTickTiming.None,
        int attackCleanse = 0,
        int manaCleanse = 0)
    {
        this.statusCardId = statusCardId;
        this.stackCount = Mathf.Max(1, stackCount);
        this.remainingTurns = remainingTurns;
        this.durationTickTiming = durationTickTiming;
        baseAttackCleanse = Mathf.Max(0, attackCleanse);
        remainingAttackCleanse = Mathf.Max(0, attackCleanse);
        baseManaCleanse = Mathf.Max(0, manaCleanse);
        remainingManaCleanse = Mathf.Max(0, manaCleanse);
    }
}

public class StatusEffectManager : NetworkBehaviour
{
    public static StatusEffectManager Instance;

    private readonly Dictionary<int, List<StatusRuntimeData>> runtimeStatesByPlayer = new Dictionary<int, List<StatusRuntimeData>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnStopServer()
    {
        runtimeStatesByPlayer.Clear();
        base.OnStopServer();
    }

    [Server]
    public bool ApplyStatus(PlayerState player, string statusCardId)
    {
        if (player == null)
            return false;
        if (!TryCreateRuntimeState(statusCardId, out StatusRuntimeData runtimeData))
            return false;

        List<StatusRuntimeData> runtimeStates = GetOrCreateRuntimeStates(player);
        if (TryExtendExistingDurationStatus(player, runtimeStates, runtimeData))
            return true;

        runtimeStates.Add(runtimeData);
        player.AddStatusCardId(statusCardId);
        player.AddStatusDisplayData(
            runtimeData.stackCount,
            runtimeData.remainingTurns,
            runtimeData.remainingAttackCleanse,
            runtimeData.remainingManaCleanse);
        ResolveStatusTrigger(player, runtimeData, StatusTriggerTiming.OnApply);
        SyncRuntimeDataToPlayer(player, runtimeStates.Count - 1, runtimeData);
        return true;
    }

    [Server]
    public bool RemoveStatus(PlayerState player, string statusCardId)
    {
        if (player == null || string.IsNullOrEmpty(statusCardId))
            return false;

        if (!TryGetRuntimeStates(player, out List<StatusRuntimeData> runtimeStates))
            return player.RemoveStatusCardId(statusCardId);

        for (int i = runtimeStates.Count - 1; i >= 0; i--)
        {
            if (runtimeStates[i].statusCardId != statusCardId)
                continue;

            RemoveStatusAt(player, runtimeStates, i);
            return true;
        }

        return player.RemoveStatusCardId(statusCardId);
    }

    [Server]
    public bool HasStatus(PlayerState player, string statusCardId)
    {
        if (player == null)
            return false;

        return player.HasStatusCardId(statusCardId);
    }

    [Server]
    public int GetStatusCount(PlayerState player, string statusCardId)
    {
        if (player == null)
            return 0;

        return player.GetStatusCardCount(statusCardId);
    }

    [Server]
    public bool ShouldRedirectGainedCardToHand(PlayerState player, string cardId)
    {
        if (player == null)
            return false;
        if (string.IsNullOrEmpty(cardId))
            return false;
        if (!HasStatus(player, "81003"))
            return false;

        return IsVictoriaSupportCardId(cardId);
    }

    [Server]
    public void RemoveAllStatuses(PlayerState player)
    {
        if (player == null)
            return;

        player.ClearStatusCardIds();
        ClearRuntimeStates(player);
    }

    [Server]
    public void ClearRuntimeStates(PlayerState player)
    {
        if (player == null)
            return;

        runtimeStatesByPlayer.Remove(player.playerIndex);
    }

    [Server]
    public void ResolveTurnStartStatuses(PlayerState player)
    {
        ResolveStatusesForTiming(player, StatusTriggerTiming.OnTurnStart);
    }

    [Server]
    public void ResolveTurnEndStatuses(PlayerState player)
    {
        ResolveStatusesForTiming(player, StatusTriggerTiming.OnTurnEnd);
    }

    [Server]
    public int ModifyResourceGain(PlayerState player, string sourceCardId, StatusResourceType resourceType, int baseAmount)
    {
        if (player == null)
            return baseAmount;
        if (baseAmount <= 0)
            return baseAmount;
        if (!TryGetRuntimeStates(player, out List<StatusRuntimeData> runtimeStates))
            return baseAmount;

        int modifiedAmount = baseAmount;

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            modifiedAmount = ModifyResourceGainForStatus(player, runtimeStates[i], sourceCardId, resourceType, modifiedAmount);
        }

        return Mathf.Max(0, modifiedAmount);
    }

    [Server]
    public bool TrySpendStatusAttackCleanse(PlayerState player, string statusCardId, int amount)
    {
        return TrySpendStatusCleanse(player, statusCardId, amount, true);
    }

    [Server]
    public bool TrySpendStatusManaCleanse(PlayerState player, string statusCardId, int amount)
    {
        return TrySpendStatusCleanse(player, statusCardId, amount, false);
    }

    [Server]
    public bool TryActivateStatusButton(PlayerState player, string statusCardId, out string failureHint)
    {
        failureHint = string.Empty;

        if (player == null)
            return false;
        if (string.IsNullOrEmpty(statusCardId))
            return false;
        if (!TryGetRuntimeStates(player, out List<StatusRuntimeData> runtimeStates))
            return false;

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            StatusRuntimeData runtimeData = runtimeStates[i];
            if (runtimeData == null || runtimeData.statusCardId != statusCardId)
                continue;

            int requiredAttack = Mathf.Max(0, runtimeData.remainingAttackCleanse);
            int requiredMana = Mathf.Max(0, runtimeData.remainingManaCleanse);

            if (requiredAttack <= 0 && requiredMana <= 0)
            {
                failureHint = "该状态无法手动清除";
                return false;
            }

            if (requiredAttack > 0 && player.attack < requiredAttack)
            {
                failureHint = "攻击不足";
                return false;
            }

            if (requiredMana > 0 && player.mana < requiredMana)
            {
                failureHint = "法力不足";
                return false;
            }

            if (requiredAttack > 0 && !player.SpendAttack(requiredAttack))
            {
                failureHint = "攻击不足";
                return false;
            }

            if (requiredMana > 0 && !player.SpendMana(requiredMana))
            {
                if (requiredAttack > 0)
                {
                    player.AddAttack(requiredAttack);
                }

                failureHint = "法力不足";
                return false;
            }

            if (requiredAttack > 0)
            {
                TrySpendStatusAttackCleanse(player, statusCardId, requiredAttack);
            }

            if (requiredMana > 0)
            {
                TrySpendStatusManaCleanse(player, statusCardId, requiredMana);
            }

            return true;
        }

        return false;
    }

    [Server]
    public void HandleEnemyDefeated(PlayerState player, string enemyCardId, bool fromCenterShop)
    {
        if (player == null)
            return;
        if (string.IsNullOrEmpty(enemyCardId))
            return;
        if (!TryGetRuntimeStates(player, out List<StatusRuntimeData> runtimeStates))
            return;

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            StatusRuntimeData runtimeData = runtimeStates[i];
            if (runtimeData == null)
                continue;

            switch (runtimeData.statusCardId)
            {
                case "81002":
                    if (!fromCenterShop)
                        continue;
                    if (!IsEnemyCardId(enemyCardId))
                        continue;
                    if (enemyCardId == "00000")
                        continue;

                    player.AddScore(1);
                    break;

                case "81004":
                    if (!IsEnemyCardId(enemyCardId))
                        continue;
                    if (CardEffectManager.Instance == null)
                        continue;

                    CardEffectManager.Instance.TryAddDerivedCardsToPlayerDeck(player, "81004", 1, true, true);
                    break;
            }
        }
    }

    private void ResolveStatusesForTiming(PlayerState player, StatusTriggerTiming timing)
    {
        if (player == null)
            return;
        if (!TryGetRuntimeStates(player, out List<StatusRuntimeData> runtimeStates))
            return;

        List<int> expiredIndices = null;
        for (int i = runtimeStates.Count - 1; i >= 0; i--)
        {
            StatusRuntimeData runtimeData = runtimeStates[i];
            ResolveStatusTrigger(player, runtimeData, timing);
            TickDurationIfNeeded(runtimeData, timing);

            if (ShouldExpireFromDuration(runtimeData))
            {
                if (expiredIndices == null)
                {
                    expiredIndices = new List<int>();
                }

                expiredIndices.Add(i);
                continue;
            }

            SyncRuntimeDataToPlayer(player, i, runtimeData);
        }

        if (expiredIndices == null)
            return;

        for (int i = 0; i < expiredIndices.Count; i++)
        {
            RemoveStatusAt(player, runtimeStates, expiredIndices[i]);
        }
    }

    private List<StatusRuntimeData> GetOrCreateRuntimeStates(PlayerState player)
    {
        int key = player.playerIndex;
        if (!runtimeStatesByPlayer.TryGetValue(key, out List<StatusRuntimeData> runtimeStates))
        {
            runtimeStates = new List<StatusRuntimeData>();
            runtimeStatesByPlayer.Add(key, runtimeStates);
        }

        return runtimeStates;
    }

    private bool TryGetRuntimeStates(PlayerState player, out List<StatusRuntimeData> runtimeStates)
    {
        runtimeStates = null;

        if (player == null)
            return false;

        return runtimeStatesByPlayer.TryGetValue(player.playerIndex, out runtimeStates);
    }

    private bool TryExtendExistingDurationStatus(PlayerState player, List<StatusRuntimeData> runtimeStates, StatusRuntimeData incomingRuntimeData)
    {
        if (player == null || runtimeStates == null || incomingRuntimeData == null)
            return false;
        if (incomingRuntimeData.durationTickTiming == StatusDurationTickTiming.None)
            return false;
        if (incomingRuntimeData.remainingTurns <= 0)
            return false;

        for (int i = runtimeStates.Count - 1; i >= 0; i--)
        {
            StatusRuntimeData existingRuntimeData = runtimeStates[i];
            if (existingRuntimeData == null)
                continue;
            if (existingRuntimeData.statusCardId != incomingRuntimeData.statusCardId)
                continue;
            if (existingRuntimeData.durationTickTiming != incomingRuntimeData.durationTickTiming)
                continue;

            existingRuntimeData.remainingTurns += incomingRuntimeData.remainingTurns;
            SyncRuntimeDataToPlayer(player, i, existingRuntimeData);
            return true;
        }

        return false;
    }

    private bool TryCreateRuntimeState(string statusCardId, out StatusRuntimeData runtimeData)
    {
        runtimeData = null;

        if (string.IsNullOrEmpty(statusCardId))
            return false;
        if (!IsStatusCardId(statusCardId))
            return false;

        switch (statusCardId)
        {
            case "80001":
                runtimeData = new StatusRuntimeData(
                    statusCardId,
                    stackCount: 1,
                    remainingTurns: -1,
                    durationTickTiming: StatusDurationTickTiming.None,
                    attackCleanse: 4,
                    manaCleanse: 0);
                return true;

            case "81001":
                runtimeData = new StatusRuntimeData(
                    statusCardId,
                    stackCount: 1,
                    remainingTurns: 1,
                    durationTickTiming: StatusDurationTickTiming.OnTurnEnd);
                return true;

            case "81002":
                runtimeData = new StatusRuntimeData(
                    statusCardId,
                    stackCount: 1,
                    remainingTurns: 1,
                    durationTickTiming: StatusDurationTickTiming.OnTurnEnd);
                return true;

            case "81003":
                runtimeData = new StatusRuntimeData(
                    statusCardId,
                    stackCount: 1,
                    remainingTurns: 1,
                    durationTickTiming: StatusDurationTickTiming.OnTurnEnd);
                return true;

            case "81004":
                runtimeData = new StatusRuntimeData(
                    statusCardId,
                    stackCount: 1,
                    remainingTurns: 2,
                    durationTickTiming: StatusDurationTickTiming.OnTurnEnd);
                return true;

            default:
                runtimeData = new StatusRuntimeData(statusCardId);
                return true;
        }
    }

    private void RemoveStatusAt(PlayerState player, List<StatusRuntimeData> runtimeStates, int index)
    {
        if (player == null)
            return;
        if (runtimeStates == null)
            return;
        if (index < 0 || index >= runtimeStates.Count)
            return;

        StatusRuntimeData runtimeData = runtimeStates[index];
        ResolveStatusTrigger(player, runtimeData, StatusTriggerTiming.OnRemove);
        runtimeStates.RemoveAt(index);
        player.RemoveStatusCardId(runtimeData.statusCardId);
        player.RemoveStatusDisplayDataAt(index);

        if (runtimeStates.Count == 0)
        {
            runtimeStatesByPlayer.Remove(player.playerIndex);
        }
    }

    private void TickDurationIfNeeded(StatusRuntimeData runtimeData, StatusTriggerTiming timing)
    {
        if (runtimeData == null)
            return;
        if (runtimeData.remainingTurns < 0)
            return;

        if (runtimeData.durationTickTiming == StatusDurationTickTiming.OnTurnStart && timing == StatusTriggerTiming.OnTurnStart)
        {
            runtimeData.remainingTurns = Mathf.Max(0, runtimeData.remainingTurns - 1);
        }
        else if (runtimeData.durationTickTiming == StatusDurationTickTiming.OnTurnEnd && timing == StatusTriggerTiming.OnTurnEnd)
        {
            runtimeData.remainingTurns = Mathf.Max(0, runtimeData.remainingTurns - 1);
        }
    }

    private bool ShouldExpireFromDuration(StatusRuntimeData runtimeData)
    {
        if (runtimeData == null)
            return false;
        if (runtimeData.durationTickTiming == StatusDurationTickTiming.None)
            return false;

        return runtimeData.remainingTurns == 0;
    }

    private int ModifyResourceGainForStatus(PlayerState player, StatusRuntimeData runtimeData, string sourceCardId, StatusResourceType resourceType, int currentAmount)
    {
        if (player == null || runtimeData == null)
            return currentAmount;

        switch (runtimeData.statusCardId)
        {
            case "81001":
                if (!IsSourceCardInCategory(sourceCardId, CardCategory.Laterano))
                    return currentAmount;

                return currentAmount + 1;

            default:
                return currentAmount;
        }
    }

    private bool TrySpendStatusCleanse(PlayerState player, string statusCardId, int amount, bool useAttackCleanse)
    {
        if (player == null)
            return false;
        if (string.IsNullOrEmpty(statusCardId))
            return false;
        if (amount <= 0)
            return false;
        if (!TryGetRuntimeStates(player, out List<StatusRuntimeData> runtimeStates))
            return false;

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            StatusRuntimeData runtimeData = runtimeStates[i];
            if (runtimeData.statusCardId != statusCardId)
                continue;

            if (useAttackCleanse)
            {
                if (runtimeData.remainingAttackCleanse <= 0)
                    return false;

                runtimeData.remainingAttackCleanse = Mathf.Max(0, runtimeData.remainingAttackCleanse - amount);
            }
            else
            {
                if (runtimeData.remainingManaCleanse <= 0)
                    return false;

                runtimeData.remainingManaCleanse = Mathf.Max(0, runtimeData.remainingManaCleanse - amount);
            }

            if (ShouldExpireFromCleanse(runtimeData))
            {
                RemoveStatusAt(player, runtimeStates, i);
            }
            else
            {
                SyncRuntimeDataToPlayer(player, i, runtimeData);
            }

            return true;
        }

        return false;
    }

    private bool ShouldExpireFromCleanse(StatusRuntimeData runtimeData)
    {
        if (runtimeData == null)
            return false;

        bool hasAnyCleanseRequirement = runtimeData.baseAttackCleanse > 0 || runtimeData.baseManaCleanse > 0;
        if (!hasAnyCleanseRequirement)
            return false;

        return runtimeData.remainingAttackCleanse <= 0 && runtimeData.remainingManaCleanse <= 0;
    }

    private void ResolveStatusTrigger(PlayerState player, StatusRuntimeData runtimeData, StatusTriggerTiming timing)
    {
        if (player == null || runtimeData == null)
            return;

        switch (runtimeData.statusCardId)
        {
            case "80001":
                if (timing != StatusTriggerTiming.OnTurnStart)
                    return;

                player.AddScore(-1);
                return;

            default:
                return;
        }
    }

    private bool IsStatusCardId(string statusCardId)
    {
        if (CardDatabase.Instance == null)
            return false;

        CardData cardData = CardDatabase.Instance.GetCardById(statusCardId);
        if (cardData == null)
            return false;

        return cardData.cardType == CardType.Buff || cardData.cardType == CardType.Debuff;
    }

    private void SyncRuntimeDataToPlayer(PlayerState player, int index, StatusRuntimeData runtimeData)
    {
        if (player == null || runtimeData == null)
            return;

        player.UpdateStatusDisplayDataAt(
            index,
            runtimeData.stackCount,
            runtimeData.remainingTurns,
            runtimeData.remainingAttackCleanse,
            runtimeData.remainingManaCleanse);
    }

    private bool IsSourceCardInCategory(string sourceCardId, CardCategory targetCategory)
    {
        if (string.IsNullOrEmpty(sourceCardId))
            return false;
        if (CardDatabase.Instance == null)
            return false;

        CardData sourceCardData = CardDatabase.Instance.GetCardById(sourceCardId);
        if (sourceCardData == null)
            return false;

        return sourceCardData.cardCategory == targetCategory;
    }

    private bool IsEnemyCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return false;
        if (CardDatabase.Instance == null)
            return false;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
            return false;

        return cardData.cardType == CardType.Enemy;
    }

    private bool IsVictoriaSupportCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return false;
        if (CardDatabase.Instance == null)
            return false;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
            return false;

        return cardData.cardCategory == CardCategory.Victoria &&
               cardData.cardType == CardType.Support;
    }
}
