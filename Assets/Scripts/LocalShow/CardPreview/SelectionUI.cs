using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionUI : MonoBehaviour
{
    public static SelectionUI Instance;

    [Header("基础引用")]
    public GameObject background;
    public Button hideButton;
    public TMP_Text titleText;
    public Button confirmButton;
    public GameObject selectionPanel;

    [Header("Scroll View")]
    public ScrollRect scrollView;
    public Transform contentRoot;

    [Header("选项 Prefab")]
    public GameObject selectionCardPrefab;

    [Header("状态")]
    public bool isSelecting;

    private readonly List<int> selectedIndexes = new List<int>();
    private readonly List<SelectionCardUI> spawnedOptions = new List<SelectionCardUI>();

    private int minSelectCount = 1;
    private int maxSelectCount = 1;
    private bool isBackgroundVisible = true;

    private Action<List<int>> onConfirmSelection;

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

    public void ShowSelection(string title, List<Sprite> optionSprites, int selectCount, Action<List<int>> onConfirm)
    {
        ShowSelection(title, optionSprites, selectCount, selectCount, onConfirm);
    }

    public void ShowSelection(string title, List<Sprite> optionSprites, int minCount, int maxCount, Action<List<int>> onConfirm)
    {
        if (optionSprites == null || optionSprites.Count == 0)
        {
            Debug.LogWarning("SelectionUI: optionSprites 为空，无法打开选择面板。");
            return;
        }
        if (titleText != null)
        {
            titleText.text = title;
        }

        minSelectCount = Mathf.Max(0, minCount);
        maxSelectCount = Mathf.Max(minSelectCount, maxCount);

        if (maxSelectCount > optionSprites.Count)
        {
            maxSelectCount = optionSprites.Count;
        }

        if (minSelectCount > maxSelectCount)
        {
            minSelectCount = maxSelectCount;
        }

        onConfirmSelection = onConfirm;

        selectedIndexes.Clear();
        ClearOptions();
        BuildOptions(optionSprites);

        isSelecting = true;

        if (selectionPanel != null)
        {
            selectionPanel.SetActive(true);
        }

        SetBackgroundVisible(true);
        RefreshOptionVisuals();
        RefreshConfirmButton();
    }

    public void CloseSelection()
    {
        isSelecting = false;
        selectedIndexes.Clear();
        onConfirmSelection = null;

        ClearOptions();

        if (selectionPanel != null)
        {
            selectionPanel.SetActive(false);
        }

        SetBackgroundVisible(true);
        RefreshConfirmButton();
    }

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

    private void SetBackgroundVisible(bool visible)
    {
        isBackgroundVisible = visible;

        if (background != null)
        {
            background.SetActive(visible);
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

    private void BuildOptions(List<Sprite> optionSprites)
    {
        if (selectionCardPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("SelectionUI: selectionCardPrefab 或 contentRoot 没有赋值。");
            return;
        }

        for (int i = 0; i < optionSprites.Count; i++)
        {
            GameObject optionObj = Instantiate(selectionCardPrefab, contentRoot);

            SelectionCardUI cardUI = optionObj.GetComponent<SelectionCardUI>();
            if (cardUI == null)
            {
                Debug.LogWarning("SelectionUI: selectionCardPrefab 上缺少 SelectionCardUI 组件。");
                Destroy(optionObj);
                continue;
            }

            cardUI.Setup(optionSprites[i], i, OnOptionClicked);
            spawnedOptions.Add(cardUI);
        }
    }

    private void ClearOptions()
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

    private void RefreshOptionVisuals()
    {
        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            if (spawnedOptions[i] == null)
                continue;

            bool isSelected = selectedIndexes.Contains(i);
            spawnedOptions[i].SetSelected(isSelected);
        }
    }
}
