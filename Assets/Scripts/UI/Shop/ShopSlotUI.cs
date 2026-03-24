using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour, IPointerClickHandler
{
    private static ShopSlotUI currentSelected;

    [SerializeField] private PlayerDeckManager playerDeckManager;
    [SerializeField] private PlayerDataDisplay playerDataDisplay;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Vector3 selectedScale;

    [Header("放大倍率")]
    public float scaleMultiplier = 1.15f;
    public CardData cardData;
    public int slotIndex;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        selectedScale = originalScale * scaleMultiplier;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        // 如果当前点的是别的卡，先让之前选中的还原
        if (currentSelected != null && currentSelected != this)
        {
            currentSelected.ResetScale();
        }

        // 如果自己本来就选中了，就进行购买
        if (currentSelected == this)
        {
            TryBuyCard(cardData, slotIndex);
            return;
        }

        Enlarge();
        currentSelected = this;
    }
    private void TryBuyCard(CardData card, int slotIndex)   // 购买卡牌
    {
        PlayerData player = PlayerDataManager.Instance.players[playerDataDisplay.playerId];
        if (card.cardType == CardType.Monster)
        {
            if (player.attack < card.cost)
            {
                Debug.Log("攻击不足");
                return;
            }
            player.attack -= card.cost;
            CardEffectManager.Instance.ResolveCardEffect(card.cardId, playerDataDisplay.playerId);
        }
        else 
        {
            if (player.mana < card.cost)
            {
                Debug.Log("费用不足");
                return;
            }
            player.mana -= card.cost;
            playerDeckManager.GainCard(card.cardId);
        }

        ClearCurrentSelection();
        playerDataDisplay.RefreshDisplay();
        
        ShopDeckManager.Instance.RefillCard(slotIndex);
        
    }

    public void Enlarge()
    {
        rectTransform.localScale = selectedScale;
    }

    public void ResetScale()
    {
        rectTransform.localScale = originalScale;

        if (currentSelected == this)
        {
            currentSelected = null;
        }
    }

    public static void ClearCurrentSelection()
    {
        if (currentSelected != null)
        {
            currentSelected.ResetScale();
            currentSelected = null;
        }
    }
}