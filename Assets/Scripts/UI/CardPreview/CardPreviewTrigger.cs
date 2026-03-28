using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardPreviewTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Image cardImage;

    public void OnPointerDown(PointerEventData eventData)
    {
        cardImage = GetComponent<Image>();
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (cardImage == null) return;
        if (cardImage.sprite == null) return;
        if (CardPreviewManager.Instance == null) return;

        CardPreviewManager.Instance.ShowPreview(cardImage.sprite);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (CardPreviewManager.Instance == null) return;

        CardPreviewManager.Instance.HidePreview();
    }
}
