using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardPreviewTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Image cardImage;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;
        if (CardPreviewManager.Instance == null)
            return;

        cardImage = GetComponent<Image>();

        CardData previewCardData = ResolvePreviewCardData();
        Sprite previewSprite = ResolvePreviewSprite(previewCardData);
        if (previewSprite == null)
            return;

        CardPreviewManager.Instance.ShowPreview(previewSprite, previewCardData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;
        if (CardPreviewManager.Instance == null)
            return;

        CardPreviewManager.Instance.HidePreview();
    }

    private Sprite ResolvePreviewSprite(CardData previewCardData)
    {
        if (previewCardData != null && previewCardData.cardSprite != null)
            return previewCardData.cardSprite;
        if (cardImage != null)
            return cardImage.sprite;

        return null;
    }

    private CardData ResolvePreviewCardData()
    {
        PileCardItemUI pileCardItemUI = GetComponent<PileCardItemUI>();
        if (pileCardItemUI != null && pileCardItemUI.cardData != null)
            return pileCardItemUI.cardData;

        ShopSlotUI shopSlotUI = GetComponent<ShopSlotUI>();
        if (shopSlotUI != null && shopSlotUI.card != null)
            return shopSlotUI.card;

        EquipmentCardUI equipmentCardUI = GetComponent<EquipmentCardUI>();
        if (equipmentCardUI != null && equipmentCardUI.CardData != null)
            return equipmentCardUI.CardData;

        HandCardUI handCardUI = GetComponent<HandCardUI>();
        if (handCardUI != null && !string.IsNullOrEmpty(handCardUI.cardId))
            return TryGetCardDataById(handCardUI.cardId);

        if (cardImage != null && cardImage.sprite != null && CardDatabase.Instance != null)
        {
            for (int i = 0; i < CardDatabase.Instance.allCards.Count; i++)
            {
                CardData cardData = CardDatabase.Instance.allCards[i];
                if (cardData != null && cardData.cardSprite == cardImage.sprite)
                    return cardData;
            }
        }

        return null;
    }

    private CardData TryGetCardDataById(string cardId)
    {
        if (string.IsNullOrEmpty(cardId) || CardDatabase.Instance == null)
            return null;

        for (int i = 0; i < CardDatabase.Instance.allCards.Count; i++)
        {
            CardData cardData = CardDatabase.Instance.allCards[i];
            if (cardData != null && cardData.cardId == cardId)
                return cardData;
        }

        return null;
    }
}
