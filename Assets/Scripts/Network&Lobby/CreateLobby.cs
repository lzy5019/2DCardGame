using UnityEngine;

public class CreateLobby : MonoBehaviour
{
    public void HostLobby()
    {
        SteamLobby steamLobby = SteamLobby.Instance;

        if (steamLobby == null)
        {
            steamLobby = FindObjectOfType<SteamLobby>();
        }

        if (steamLobby == null)
        {
            Debug.LogError("SteamLobby is not initialized. Cannot create lobby.");
            return;
        }

        steamLobby.HostLobby();
    }
}
