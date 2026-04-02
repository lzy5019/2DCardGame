using Mirror;
using Steamworks;

public class MyNetworkRoomPlayer : NetworkRoomPlayer
{
    [SyncVar] public string roomPlayerName = "";

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (SteamManager.Initialized)
        {
            CmdSetRoomPlayerName(SteamFriends.GetPersonaName());
        }
    }

    [Command]
    private void CmdSetRoomPlayerName(string newName)
    {
        roomPlayerName = newName;
    }
}
