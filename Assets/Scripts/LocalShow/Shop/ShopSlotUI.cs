using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour, IPointerClickHandler
{
    public CardData card;
    public int slotIndex;
    public Image cardImage;
    public ShopPanelUI panel;

    #region 选中显示
    private Vector3 originalScale;

    [SerializeField] private float selectedScaleMultiplier = 1.05f;
    [SerializeField] private GameObject selectedFxObject;
    #endregion

    #region 生命周期
    private void Awake()
    {
        cardImage = GetComponent<Image>();
        panel = GetComponentInParent<ShopPanelUI>();
        originalScale = transform.localScale;

        if (selectedFxObject != null)
        {
            selectedFxObject.SetActive(false);
        }
    }
    #endregion

    #region 数据绑定
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
    #endregion

    #region 指针事件
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (card == null) return;
        if (panel == null) return;

        panel.OnSlotClicked(this);
    }
    #endregion

    #region 视觉状态
    public void SetSelected(bool selected)
    {
        if (selectedFxObject != null)
        {
            selectedFxObject.SetActive(selected);
        }

        if (selected)
        {
            transform.localScale = originalScale * selectedScaleMultiplier;
        }
        else
        {
            transform.localScale = originalScale;
        }
    }
    #endregion
}

