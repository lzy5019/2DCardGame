using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PileBrowserUI : MonoBehaviour
{
    public static PileBrowserUI Instance;

    #region 界面引用
    [Header("核心引用")]
    public GameObject cardPrefab;
    public GameObject pilePanel;
    public Transform contentRoot;
    public TMP_Text titleText;
    public Button closeButton;
    public GameObject emptyHintObject = null;
    #endregion

    #region 运行时状态
    public PlayerState localPlayer;
    private readonly List<GameObject> spawnedCards = new List<GameObject>();
    private string currentPileKey = "";

    public bool IsOpen
    {
        get
        {
            return pilePanel != null && pilePanel.gameObject.activeSelf;
        }
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
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePile);
        }

        if (pilePanel != null)
        {
            pilePanel.gameObject.SetActive(false);
        }

        if (emptyHintObject != null)
        {
            emptyHintObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePile);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region 注册
    public void RegisterLocalPlayer(PlayerState player)
    {
        localPlayer = player;
    }
    #endregion

    #region 打开与关闭
    public void TogglePile(string pileKey, System.Action openAction)
    {
        if (IsOpen && currentPileKey == pileKey)
        {
            ClosePile();
            currentPileKey = "";
            return;
        }

        openAction.Invoke();
        currentPileKey = pileKey;
    }

    public void OpenPile(string title, List<string> cardIDs)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }

        RebuildCards(cardIDs);
        pilePanel.gameObject.SetActive(true);
    }

    public void ClosePile()
    {
        if (pilePanel != null)
        {
            pilePanel.gameObject.SetActive(false);
        }

        currentPileKey = "";
    }
    #endregion

    #region 卡牌渲染
    private void RebuildCards(List<string> cardIDs)
    {
        ClearCards();

        if (cardIDs == null || cardIDs.Count == 0)
        {
            if (emptyHintObject != null)
            {
                emptyHintObject.SetActive(true);
            }
            return;
        }

        int createdCount = 0;

        for (int i = 0; i < cardIDs.Count; i++)
        {
            string cardId = cardIDs[i];

            if (string.IsNullOrEmpty(cardId))
                continue;

            if (CardDatabase.Instance == null)
            {
                Debug.LogError("PileBrowserUI: CardDatabase.Instance is null.");
                continue;
            }

            CardData cardData = CardDatabase.Instance.GetCardById(cardId);

            if (cardData == null)
            {
                Debug.LogWarning("PileBrowserUI: Card not found for id = " + cardId);
                continue;
            }

            GameObject obj = Instantiate(cardPrefab, contentRoot);
            spawnedCards.Add(obj);

            PileCardItemUI itemUI = obj.GetComponent<PileCardItemUI>();
            if (itemUI != null)
            {
                itemUI.SetCard(cardData, cardId);
            }
            else
            {
                // 如果当前还没有专用显示组件，则退回为直接赋值图片。
                Image image = obj.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = cardData.cardSprite;
                    image.preserveAspect = true;
                }
            }

            createdCount++;
        }

        if (emptyHintObject != null)
        {
            emptyHintObject.SetActive(createdCount == 0);
        }
    }

    private void ClearCards()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
            {
                Destroy(spawnedCards[i]);
            }
        }

        spawnedCards.Clear();
    }
    #endregion

    #region 排序
    private List<string> GetDisplayList(List<string> source, PileDisplayOrder order)
    {
        List<string> result = new List<string>(source);

        switch (order)
        {
            case PileDisplayOrder.KeepOriginal:
                break;

            case PileDisplayOrder.IdAscending:
                result.Sort(CompareCardIdAscending);
                break;

            case PileDisplayOrder.IdDescending:
                result.Sort(CompareCardIdDescending);
                break;
        }

        return result;
    }

    private int CompareCardIdAscending(string a, string b)
    {
        bool aOk = int.TryParse(a, out int aNum);
        bool bOk = int.TryParse(b, out int bNum);

        if (aOk && bOk)
            return aNum.CompareTo(bNum);

        return string.Compare(a, b, System.StringComparison.Ordinal);
    }

    private int CompareCardIdDescending(string a, string b)
    {
        bool aOk = int.TryParse(a, out int aNum);
        bool bOk = int.TryParse(b, out int bNum);

        if (aOk && bOk)
            return bNum.CompareTo(aNum);

        return string.Compare(b, a, System.StringComparison.Ordinal);
    }
    #endregion

    #region 快捷操作
    public void OpenDrawPile()
    {
        List<string> displayList = GetDisplayList(
            new List<string>(localPlayer.drawPile),
            PileDisplayOrder.IdDescending
        );

        OpenPile("抽牌堆", displayList);
    }

    public void OpenDiscardPile()
    {
        List<string> displayList = GetDisplayList(
            new List<string>(localPlayer.discardPile),
            PileDisplayOrder.IdDescending
        );

        OpenPile("弃牌堆", displayList);
    }

    public void OpenHandPile()
    {
        OpenPile("手牌", new List<string>(localPlayer.handCardIds));
    }

    public void OpenPlayedPile()
    {
        List<string> displayList = GetDisplayList(
            new List<string>(localPlayer.playedCardIds),
            PileDisplayOrder.KeepOriginal
        );

        OpenPile("本回合出牌", displayList);
    }

    public void OpenOwnedPile()
    {
        List<string> displayList = GetDisplayList(
            new List<string>(localPlayer.ownedCardIds),
            PileDisplayOrder.IdDescending
        );

        OpenPile("牌库总览", displayList);
    }
    #endregion
}

public enum PileDisplayOrder
{
    KeepOriginal,   // 保持原顺序
    IdAscending,    // 按编号从小到大排序
    IdDescending    // 按编号从大到小排序
}

