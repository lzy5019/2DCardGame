using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentCardUI : MonoBehaviour
{
    #region 界面引用
    [Header("界面引用")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Button button;

    [Header("状态显示")]
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private GameObject usedMaskObject;
    [SerializeField] private TMP_Text typeText;
    #endregion

    #region 运行时状态
    private string cardId;
    private CardData cardData;
    private bool isWeapon;
    private int equipmentIndex = -1;
    #endregion

    public string CardId => cardId;
    public CardData CardData => cardData;

    #region 初始化
    public void Setup(string newCardId, CardData cardData, bool weapon, int index, bool isUsed, bool canUse)
    {
        cardId = newCardId;
        this.cardData = cardData;
        isWeapon = weapon;
        equipmentIndex = index;

        if (cardImage != null)
        {
            cardImage.sprite = cardData.cardSprite;
            cardImage.preserveAspect = true;
        }

        if (typeText != null)
        {
            typeText.text = isWeapon ? "Weapon" : "Support";
        }

        SetUsed(isUsed);
        SetHighlight(canUse);

        if (button != null)
        {
            button.interactable = canUse;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickUse);
        }
    }
    #endregion

    #region 视觉状态
    public void SetUsed(bool isUsed)
    {
        if (usedMaskObject != null)
        {
            usedMaskObject.SetActive(isUsed);
        }

        if (button != null && isUsed)
        {
            button.interactable = false;
        }
    }

    public void SetHighlight(bool canUse)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(canUse);
        }

        if (button != null)
        {
            button.interactable = canUse;
        }
    }
    #endregion

    #region 按钮事件
    private void OnClickUse()
    {
        if (EquipmentZoneUI.Instance == null)
            return;

        if (isWeapon)
        {
            EquipmentZoneUI.Instance.RequestUseWeaponFromUI();
        }
        else
        {
            EquipmentZoneUI.Instance.RequestUseEquipmentFromUI(equipmentIndex);
        }
    }
    #endregion
}

