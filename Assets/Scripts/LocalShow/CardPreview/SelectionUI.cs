using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionUI : MonoBehaviour
{
    public static SelectionUI Instance;

    private enum SelectionDisplayMode
    {
        None,
        Card,
        Player
    }

    #region 界面引用
    [Header("核心引用")]
    public GameObject background;
    public Button hideButton;
    public TMP_Text titleText;
    public Button confirmButton;
    public GameObject selectionPanel;
    public GameObject cardSelectionPanel;
    public GameObject playerSelectionPanel;

    [Header("滚动视图")]
    public ScrollRect scrollView;
    public Transform contentRoot;
    public Transform playerContentRoot;

    [Header("选项预制体")]
    public GameObject selectionCardPrefab;
    public GameObject selectionPlayerPrefab;
    #endregion

    #region 状态
    [Header("状态")]
    public bool isSelecting;

    private readonly List<int> selectedIndexes = new List<int>();
    private readonly List<SelectionCardUI> spawnedOptions = new List<SelectionCardUI>();
    private readonly List<SelectionPlayerItemUI> spawnedPlayerOptions = new List<SelectionPlayerItemUI>();

    private int minSelectCount = 1;
    private int maxSelectCount = 1;
    private bool isBackgroundVisible = true;
    private Action<List<int>> onConfirmSelection;
    private SelectionDisplayMode currentDisplayMode = SelectionDisplayMode.None;
    #endregion

    #region 属性
    public int SelectedCount
    {
        get { return selectedIndexes.Count; }
    }

    public int MinSelectCount
    {
        get { return minSelectCount; }
    }

    public int MaxSelectCount
    {
        get { return maxSelectCount; }
    }
    #endregion

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (hideButton != null)
        {
            hideButton.onClick.AddListener(ToggleBackground);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(ConfirmSelection);
        }

        CloseSelection();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (hideButton != null)
        {
            hideButton.onClick.RemoveListener(ToggleBackground);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelection);
        }
    }
    #endregion

    #region 打开与关闭
    public void ShowSelection(string title, List<Sprite> optionSprites, int selectCount, Action<List<int>> onConfirm)
    {
        ShowSelection(title, optionSprites, selectCount, selectCount, onConfirm);
    }

    public void ShowSelection(string title, List<Sprite> optionSprites, int minCount, int maxCount, Action<List<int>> onConfirm)
    {
        if (optionSprites == null || optionSprites.Count == 0)
        {
            Debug.LogWarning("SelectionUI: optionSprites is empty. The selection panel cannot be opened.");
            return;
        }

        PrepareSelection(title, minCount, maxCount, optionSprites.Count, onConfirm, SelectionDisplayMode.Card);
        ClearPlayerOptions();
        ClearCardOptions();
        BuildCardOptions(optionSprites);
        SetPanelState(SelectionDisplayMode.Card);
        RefreshOptionVisuals();
    }

    public void ShowPlayerSelection(string title, List<PlayerState> optionPlayers, int minCount, int maxCount, Action<List<int>> onConfirm)
    {
        if (optionPlayers == null || optionPlayers.Count == 0)
        {
            Debug.LogWarning("SelectionUI: optionPlayers is empty. The player selection panel cannot be opened.");
            return;
        }

        PrepareSelection(title, minCount, maxCount, optionPlayers.Count, onConfirm, SelectionDisplayMode.Player);
        ClearCardOptions();
        ClearPlayerOptions();
        BuildPlayerOptions(optionPlayers);
        SetPanelState(SelectionDisplayMode.Player);
        RefreshOptionVisuals();
    }

    public void CloseSelection()
    {
        isSelecting = false;
        selectedIndexes.Clear();
        onConfirmSelection = null;
        currentDisplayMode = SelectionDisplayMode.None;

        ClearCardOptions();
        ClearPlayerOptions();

        if (selectionPanel != null)
        {
            selectionPanel.SetActive(false);
        }

        if (cardSelectionPanel != null)
        {
            cardSelectionPanel.SetActive(false);
        }

        if (playerSelectionPanel != null)
        {
            playerSelectionPanel.SetActive(false);
        }

        SetBackgroundVisible(true);
        RefreshConfirmButton();
    }
    #endregion

    #region 交互
    public void OnOptionClicked(int optionIndex)
    {
        if (!isSelecting)
            return;

        if (selectedIndexes.Contains(optionIndex))
        {
            selectedIndexes.Remove(optionIndex);
        }
        else
        {
            if (selectedIndexes.Count >= maxSelectCount)
            {
                selectedIndexes.RemoveAt(0);
            }

            selectedIndexes.Add(optionIndex);
        }

        RefreshOptionVisuals();
        RefreshConfirmButton();
    }

    private void ConfirmSelection()
    {
        if (!isSelecting)
            return;

        if (selectedIndexes.Count < minSelectCount || selectedIndexes.Count > maxSelectCount)
            return;

        List<int> result = new List<int>(selectedIndexes);
        onConfirmSelection?.Invoke(result);

        CloseSelection();
    }

    private void ToggleBackground()
    {
        SetBackgroundVisible(!isBackgroundVisible);
    }

    public void TriggerHideByHotkey()
    {
        if (!isSelecting)
            return;

        if (hideButton != null)
        {
            hideButton.onClick.Invoke();
        }
        else
        {
            ToggleBackground();
        }
    }
    #endregion

    #region 渲染
    private void SetBackgroundVisible(bool visible)
    {
        isBackgroundVisible = visible;

        if (background != null)
        {
            background.SetActive(visible);
        }

        bool showCardPanel = visible && currentDisplayMode == SelectionDisplayMode.Card;
        bool showPlayerPanel = visible && currentDisplayMode == SelectionDisplayMode.Player;

        if (cardSelectionPanel != null)
        {
            cardSelectionPanel.SetActive(showCardPanel);
        }

        if (playerSelectionPanel != null)
        {
            playerSelectionPanel.SetActive(showPlayerPanel);
        }
    }

    private void RefreshConfirmButton()
    {
        if (confirmButton == null)
            return;

        int selectedCount = selectedIndexes.Count;
        bool canConfirm = selectedCount >= minSelectCount && selectedCount <= maxSelectCount;

        confirmButton.interactable = isSelecting && canConfirm;
    }

    private void BuildCardOptions(List<Sprite> optionSprites)
    {
        if (selectionCardPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("SelectionUI: selectionCardPrefab or contentRoot is not assigned.");
            return;
        }

        for (int i = 0; i < optionSprites.Count; i++)
        {
            GameObject optionObj = Instantiate(selectionCardPrefab, contentRoot);

            SelectionCardUI cardUI = optionObj.GetComponent<SelectionCardUI>();
            if (cardUI == null)
            {
                Debug.LogWarning("SelectionUI: selectionCardPrefab is missing SelectionCardUI.");
                Destroy(optionObj);
                continue;
            }

            cardUI.Setup(optionSprites[i], i, OnOptionClicked);
            spawnedOptions.Add(cardUI);
        }
    }

    private void BuildPlayerOptions(List<PlayerState> optionPlayers)
    {
        if (selectionPlayerPrefab == null || playerContentRoot == null)
        {
            Debug.LogWarning("SelectionUI: selectionPlayerPrefab or playerContentRoot is not assigned.");
            return;
        }

        for (int i = 0; i < optionPlayers.Count; i++)
        {
            PlayerState targetPlayer = optionPlayers[i];
            if (targetPlayer == null)
                continue;

            GameObject optionObj = Instantiate(selectionPlayerPrefab, playerContentRoot);
            SelectionPlayerItemUI playerItemUI = optionObj.GetComponent<SelectionPlayerItemUI>();
            if (playerItemUI == null)
            {
                playerItemUI = optionObj.AddComponent<SelectionPlayerItemUI>();
            }

            playerItemUI.Setup(targetPlayer, i, OnOptionClicked);
            spawnedPlayerOptions.Add(playerItemUI);
        }
    }

    private void ClearCardOptions()
    {
        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            if (spawnedOptions[i] != null)
            {
                Destroy(spawnedOptions[i].gameObject);
            }
        }

        spawnedOptions.Clear();
    }

    private void ClearPlayerOptions()
    {
        for (int i = 0; i < spawnedPlayerOptions.Count; i++)
        {
            if (spawnedPlayerOptions[i] != null)
            {
                Destroy(spawnedPlayerOptions[i].gameObject);
            }
        }

        spawnedPlayerOptions.Clear();
    }

    private void RefreshOptionVisuals()
    {
        if (currentDisplayMode == SelectionDisplayMode.Player)
        {
            for (int i = 0; i < spawnedPlayerOptions.Count; i++)
            {
                if (spawnedPlayerOptions[i] == null)
                    continue;

                bool isSelected = selectedIndexes.Contains(i);
                spawnedPlayerOptions[i].SetSelected(isSelected);
            }

            return;
        }

        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            if (spawnedOptions[i] == null)
                continue;

            bool isSelected = selectedIndexes.Contains(i);
            spawnedOptions[i].SetSelected(isSelected);
        }
    }
    #endregion

    private void PrepareSelection(string title, int minCount, int maxCount, int optionCount, Action<List<int>> onConfirm, SelectionDisplayMode displayMode)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }

        minSelectCount = Mathf.Max(0, minCount);
        maxSelectCount = Mathf.Max(minSelectCount, maxCount);

        if (maxSelectCount > optionCount)
        {
            maxSelectCount = optionCount;
        }

        if (minSelectCount > maxSelectCount)
        {
            minSelectCount = maxSelectCount;
        }

        onConfirmSelection = onConfirm;
        currentDisplayMode = displayMode;

        selectedIndexes.Clear();
        isSelecting = true;

        SetBackgroundVisible(true);
        RefreshConfirmButton();
    }

    private void SetPanelState(SelectionDisplayMode displayMode)
    {
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(displayMode != SelectionDisplayMode.None);
        }

        SetBackgroundVisible(isBackgroundVisible);
    }
}

