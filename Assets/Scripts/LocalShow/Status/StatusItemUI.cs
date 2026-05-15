using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatusItemUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Frames")]
    [SerializeField] private GameObject buffFrameObject;
    [SerializeField] private GameObject debuffFrameObject;

    [Header("Art")]
    [SerializeField] private Image cardImage;

    [Header("Optional Count Displays")]
    [SerializeField] private GameObject timerRoot;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private GameObject manaCleanseRoot;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private GameObject attackCleanseRoot;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private GameObject stackRoot;
    [SerializeField] private TMP_Text stackText;

    [Header("Optional Interaction")]
    [SerializeField] private Button button;

    private string statusCardId;
    private CardData cardData;

    public string StatusCardId => statusCardId;
    public CardData CardData => cardData;

    public void Setup(
        string newStatusCardId,
        CardData newCardData,
        int stackCount,
        int remainingTurns,
        int manaCleanseValue,
        int attackCleanseValue)
    {
        statusCardId = newStatusCardId;
        cardData = newCardData;

        if (cardImage != null)
        {
            cardImage.sprite = newCardData != null ? newCardData.cardSprite : null;
            cardImage.preserveAspect = true;
            cardImage.enabled = cardImage.sprite != null;
        }

        bool isBuff = newCardData != null && newCardData.cardType == CardType.Buff;
        bool isDebuff = newCardData != null && newCardData.cardType == CardType.Debuff;

        if (buffFrameObject != null)
        {
            buffFrameObject.SetActive(isBuff);
        }

        if (debuffFrameObject != null)
        {
            debuffFrameObject.SetActive(isDebuff);
        }

        SetOptionalCountDisplay(timerRoot, turnText, remainingTurns > 0, remainingTurns.ToString());
        SetOptionalCountDisplay(manaCleanseRoot, manaText, manaCleanseValue > 0, manaCleanseValue.ToString());
        SetOptionalCountDisplay(attackCleanseRoot, attackText, attackCleanseValue > 0, attackCleanseValue.ToString());
        SetOptionalCountDisplay(stackRoot, stackText, stackCount > 1, stackCount.ToString());

        bool canUseButton = manaCleanseValue > 0 || attackCleanseValue > 0;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = canUseButton;
            if (canUseButton)
            {
                button.onClick.AddListener(OnClickUseStatus);
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;
        if (CardPreviewManager.Instance == null)
            return;
        if (cardData == null || cardData.cardSprite == null)
            return;

        CardPreviewManager.Instance.ShowPreview(cardData.cardSprite, cardData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;
        if (CardPreviewManager.Instance == null)
            return;

        CardPreviewManager.Instance.HidePreview();
    }

    private void SetOptionalCountDisplay(GameObject rootObject, TMP_Text textComponent, bool shouldShow, string valueText)
    {
        if (rootObject != null)
        {
            rootObject.SetActive(shouldShow);
        }

        if (textComponent != null)
        {
            textComponent.text = shouldShow ? valueText : string.Empty;
        }
    }

    private void OnClickUseStatus()
    {
        if (StatusAreaUI.Instance == null)
            return;

        StatusAreaUI.Instance.RequestUseStatusFromUI(statusCardId);
    }
}
