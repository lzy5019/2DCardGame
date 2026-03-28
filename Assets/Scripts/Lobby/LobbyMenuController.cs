using Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LobbyMenuController : MonoBehaviour
{
    [SerializeField] GameObject startButton;

    private void Awake()
    {
        MyNetworkRoomManager.Instance.startGameButton = startButton;
    }

    private void Start()
    {
        startButton.SetActive(false);
    }

    public void StartGame()
    {
        MyNetworkRoomManager.Instance.StartGame();
    }

    public void HostLobby()     // 主界面按钮
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MyNetworkRoomManager.Instance.maxConnections);
    }
}
