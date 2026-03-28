using Mirror;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

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

    [Header("牌堆数据")]
    public readonly SyncList<string> drawPile = new SyncList<string>();
    public readonly SyncList<string> discardPile = new SyncList<string>();
    public readonly SyncList<string> handCardIds = new SyncList<string>();
    public readonly SyncList<string> playedCardIds = new SyncList<string>();
    public readonly SyncList<string> ownedCardIds = new SyncList<string>();

    [Header("本地玩家专属UI")]
    [SerializeField] private GameObject playerCanvas;

    #region 初始化
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
    public override void OnStartLocalPlayer()       // 本机自动调用，同步名字和ID
    {
        base.OnStartLocalPlayer();

        if (SteamManager.Initialized)
        {
            string localSteamName = SteamFriends.GetPersonaName();
            string localSteamId = SteamUser.GetSteamID().ToString();

            CmdSetSteamProfile(localSteamName, localSteamId);
        }
        else
        {
            Debug.LogWarning("SteamManager 尚未初始化，无法读取 Steam 名字和 SteamID");
        }
    }
    [Command]
    private void CmdSetSteamProfile(string newName, string newSteamId)
    {
        playerName = newName;
        steamId = newSteamId;
    }

    #region 监听手牌变化刷新手牌
    public override void OnStartClient()
    {
        base.OnStartClient();
        handCardIds.OnChange += OnHandCardsChanged;
    }
    public override void OnStopClient()
    {
        handCardIds.OnChange -= OnHandCardsChanged;
        base.OnStopClient();
    }
    private void OnHandCardsChanged(SyncList<string>.Operation op, int index, string item)
    {
        if (!isLocalPlayer) return;
        if (HandDisplayManager.Instance == null) return;

        HandDisplayManager.Instance.RefreshHand();
    }
    #endregion

    #endregion

    private void Start()
    {
        if (!isLocalPlayer && playerCanvas != null)
        {
            playerCanvas.SetActive(false);
        }
    }

    #region 资源修改
    [Server]
    public void AddGold(int amount)
    {
        mana += amount;
    }

    [Server]
    public void AddGem(int amount)
    {
        attack += amount;
    }

    [Server]
    public void AddScore(int amount)
    {
        score += amount;
    }

    [Server]
    public bool SpendGold(int amount)
    {
        if (mana < amount) return false;

        mana -= amount;
        return true;
    }

    [Server]
    public bool SpendGem(int amount)
    {
        if (attack < amount) return false;

        attack -= amount;
        return true;
    }
    #endregion

    #region 回合控制
    [Server]
    public void StartTurn()
    {
        isMyTurn = true;
    }

    [Server]
    public void EndTurn()
    {
        isMyTurn = false;
        mana = 0;
        attack = 0;

        for (int i = 0; i < playedCardIds.Count; i++)
        {
            discardPile.Add(playedCardIds[i]);
        }

        playedCardIds.Clear();

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

    #region 牌堆操作
    [Command]
    public void CmdPlayCard(int handIndex)      // 请求出牌
    {
        if (!isMyTurn) return;
        if (handIndex < 0 || handIndex >= handCardIds.Count) return;

        string cardId = handCardIds[handIndex];

        HandDisplayManager.Instance.RearrangeAfterPlay(handIndex);

        PlayCardFromHand(cardId, handIndex);
        Debug.Log("打出："+cardId);

        // 这里以后再补真正出牌效果
        // 比如加入弃牌堆、触发效果、加资源之类
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
            int randomIndex = Random.Range(i, drawPile.Count);

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
}