using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{

    public void GotoLobby()
    {
        MyNetworkRoomManager.Instance.ReturnToLobby();
    }

    public void ExitLobby()
    {
        MyNetworkRoomManager.Instance.ReturnToOffline();
    }
}
