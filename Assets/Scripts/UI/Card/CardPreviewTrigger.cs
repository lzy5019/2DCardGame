using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardPreviewTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private bool isPointerOver = false;
    private Image cardImage;

    private void Update()
    {
        if (isPointerOver && Input.GetMouseButtonDown(1))
        {
            cardImage = GetComponent<Image>();
            CardPreviewManager.Instance.ShowPreview(cardImage.sprite);
        }

        if (Input.GetMouseButtonUp(1))
        {
            CardPreviewManager.Instance.HidePreview();
        }
    }

    // 检查鼠标是否在卡牌上
    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
    }
}