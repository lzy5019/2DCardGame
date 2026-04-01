using UnityEngine;
using UnityEngine.UI;

public class PileCardItemUI : MonoBehaviour
{
    public Image cardImage;

    public string cardId;
    public CardData cardData;

    public void SetCard(CardData data, string id)
    {
        cardData = data;
        cardId = id;

        if (cardImage != null)
        {
            cardImage.sprite = data.cardSprite;
            cardImage.preserveAspect = true;
        }

    }
}