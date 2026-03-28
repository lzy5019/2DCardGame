using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Steamworks;
using TMPro;

public class Player : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;

    [SyncVar(hook = nameof(OnNameChanged))]
    private string playerName;

    private void Start()
    {
        if (isLocalPlayer) 
        {
            // 获取steam用户名并且同步到服务器
            string steamName = SteamFriends.GetPersonaName();
            if(NetworkClient.ready)
            {
                CmdSetPlayerName(steamName);
            }
        }
    }

    private void Update()
    {
        
    }

    private void OnNameChanged(string oldName, string newName)
    {
        playerNameText.text = newName;
    }

    [Command]
    private void CmdSetPlayerName(string name)
    {
        playerName = name;
    }

}
