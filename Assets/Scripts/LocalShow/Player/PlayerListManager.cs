using System.Collections.Generic;
using UnityEngine;

public class PlayerListManager : MonoBehaviour
{
    public static PlayerListManager Instance;

    [SerializeField] private Transform playerListPanel;
    [SerializeField] private GameObject playerListItemPrefab;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private readonly List<PlayerListItemUI> spawnedItemUis = new List<PlayerListItemUI>();
    private int lastPlayerCount = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

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
            if (itemUI == null)
            {
                itemUI = itemObj.GetComponentInChildren<PlayerListItemUI>(true);
            }
            if (itemUI != null)
            {
                itemUI.Bind(players[i]);
                spawnedItemUis.Add(itemUI);
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
        spawnedItemUis.Clear();
    }

    public bool TryGetPlayerNameRect(int playerIndex, out RectTransform targetRect)
    {
        targetRect = null;

        for (int i = 0; i < spawnedItemUis.Count; i++)
        {
            PlayerListItemUI itemUi = spawnedItemUis[i];
            if (itemUi == null)
                continue;
            if (itemUi.BoundPlayerIndex != playerIndex)
                continue;

            targetRect = itemUi.GetNameTargetRect();
            return targetRect != null;
        }

        return false;
    }
}
