using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

public class MyNetworkRoomManager : NetworkRoomManager
{
    public static MyNetworkRoomManager Instance;
    private const string OfflineSceneName = "MainMenu";

    public GameObject startGameButton;

    public int gamePlayerCount = 0;


    public override void Awake()
    {
        base.Awake();
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }

    public override void Start()
    {
        base.Start();

        if(Utils.IsSceneActive(RoomScene) && startGameButton != null)
        {
            startGameButton.SetActive(true);
        }
    }

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

    public override void OnGUI()
    {
        if (!showRoomGUI)
            return;

        if (!Utils.IsSceneActive(RoomScene)) 
            return;
    }

    #region UI引用
    public void StartGame()
    {
        gamePlayerCount = roomSlots.Count;
        ServerChangeScene(GameplayScene);
    }

    public void ReturnToLobby()
    {
        if(NetworkServer.active && Utils.IsSceneActive(GameplayScene))
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
