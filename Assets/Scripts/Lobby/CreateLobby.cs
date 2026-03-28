// 主要作用就是开启房间

using Steamworks;
using UnityEngine;

public class CreateLobby : MonoBehaviour
{
    public void HostLobby()     // 主界面按钮
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MyNetworkRoomManager.Instance.maxConnections);
    }
}
