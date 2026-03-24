using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("系统引用")]
    public ShopDeckManager shopDeckManager;
    public ShopDisplay shopDisplay;
    public PlayerDeckManager playerDeckManager;
    public HandDisplayManager handDisplayManager;
    public PlayerDataManager playerDataManager;
    public TurnManager turnManager;

    [Header("初始卡组")]
    public List<string> startCards = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        Debug.Log("游戏开始初始化...");

        // 初始化玩家数据
        playerDataManager.InitializePlayers();
        playerDataManager.AddPlayer(0, "玩家A");
        playerDataManager.AddPlayer(1, "玩家B");
        playerDataManager.playerDataDisplay.RefreshDisplay();

        // 构建玩家初始卡组
        playerDeckManager.startCards = startCards;
        playerDeckManager.Initialized();
        CardEffect.Instance.DrawCards(5);
        handDisplayManager.RefreshHand();

        // 构建中场牌库
        shopDeckManager.Initialized();

        // 初始化中场显示
        shopDisplay.Initialized();

        Debug.Log("####游戏初始化完成####");
    }
}