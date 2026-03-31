using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour, IPointerClickHandler
{
    public CardData card;
    public int slotIndex;
    
    public Image cardImage;

    public ShopPanelUI panel;

    // 显示设置
    private Vector3 originalScale;
    [SerializeField] private float selectedScaleMultiplier = 1.15f;

    private void Awake()
    {
        cardImage = GetComponent<Image>();
        panel = GetComponentInParent<ShopPanelUI>();
        originalScale = transform.localScale;
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (card == null) return;
        if (panel == null) return;

        Debug.Log("点击位置: " + slotIndex);
        panel.OnSlotClicked(this);
    }

    public void SetSelected(bool selected)
    {
        if(selected)
        {
            transform.localScale = originalScale * selectedScaleMultiplier;
            Debug.Log("开启高亮");
        }
        else
        {
            transform.localScale = originalScale;
            Debug.Log("关闭高亮");
        }
    }
}