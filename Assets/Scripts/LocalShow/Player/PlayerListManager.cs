using System.Collections.Generic;
using UnityEngine;

public class PlayerListManager : MonoBehaviour
{
    [SerializeField] private Transform playerListPanel;
    [SerializeField] private GameObject playerListItemPrefab;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private int lastPlayerCount = -1;

    private void Update()
    {
        if (MatchManager.Instance == null)
            return;

        if (MatchManager.Instance.playerList.Count != lastPlayerCount)
        {
            lastPlayerCount = MatchManager.Instance.playerList.Count;
            RebuildPlayerList();
        }
    }

    private void RebuildPlayerList()
    {
        if (MatchManager.Instance == null || playerListPanel == null || playerListItemPrefab == null)
            return;

        ClearItems();

        List<PlayerState> players = new List<PlayerState>();

        for (int i = 0; i < MatchManager.Instance.playerList.Count; i++)
        {
            PlayerState player = MatchManager.Instance.playerList[i];
            if (player != null)
            {
                players.Add(player);
            }
        }

        players.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));

        for (int i = 0; i < players.Count; i++)
        {
            GameObject itemObj = Instantiate(playerListItemPrefab, playerListPanel);
            spawnedItems.Add(itemObj);

            PlayerListItemUI itemUI = itemObj.GetComponent<PlayerListItemUI>();
            if (itemUI != null)
            {
                itemUI.Bind(players[i]);
            }
        }
    }

    private void ClearItems()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
            {
                Destroy(spawnedItems[i]);
            }
        }

        spawnedItems.Clear();
    }
}
