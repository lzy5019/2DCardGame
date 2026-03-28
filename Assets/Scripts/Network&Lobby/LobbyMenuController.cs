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
        if(startButton != null)
            MyNetworkRoomManager.Instance.startGameButton = startButton;
    }

    private void Start()
    {
        if (startButton != null)
            startButton.SetActive(false);
    }

    public void StartGame()
    {
        MyNetworkRoomManager.Instance.StartGame();
    }
}
