using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PileBrowserUI : MonoBehaviour
{
    public static PileBrowserUI instance;

    [Header("基础引用")]
    public GameObject cardPrefab;
    public GameObject pilePanel;
    public Transform contentRoot;
    public TMP_Text titleText;
    public Button closeButton;
    public GameObject emptyHintObject = null;

    private PlayerState localPlayer;
    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    public bool IsOpen
    {
        get
        {
            return pilePanel != null && pilePanel.gameObject.activeSelf;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
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

        if (instance == this)
        {
            instance = null;
        }
    }

    public void RegisterLocalPlayer(PlayerState player)
    {
        localPlayer = player;
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
    }

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
                Debug.LogError("PileBrowserUI: CardDatabase.Instance 是 null");
                continue;
            }

            CardData cardData = CardDatabase.Instance.GetCardById(cardId);

            if (cardData == null)
            {
                Debug.LogWarning("PileBrowserUI: 找不到卡牌 id = " + cardId);
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
                // 如果你暂时还没挂 PileCardItemUI，至少也会尝试直接给 Image 赋图
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

    #region 按键绑定快捷函数
    public void OpenDrawPile()
    {
        OpenPile("抽牌堆", new List<string>(localPlayer.drawPile));
    }

    public void OpenDiscardPile()
    {
        OpenPile("弃牌堆", new List<string>(localPlayer.discardPile));
    }

    public void OpenHandPile()
    {
        OpenPile("手牌", new List<string>(localPlayer.handCardIds));
    }

    public void OpenPlayedPile()
    {
        OpenPile("已打出", new List<string>(localPlayer.playedCardIds));
    }

    public void OpenOwnedPile()
    {
        OpenPile("牌库总览", new List<string>(localPlayer.ownedCardIds));
    }
    #endregion
}
