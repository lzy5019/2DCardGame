using UnityEngine;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour
{
    public CardData card;
    public int slotIndex;
    
    public Image cardImage;

    private void Awake()
    {
        cardImage = GetComponent<Image>();
    }

    public void SetCard(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            card = null;
            cardImage.sprite = null;
            cardImage.enabled = false;
            return;
        }

        card = CardDatabase.Instance.GetCardById(cardId);

        if (card == null)
        {
            cardImage.sprite = null;
            cardImage.enabled = false;
            return;
        }

        cardImage.sprite = card.cardSprite;
        cardImage.enabled = true;
    }
}