using Steamworks;
using UnityEngine;

public class SteamLobby : MonoBehaviour
{
    public static SteamLobby Instance;

    private MyNetworkRoomManager roomManager;
    private const string HostAddressKey = "host";
    private CSteamID currentLobbyId;
    private bool hasActiveLobby = false;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> lobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("Steam init failed or Steam is unavailable.");
            return;
        }

        Debug.Log("Steam initialized.");
        roomManager = GetComponent<MyNetworkRoomManager>();

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    private bool TryEnsureRoomManager()
    {
        if (roomManager == null)
        {
            roomManager = GetComponent<MyNetworkRoomManager>();
        }

        if (roomManager == null)
        {
            Debug.LogError("MyNetworkRoomManager missing. Cannot continue Steam lobby flow.");
            return false;
        }

        return true;
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.Log("Steam lobby creation failed.");
            return;
        }

        Debug.Log("Steam lobby created.");

        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        hasActiveLobby = true;

        roomManager.StartHost();
        SteamMatchmaking.SetLobbyData(
            currentLobbyId,
            HostAddressKey,
            SteamUser.GetSteamID().ToString()
        );
    }

    private void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        hasActiveLobby = true;

        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyId, HostAddressKey);
        roomManager.networkAddress = hostAddress;

        if (!roomManager.isNetworkActive)
        {
            roomManager.StartClient();
            Debug.Log("Joining lobby.");
        }
    }

    public void HostLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Steam is not initialized. Cannot create lobby.");
            return;
        }

        if (!TryEnsureRoomManager()) return;

        if (hasActiveLobby)
        {
            LeaveCurrentLobby();
        }

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, roomManager.maxConnections);
    }

    public void LeaveCurrentLobby()
    {
        if (!SteamManager.Initialized) return;
        if (!hasActiveLobby) return;

        SteamMatchmaking.LeaveLobby(currentLobbyId);
        Debug.Log("Left Steam lobby: " + currentLobbyId);

        currentLobbyId = default;
        hasActiveLobby = false;
    }
}
