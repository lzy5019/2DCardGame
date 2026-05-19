using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance;

    [Header("Players")]
    public readonly SyncList<PlayerState> playerList = new SyncList<PlayerState>();

    [Header("Start Deck")]
    public List<string> startCards = new List<string>()
    {
        "00001",
        "00001",
        "00001",
        "00001",
        "00001",
        "00001",
        "00001",
        "00001",
        "00002",
        "00002"
    };

    [Header("Match State")]
    [SyncVar] public bool gameStarted = false;
    [SyncVar] public bool gameEnded = false;
    [SyncVar] public int currentTurnPlayerIndex = -1;
    [SyncVar] public bool waitingForPublicActionDrain = false;
    [SyncVar] public int turnCount = 0;

    [Header("Score Pool")]
    [SerializeField] private int scorePoolPerPlayer = 40;
    [SyncVar] public int initialTotalScorePool = 0;
    [SyncVar(hook = nameof(OnRemainingScorePoolChanged))] public int remainingScorePool = 0;

    [Header("Turn Timer")]
    [SerializeField] private float turnDurationSeconds = 60f;
    [SyncVar] public double currentTurnEndTime;
    [SyncVar] private bool turnTimerRunning;

    private bool isEndingTurn;
    private int currentDrainWaitId = 0;
    private readonly HashSet<uint> drainedPlayerNetIds = new HashSet<uint>();

    public event Action<int> RemainingScorePoolChanged;

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

    [ServerCallback]
    private void Update()
    {
        if (!gameStarted) return;
        if (gameEnded) return;
        if (!turnTimerRunning) return;
        if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= playerList.Count) return;

        if (NetworkTime.time >= currentTurnEndTime)
        {
            EndCurrentTurn();
        }
    }

    [Server]
    public void RegisterPlayer(PlayerState player)
    {
        if (player == null) return;
        if (playerList.Contains(player)) return;

        playerList.Add(player);
        Debug.Log("Player registered. Count: " + playerList.Count);

        if (playerList.Count >= MyNetworkRoomManager.Instance.gamePlayerCount)
        {
            StartGame();
        }
    }

    [Server]
    public void UnregisterPlayer(PlayerState player)
    {
        if (player == null) return;
        if (!playerList.Contains(player)) return;

        playerList.Remove(player);
        Debug.Log("Player unregistered. Count: " + playerList.Count);
    }

    [Server]
    public void StartGame()
    {
        if (gameStarted) return;
        if (playerList.Count == 0)
        {
            Debug.Log("No players, cannot start game.");
            return;
        }

        Debug.Log("Game started");
        gameStarted = true;
        gameEnded = false;

        int validPlayerCount = CountValidPlayers();
        initialTotalScorePool = Mathf.Max(0, scorePoolPerPlayer * validPlayerCount);
        remainingScorePool = initialTotalScorePool;

        for (int i = 0; i < playerList.Count; i++)
        {
            PlayerState player = playerList[i];
            if (player == null) continue;

            player.InitPlayer(i);
            player.BuildStartDeck(startCards);
            player.DrawCards(5);
        }

        currentTurnPlayerIndex = 0;
        turnCount = 1;
        StartCurrentTurn();
    }

    [Server]
    public void StartCurrentTurn()
    {
        if (!gameStarted) return;
        if (gameEnded) return;
        if (playerList.Count == 0) return;
        if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= playerList.Count) return;

        PlayerState currentPlayer = playerList[currentTurnPlayerIndex];
        if (currentPlayer == null) return;

        currentTurnEndTime = NetworkTime.time + turnDurationSeconds;
        turnTimerRunning = true;

        currentPlayer.StartTurn();
        Debug.Log("Start turn: " + currentPlayer.playerName);
    }

    [Server]
    public void EndCurrentTurn()
    {
        if (!gameStarted) return;
        if (gameEnded) return;
        if (playerList.Count == 0) return;
        if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= playerList.Count) return;

        PlayerState currentPlayer = playerList[currentTurnPlayerIndex];
        if (currentPlayer == null) return;
        if (isEndingTurn) return;

        isEndingTurn = true;
        turnTimerRunning = false;
        currentTurnEndTime = 0;

        currentPlayer.EndTurn();
        BeginWaitForPublicActionDrain();

        isEndingTurn = false;
    }

    [Server]
    private void NextTurn()
    {
        if (playerList.Count == 0) return;

        currentTurnPlayerIndex++;
        if (currentTurnPlayerIndex >= playerList.Count)
        {
            currentTurnPlayerIndex = 0;
            turnCount++;
        }

        StartCurrentTurn();
    }

    [Server]
    private void BeginWaitForPublicActionDrain()
    {
        waitingForPublicActionDrain = true;
        currentDrainWaitId++;
        drainedPlayerNetIds.Clear();

        RpcWaitForPublicActionDrain(currentDrainWaitId);
    }

    [ClientRpc]
    private void RpcWaitForPublicActionDrain(int waitId)
    {
        if (PublicActionQueueUI.Instance != null)
        {
            PublicActionQueueUI.Instance.WaitUntilIdleThenAck(waitId);
            return;
        }

        if (NetworkClient.localPlayer == null)
            return;

        PlayerState localPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
        if (localPlayer != null)
        {
            localPlayer.RequestReportPublicActionQueueDrained(waitId);
        }
    }

    [Server]
    public void ReportPublicActionQueueDrained(PlayerState player, int waitId)
    {
        if (!waitingForPublicActionDrain)
            return;
        if (waitId != currentDrainWaitId)
            return;
        if (player == null)
            return;

        drainedPlayerNetIds.Add(player.netId);

        int validPlayerCount = CountValidPlayers();
        if (drainedPlayerNetIds.Count >= validPlayerCount)
        {
            waitingForPublicActionDrain = false;
            drainedPlayerNetIds.Clear();

            if (ShouldEndGameAfterTurn())
            {
                EndGame();
                return;
            }

            NextTurn();
        }
    }

    [Server]
    public void BroadcastPublicAction(int actorPlayerIndex, string cardId, PublicActionType actionType)
    {
        BroadcastPresentationEvent(
            PresentationEvent.CreateLegacyAction(actorPlayerIndex, cardId, actionType)
        );
    }

    [Server]
    public void BroadcastPresentationEvent(PresentationEvent presentationEvent)
    {
        RpcBroadcastPresentationEvent(
            presentationEvent.actorPlayerIndex,
            (int)presentationEvent.presentationType,
            (int)presentationEvent.presentationStyle,
            presentationEvent.legacyActionTypeValue,
            presentationEvent.sourceCardIds,
            presentationEvent.beforeCardIds,
            presentationEvent.afterCardIds,
            presentationEvent.message
        );
    }

    [ClientRpc]
    private void RpcBroadcastPresentationEvent(
        int actorPlayerIndex,
        int presentationTypeValue,
        int presentationStyleValue,
        int legacyActionTypeValue,
        string[] sourceCardIds,
        string[] beforeCardIds,
        string[] afterCardIds,
        string message)
    {
        if (PublicActionQueueUI.Instance == null)
            return;

        PresentationEvent presentationEvent = new PresentationEvent(
            actorPlayerIndex,
            (PresentationType)presentationTypeValue,
            (PresentationStyle)presentationStyleValue,
            legacyActionTypeValue,
            sourceCardIds,
            beforeCardIds,
            afterCardIds,
            message
        );

        PublicActionQueueUI.Instance.Enqueue(presentationEvent);
    }

    public PlayerState GetCurrentPlayer()
    {
        if (playerList.Count == 0) return null;
        if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= playerList.Count) return null;

        return playerList[currentTurnPlayerIndex];
    }

    public PlayerState GetPlayerByIndex(int index)
    {
        if (index < 0 || index >= playerList.Count) return null;
        return playerList[index];
    }

    public int GetRemainingScorePool()
    {
        return Mathf.Max(0, remainingScorePool);
    }

    [Server]
    public int ApplyScoreChange(PlayerState player, int requestedDelta)
    {
        if (player == null)
            return 0;
        if (requestedDelta == 0)
            return 0;

        if (requestedDelta > 0)
        {
            int actualGain = Mathf.Min(requestedDelta, Mathf.Max(0, remainingScorePool));
            if (actualGain <= 0)
                return 0;

            player.score += actualGain;
            remainingScorePool = Mathf.Max(0, remainingScorePool - actualGain);
            return actualGain;
        }

        int requestedLoss = -requestedDelta;
        int actualLoss = Mathf.Min(requestedLoss, Mathf.Max(0, player.score));
        if (actualLoss <= 0)
            return 0;

        player.score -= actualLoss;
        remainingScorePool = Mathf.Min(initialTotalScorePool, remainingScorePool + actualLoss);
        return -actualLoss;
    }

    [Server]
    private bool ShouldEndGameAfterTurn()
    {
        return remainingScorePool <= 0;
    }

    [Server]
    private void EndGame()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        gameStarted = false;
        waitingForPublicActionDrain = false;
        turnTimerRunning = false;
        currentTurnEndTime = 0;
        currentTurnPlayerIndex = -1;

        FinalMatchSnapshot snapshot = BuildFinalMatchSnapshot();
        FinalResultBridge.EnsureInstance().StoreSnapshot(snapshot);

        if (NetworkManager.singleton != null)
        {
            NetworkManager.singleton.ServerChangeScene(FinalResultBridge.EnsureInstance().FinalSceneName);
            return;
        }

        Debug.Log($"Game ended. Score pool exhausted. Initial={initialTotalScorePool}, Remaining={remainingScorePool}");
    }

    private int CountValidPlayers()
    {
        int validPlayerCount = 0;
        for (int i = 0; i < playerList.Count; i++)
        {
            if (playerList[i] != null)
            {
                validPlayerCount++;
            }
        }

        return validPlayerCount;
    }

    [Server]
    private FinalMatchSnapshot BuildFinalMatchSnapshot()
    {
        FinalMatchSnapshot snapshot = new FinalMatchSnapshot
        {
            playerCount = CountValidPlayers(),
            initialScorePool = initialTotalScorePool,
            remainingScorePool = remainingScorePool
        };

        for (int i = 0; i < playerList.Count; i++)
        {
            PlayerState player = playerList[i];
            if (player == null)
                continue;

            snapshot.players.Add(BuildFinalPlayerSnapshot(player));
        }

        snapshot.players.Sort((left, right) => left.playerIndex.CompareTo(right.playerIndex));
        return snapshot;
    }

    [Server]
    private FinalPlayerSnapshot BuildFinalPlayerSnapshot(PlayerState player)
    {
        List<CardData> ownedCards = GetOwnedCardDatas(player);
        int cardValueScore = 0;
        for (int i = 0; i < ownedCards.Count; i++)
        {
            CardData cardData = ownedCards[i];
            if (cardData != null)
            {
                cardValueScore += Mathf.Max(0, cardData.scoreValue);
            }
        }

        FinalPlayerSnapshot snapshot = new FinalPlayerSnapshot
        {
            playerIndex = player.playerIndex,
            playerName = player.playerName,
            gainScore = Mathf.Max(0, player.score),
            cardValueScore = cardValueScore
        };
        snapshot.totalScore = snapshot.gainScore + snapshot.cardValueScore;
        for (int i = 0; i < player.ownedCardIds.Count; i++)
        {
            snapshot.ownedCardIds.Add(player.ownedCardIds[i]);
        }
        snapshot.keyCardIds.AddRange(SelectTopThreeKeyCardIds(ownedCards));
        return snapshot;
    }

    [Server]
    private List<CardData> GetOwnedCardDatas(PlayerState player)
    {
        List<CardData> result = new List<CardData>();
        if (player == null || CardDatabase.Instance == null)
            return result;

        for (int i = 0; i < player.ownedCardIds.Count; i++)
        {
            string cardId = player.ownedCardIds[i];
            CardData cardData = TryGetCardData(cardId);
            if (cardData != null)
            {
                result.Add(cardData);
            }
        }

        return result;
    }

    [Server]
    private List<string> SelectTopThreeKeyCardIds(List<CardData> ownedCards)
    {
        List<string> result = new List<string>(3);
        if (ownedCards == null || ownedCards.Count == 0)
        {
            result.Add("");
            result.Add("");
            result.Add("");
            return result;
        }

        System.Random rng = new System.Random();
        List<CardData> orderedCards = ownedCards
            .OrderByDescending(card => card != null ? card.cost : 0)
            .ThenBy(_ => rng.Next())
            .ToList();

        for (int i = 0; i < orderedCards.Count && result.Count < 3; i++)
        {
            CardData cardData = orderedCards[i];
            if (cardData != null && !string.IsNullOrEmpty(cardData.cardId))
            {
                result.Add(cardData.cardId);
            }
        }

        while (result.Count < 3)
        {
            result.Add(result.Count > 0 ? result[0] : "");
        }

        return result;
    }

    [Server]
    private CardData TryGetCardData(string cardId)
    {
        if (string.IsNullOrEmpty(cardId) || CardDatabase.Instance == null)
            return null;

        List<CardData> allCards = CardDatabase.Instance.allCards;
        for (int i = 0; i < allCards.Count; i++)
        {
            CardData cardData = allCards[i];
            if (cardData != null && cardData.cardId == cardId)
            {
                return cardData;
            }
        }

        return null;
    }

    private void OnRemainingScorePoolChanged(int oldValue, int newValue)
    {
        RemainingScorePoolChanged?.Invoke(Mathf.Max(0, newValue));
    }
}
