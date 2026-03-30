using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance;

    [Header("玩家列表")]
    public readonly SyncList<PlayerState> playerList = new SyncList<PlayerState>();

    [Header("开局配置")]
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

    [Header("对局状态")]
    [SyncVar] public bool gameStarted = false;
    [SyncVar] public int currentTurnPlayerIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #region 玩家注册
    [Server]
    public void RegisterPlayer(PlayerState player)
    {
        if (player == null) return;
        if (playerList.Contains(player)) return;

        playerList.Add(player);
        Debug.Log("注册玩家成功，当前玩家数：" + playerList.Count);

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
        Debug.Log("移除玩家成功，当前玩家数：" + playerList.Count);
    }
    #endregion

    #region 开局流程
    [Server]
    public void StartGame()
    {
        if (gameStarted) return;
        if (playerList.Count == 0)
        {
            Debug.Log("没有玩家，无法开始游戏");
            return;
        }
        Debug.Log("游戏开始");
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
    #endregion

    #region 回合流程
    [Server]
    public void StartCurrentTurn()
    {
        if (!gameStarted) return;
        if (playerList.Count == 0) return;
        if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= playerList.Count) return;

        PlayerState currentPlayer = playerList[currentTurnPlayerIndex];
        if (currentPlayer == null) return;

        currentPlayer.StartTurn();
        Debug.Log("开始玩家回合：" + currentPlayer.playerName);
    }

    [Server]
    public void EndCurrentTurn()
    {
        if (!gameStarted) return;
        if (playerList.Count == 0) return;
        if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= playerList.Count) return;

        PlayerState currentPlayer = playerList[currentTurnPlayerIndex];
        if (currentPlayer == null) return;

        currentPlayer.EndTurn();
        NextTurn();
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

    #region 查询辅助
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