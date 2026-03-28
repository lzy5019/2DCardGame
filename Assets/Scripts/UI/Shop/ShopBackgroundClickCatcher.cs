/// <summary>
/// 用于检测点击其他地方，取消商店牌选中
/// </summary>

using UnityEngine;
using UnityEngine.EventSystems;

public class ShopBackgroundClickCatcher : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        //ShopSlotUI.ClearCurrentSelection();
    }
}