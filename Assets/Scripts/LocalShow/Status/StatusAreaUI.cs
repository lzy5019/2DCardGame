using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class StatusAreaUI : MonoBehaviour
{
    public static StatusAreaUI Instance;

    [Header("References")]
    [SerializeField] private Transform statusContentRoot;
    [SerializeField] private GameObject statusPrefab;
    [SerializeField] private GameObject emptyHintObject;

    [Header("Behavior")]
    [SerializeField] private bool autoRegisterLocalPlayer = true;

    private PlayerState localPlayer;
    private readonly List<GameObject> spawnedStatusObjects = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (!autoRegisterLocalPlayer)
            return;
        if (localPlayer != null)
            return;
        if (NetworkClient.localPlayer == null)
            return;

        PlayerState fallbackPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
        if (fallbackPlayer != null)
        {
            RegisterLocalPlayer(fallbackPlayer);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterCurrentPlayer();
    }

    public void RegisterLocalPlayer(PlayerState player)
    {
        if (player == null)
            return;
        if (localPlayer == player)
            return;

        UnregisterCurrentPlayer();

        localPlayer = player;
        localPlayer.activeStatusCardIds.Callback += OnStatusIdsChanged;
        localPlayer.activeStatusStackCounts.Callback += OnStatusIntDataChanged;
        localPlayer.activeStatusRemainingTurns.Callback += OnStatusIntDataChanged;
        localPlayer.activeStatusAttackCleanseValues.Callback += OnStatusIntDataChanged;
        localPlayer.activeStatusManaCleanseValues.Callback += OnStatusIntDataChanged;

        RefreshAll();
    }

    public void UnregisterCurrentPlayer()
    {
        if (localPlayer != null)
        {
            localPlayer.activeStatusCardIds.Callback -= OnStatusIdsChanged;
            localPlayer.activeStatusStackCounts.Callback -= OnStatusIntDataChanged;
            localPlayer.activeStatusRemainingTurns.Callback -= OnStatusIntDataChanged;
            localPlayer.activeStatusAttackCleanseValues.Callback -= OnStatusIntDataChanged;
            localPlayer.activeStatusManaCleanseValues.Callback -= OnStatusIntDataChanged;
            localPlayer = null;
        }

        ClearSpawnedStatusObjects();
        RefreshEmptyHint();
    }

    public void RequestUseStatusFromUI(string statusCardId)
    {
        if (localPlayer == null)
            return;
        if (string.IsNullOrEmpty(statusCardId))
            return;

        localPlayer.RequestUseStatus(statusCardId);
    }

    private void OnStatusIdsChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        RefreshAll();
    }

    private void OnStatusIntDataChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        ClearSpawnedStatusObjects();

        if (localPlayer == null || statusPrefab == null || ContentRoot == null || CardDatabase.Instance == null)
        {
            RefreshEmptyHint();
            return;
        }

        for (int i = 0; i < localPlayer.activeStatusCardIds.Count; i++)
        {
            string statusCardId = localPlayer.activeStatusCardIds[i];
            if (string.IsNullOrEmpty(statusCardId))
                continue;

            CardData cardData = CardDatabase.Instance.GetCardById(statusCardId);
            if (cardData == null)
                continue;

            GameObject spawnedObject = Instantiate(statusPrefab, ContentRoot);
            spawnedStatusObjects.Add(spawnedObject);

            StatusItemUI statusItemUI = spawnedObject.GetComponent<StatusItemUI>();
            if (statusItemUI == null)
                continue;

            statusItemUI.Setup(
                statusCardId,
                cardData,
                GetIntValue(localPlayer.activeStatusStackCounts, i, 1),
                GetIntValue(localPlayer.activeStatusRemainingTurns, i, -1),
                GetIntValue(localPlayer.activeStatusManaCleanseValues, i, 0),
                GetIntValue(localPlayer.activeStatusAttackCleanseValues, i, 0));
        }

        RefreshEmptyHint();
    }

    private Transform ContentRoot => statusContentRoot != null ? statusContentRoot : transform;

    private int GetIntValue(SyncList<int> syncList, int index, int fallbackValue)
    {
        if (syncList == null)
            return fallbackValue;
        if (index < 0 || index >= syncList.Count)
            return fallbackValue;

        return syncList[index];
    }

    private void RefreshEmptyHint()
    {
        if (emptyHintObject == null)
            return;

        emptyHintObject.SetActive(localPlayer == null || localPlayer.activeStatusCardIds.Count == 0);
    }

    private void ClearSpawnedStatusObjects()
    {
        for (int i = 0; i < spawnedStatusObjects.Count; i++)
        {
            if (spawnedStatusObjects[i] != null)
            {
                Destroy(spawnedStatusObjects[i]);
            }
        }

        spawnedStatusObjects.Clear();
    }
}
