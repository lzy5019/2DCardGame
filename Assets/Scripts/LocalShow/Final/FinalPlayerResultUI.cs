using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinalPlayerResultUI : MonoBehaviour
{
    private static Dictionary<string, CardData> cachedCardDataById;

    [Header("Player Order")]
    [SerializeField] private GameObject playerOrder01Root;
    [SerializeField] private GameObject playerOrder02Root;
    [SerializeField] private GameObject playerOrder03Root;
    [SerializeField] private GameObject playerOrder04Root;

    [Header("Texts")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text totalScoreText;
    [SerializeField] private TMP_Text gainScoreText;
    [SerializeField] private TMP_Text cardValueScoreText;

    [Header("Interactions")]
    [SerializeField] private Button viewOwnedCardsButton;
    [SerializeField] private string ownedPileTitleSuffix = "的牌组";

    [Header("Key Cards")]
    [SerializeField] private Image[] keyCardImages = new Image[3];
    [SerializeField] private bool hideEmptyCardSlots = false;
    [SerializeField] private Sprite fallbackCardSprite;

    private readonly List<string> ownedCardIds = new List<string>();

    private void Awake()
    {
        if (viewOwnedCardsButton != null)
        {
            viewOwnedCardsButton.onClick.AddListener(HandleViewOwnedCardsClicked);
        }
    }

    private void OnDestroy()
    {
        if (viewOwnedCardsButton != null)
        {
            viewOwnedCardsButton.onClick.RemoveListener(HandleViewOwnedCardsClicked);
        }
    }

    public void SetData(int playerOrder, string playerName, int totalScore, int gainScore, int cardValueScore, IList<string> keyCardIds = null)
    {
        SetPlayerOrder(playerOrder);
        SetPlayerName(playerName);
        SetScores(totalScore, gainScore, cardValueScore);
        SetKeyCards(keyCardIds);
    }

    public void SetOwnedCardIds(IList<string> sourceOwnedCardIds)
    {
        ownedCardIds.Clear();
        if (sourceOwnedCardIds == null)
            return;

        for (int i = 0; i < sourceOwnedCardIds.Count; i++)
        {
            ownedCardIds.Add(sourceOwnedCardIds[i]);
        }
    }

    public IReadOnlyList<string> GetOwnedCardIds()
    {
        return ownedCardIds;
    }

    public void SetPlayerOrder(int playerOrder)
    {
        SetActiveSafe(playerOrder01Root, playerOrder == 1);
        SetActiveSafe(playerOrder02Root, playerOrder == 2);
        SetActiveSafe(playerOrder03Root, playerOrder == 3);
        SetActiveSafe(playerOrder04Root, playerOrder == 4);
    }

    public void SetPlayerName(string playerName)
    {
        if (playerNameText != null)
        {
            playerNameText.text = string.IsNullOrEmpty(playerName) ? "-" : playerName;
        }
    }

    public void SetScores(int totalScore, int gainScore, int cardValueScore)
    {
        SetTextValue(totalScoreText, totalScore);
        SetTextValue(gainScoreText, gainScore);
        SetTextValue(cardValueScoreText, cardValueScore);
    }

    public void SetTotalScore(int totalScore)
    {
        SetTextValue(totalScoreText, totalScore);
    }

    public void SetGainScore(int gainScore)
    {
        SetTextValue(gainScoreText, gainScore);
    }

    public void SetCardValueScore(int cardValueScore)
    {
        SetTextValue(cardValueScoreText, cardValueScore);
    }

    public void SetTotalScoreText(string textValue)
    {
        SetTextString(totalScoreText, textValue);
    }

    public void SetGainScoreText(string textValue)
    {
        SetTextString(gainScoreText, textValue);
    }

    public void SetCardValueScoreText(string textValue)
    {
        SetTextString(cardValueScoreText, textValue);
    }

    public void SetKeyCards(IList<string> keyCardIds)
    {
        int slotCount = keyCardImages != null ? keyCardImages.Length : 0;
        for (int i = 0; i < slotCount; i++)
        {
            string cardId = keyCardIds != null && i < keyCardIds.Count ? keyCardIds[i] : null;
            SetKeyCardAt(i, cardId);
        }
    }

    public void HideAllKeyCards()
    {
        int slotCount = keyCardImages != null ? keyCardImages.Length : 0;
        for (int i = 0; i < slotCount; i++)
        {
            Image targetImage = keyCardImages[i];
            if (targetImage == null)
                continue;

            targetImage.gameObject.SetActive(false);
        }
    }

    public void RevealKeyCardAt(int slotIndex, string cardId)
    {
        SetKeyCardAt(slotIndex, cardId);
    }

    public void SetKeyCardAt(int slotIndex, string cardId)
    {
        if (keyCardImages == null || slotIndex < 0 || slotIndex >= keyCardImages.Length)
            return;

        Image targetImage = keyCardImages[slotIndex];
        if (targetImage == null)
            return;

        if (string.IsNullOrEmpty(cardId))
        {
            ApplyEmptyCardSlot(targetImage);
            return;
        }

        CardData cardData = TryGetCardData(cardId);
        if (cardData == null || cardData.cardSprite == null)
        {
            ApplyEmptyCardSlot(targetImage);
            return;
        }

        targetImage.gameObject.SetActive(true);
        targetImage.enabled = true;
        targetImage.sprite = cardData.cardSprite;
        targetImage.preserveAspect = true;
    }

    [ContextMenu("Auto Bind From Default Hierarchy")]
    private void AutoBindFromDefaultHierarchy()
    {
        playerOrder01Root = FindGameObject("01");
        playerOrder02Root = FindGameObject("02");
        playerOrder03Root = FindGameObject("03");
        playerOrder04Root = FindGameObject("04");

        playerNameText = FindText("Name");
        totalScoreText = FindText("总分/分数");
        gainScoreText = FindText("得分/分数");
        cardValueScoreText = FindText("卡牌价值/分数");

        EnsureKeyCardArray();
        keyCardImages[0] = FindImage("Card/1");
        keyCardImages[1] = FindImage("Card/2");
        keyCardImages[2] = FindImage("Card/3");
        viewOwnedCardsButton = FindButton("Button");
    }

    private void EnsureKeyCardArray()
    {
        if (keyCardImages == null || keyCardImages.Length != 3)
        {
            keyCardImages = new Image[3];
        }
    }

    private void SetTextValue(TMP_Text targetText, int value)
    {
        if (targetText != null)
        {
            targetText.text = value.ToString();
        }
    }

    private void SetTextString(TMP_Text targetText, string textValue)
    {
        if (targetText != null)
        {
            targetText.text = textValue ?? string.Empty;
        }
    }

    private void SetActiveSafe(GameObject targetObject, bool isActive)
    {
        if (targetObject != null)
        {
            targetObject.SetActive(isActive);
        }
    }

    private void ApplyEmptyCardSlot(Image targetImage)
    {
        if (hideEmptyCardSlots)
        {
            targetImage.gameObject.SetActive(false);
            return;
        }

        targetImage.gameObject.SetActive(true);
        targetImage.enabled = fallbackCardSprite != null;
        targetImage.sprite = fallbackCardSprite;
        targetImage.preserveAspect = true;
    }

    private CardData TryGetCardData(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return null;

        if (CardDatabase.Instance != null)
        {
            List<CardData> allCards = CardDatabase.Instance.allCards;
            for (int i = 0; i < allCards.Count; i++)
            {
                CardData cardData = allCards[i];
                if (cardData != null && cardData.cardId == cardId)
                {
                    return cardData;
                }
            }
        }

        EnsureFallbackCardCache();
        if (cachedCardDataById != null && cachedCardDataById.TryGetValue(cardId, out CardData cachedCardData))
        {
            return cachedCardData;
        }

        return null;
    }

    private GameObject FindGameObject(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.gameObject : null;
    }

    private TMP_Text FindText(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private Image FindImage(string path)
    {
        Transform target = transform.Find(path);
        if (target == null)
            return null;

        Image image = target.GetComponent<Image>();
        if (image != null)
            return image;

        return target.GetComponentInChildren<Image>(true);
    }

    private Button FindButton(string path)
    {
        Transform target = transform.Find(path);
        if (target == null)
            return null;

        Button button = target.GetComponent<Button>();
        if (button != null)
            return button;

        return target.GetComponentInChildren<Button>(true);
    }

    private void HandleViewOwnedCardsClicked()
    {
        if (PileBrowserUI.Instance == null)
        {
            Debug.LogWarning("FinalPlayerResultUI: PileBrowserUI.Instance is null.");
            return;
        }

        string displayName = playerNameText != null ? playerNameText.text : "玩家";
        string title = string.IsNullOrEmpty(displayName)
            ? ownedPileTitleSuffix
            : displayName + ownedPileTitleSuffix;

        PileBrowserUI.Instance.OpenCustomPile(title, ownedCardIds, PileDisplayOrder.IdDescending);
    }

    private static void EnsureFallbackCardCache()
    {
        if (cachedCardDataById != null)
            return;

        cachedCardDataById = new Dictionary<string, CardData>();
        CardData[] loadedCards = Resources.LoadAll<CardData>("Cards");
        for (int i = 0; i < loadedCards.Length; i++)
        {
            CardData cardData = loadedCards[i];
            if (cardData == null || string.IsNullOrEmpty(cardData.cardId))
                continue;

            if (!cachedCardDataById.ContainsKey(cardData.cardId))
            {
                cachedCardDataById.Add(cardData.cardId, cardData);
            }
        }
    }
}
