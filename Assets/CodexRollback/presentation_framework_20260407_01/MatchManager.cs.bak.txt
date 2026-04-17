using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance;

    #region 玩家
    [Header("玩家")]
    public readonly SyncList<PlayerState> playerList = new SyncList<PlayerState>();
    #endregion

    #region 起始卡组
    [Header("起始卡组")]
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
    #endregion

    #region 对局状态
    [Header("对局状态")]
    [SyncVar] public bool gameStarted = false;
    [SyncVar] public int currentTurnPlayerIndex = -1;
    [SyncVar] public bool waitingForPublicActionDrain = false;

    [Header("回合计时")]
    [SerializeField] private float turnDurationSeconds = 60f;
    [SyncVar] public double currentTurnEndTime;
    [SyncVar] private bool turnTimerRunning;

    private bool isEndingTurn;
    private int currentDrainWaitId = 0;
    private readonly HashSet<uint> drainedPlayerNetIds = new HashSet<uint>();
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

    [ServerCallback]
    private void Update()
    {
        if (!gameStarted) return;
        if (!turnTimerRunning) return;
        if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= playerList.Count) return;

        if (NetworkTime.time >= currentTurnEndTime)
        {
            EndCurrentTurn();
        }
    }
    #endregion

    #region 玩家注册
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
    #endregion

    #region 对局流程
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

        for (int i = 0; i < playerList.Count; i++)
        {
            PlayerState player = playerList[i];
            if (player == null) continue;

            player.InitPlayer(i);
            player.BuildStartDeck(startCards);
            player.DrawCards(5);
        }

        currentTurnPlayerIndex = 0;
        StartCurrentTurn();
    }

    [Server]
    public void StartCurrentTurn()
    {
        if (!gameStarted) return;
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
        }

        StartCurrentTurn();
    }
    #endregion

    #region 公共动作队列
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

        int validPlayerCount = 0;
        for (int i = 0; i < playerList.Count; i++)
        {
            if (playerList[i] != null)
            {
                validPlayerCount++;
            }
        }

        if (drainedPlayerNetIds.Count >= validPlayerCount)
        {
            waitingForPublicActionDrain = false;
            drainedPlayerNetIds.Clear();
            NextTurn();
        }
    }

    [Server]
    public void BroadcastPublicAction(int actorPlayerIndex, string cardId, PublicActionType actionType)
    {
        RpcBroadcastPublicAction(actorPlayerIndex, cardId, (int)actionType);
    }

    [ClientRpc]
    private void RpcBroadcastPublicAction(int actorPlayerIndex, string cardId, int actionTypeValue)
    {
        if (PublicActionQueueUI.Instance == null)
            return;

        PublicActionQueueUI.Instance.Enqueue(
            new PublicActionEvent(
                actorPlayerIndex,
                cardId,
                (PublicActionType)actionTypeValue
            )
        );
    }
    #endregion

    #region 查询接口
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
    #endregion
}
