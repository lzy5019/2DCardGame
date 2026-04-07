using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MyNetworkRoomManager : NetworkRoomManager
{
    public static MyNetworkRoomManager Instance;

    private const string OfflineSceneName = "MainMenu";

    #region 房间状态
    public GameObject startGameButton;
    public int gamePlayerCount = 0;
    #endregion

    #region 生命周期
    public override void Awake()
    {
        base.Awake();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    public override void Start()
    {
        base.Start();

        if (Utils.IsSceneActive(RoomScene) && startGameButton != null)
        {
            startGameButton.SetActive(true);
        }
    }

    public override void OnGUI()
    {
        if (!showRoomGUI)
            return;

        if (!Utils.IsSceneActive(RoomScene))
            return;
    }
    #endregion

    #region 房间准备状态
    public override void OnRoomServerPlayersReady()
    {
        if (Utils.IsSceneActive(RoomScene) && startGameButton != null)
        {
            startGameButton.SetActive(true);
        }
    }

    public override void OnRoomServerPlayersNotReady()
    {
        if (Utils.IsSceneActive(RoomScene) && startGameButton != null)
        {
            startGameButton.SetActive(false);
        }
    }
    #endregion

    #region 场景切换
    public void StartGame()
    {
        gamePlayerCount = roomSlots.Count;
        ServerChangeScene(GameplayScene);
    }

    public void ReturnToLobby()
    {
        if (NetworkServer.active && Utils.IsSceneActive(GameplayScene))
        {
            ServerChangeScene(RoomScene);
        }
    }

    public void ReturnToOffline()
    {
        string originalOfflineScene = offlineScene;
        offlineScene = string.Empty;

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.LeaveCurrentLobby();
        }

        if (NetworkServer.active && NetworkClient.isConnected)
        {
            StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            StopClient();
        }
        else if (NetworkServer.active)
        {
            StopServer();
        }

        offlineScene = originalOfflineScene;
        SceneManager.LoadScene(OfflineSceneName);
    }
    #endregion
}

