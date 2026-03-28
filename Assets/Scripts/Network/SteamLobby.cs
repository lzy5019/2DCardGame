using Steamworks;
using UnityEngine;
using UnityEngine.UI;

public class SteamLobby : MonoBehaviour
{
    public static SteamLobby Instance;

    private MyNetworkRoomManager _roomManager;
    private const string hostAddressKey = "host";       // 房间键名

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> lobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("steam初始化失败/未连接到服务器");
            return;
        }
        Debug.Log("steam初始化成功");

        _roomManager = GetComponent<MyNetworkRoomManager>();

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if(callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.Log("steam大厅创建失败");
            return;
        }
        Debug.Log("steam大厅创建成功");

        _roomManager.StartHost();       // 房主既要当server又要当player
        SteamMatchmaking.SetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby), 
            hostAddressKey, 
            SteamUser.GetSteamID().ToString()       // 将房主的SteamID写进Lobby里
        );
    }

    private void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        string hostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), hostAddressKey);    // 拿到房主SteamID
        _roomManager.networkAddress = hostAddress;      // 告诉mirror

        if(!_roomManager.isNetworkActive)
        {
            _roomManager.StartClient();     // 开启客户端模式
            Debug.Log("正在进入房间");
        }
    }

    public void HostLobby()     // 主界面按钮
    {
        if (!SteamManager.Initialized) return;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _roomManager.maxConnections);
    }
}
