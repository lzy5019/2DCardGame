using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PileCardItemUI : MonoBehaviour
{
    public Image cardImage;

    private string cardId;
    private CardData cardData;

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