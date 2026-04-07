using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ShopPanelUI : MonoBehaviour
{
    public static ShopPanelUI Instance;

    #region 界面引用
    public GameObject baseShopPanel;
    public GameObject centerShopPanel;
    public List<ShopSlotUI> baseSlots = new List<ShopSlotUI>();
    public List<ShopSlotUI> centerSlots = new List<ShopSlotUI>();
    #endregion

    #region 运行时状态
    private ShopState shopState;
    private bool hasRegisteredCallback = false;

    // 记录当前选中的槽位，以便第二次点击时确认购买。
    public ShopSlotUI currentSelectedSlot;
    private PlayerState localPlayer = null;
    #endregion

    #region 生命周期
    private void Awake()
    {
        Instance = this;
        GetAllSlots();
    }

    private void Start()
    {
        StartCoroutine(WaitForShopState());
    }

    private void OnDestroy()
    {
        UnregisterCallback();
    }
    #endregion

    #region 注册
    public void RegisterLocalPlayer(PlayerState player)
    {
        localPlayer = player;
    }

    private IEnumerator WaitForShopState()
    {
        yield return new WaitUntil(() => ShopState.Instance != null);

        shopState = ShopState.Instance;
        RegisterCallback();
        RefreshBaseShop();
        RefreshCenterShop();
    }

    private void GetAllSlots()
    {
        baseSlots.Clear();
        centerSlots.Clear();

        if (baseShopPanel != null)
        {
            ShopSlotUI[] baseShopSlotArray = baseShopPanel.GetComponentsInChildren<ShopSlotUI>(true);
            baseSlots.AddRange(baseShopSlotArray);
        }

        if (centerShopPanel != null)
        {
            ShopSlotUI[] centerShopSlotArray = centerShopPanel.GetComponentsInChildren<ShopSlotUI>(true);
            centerSlots.AddRange(centerShopSlotArray);
        }
    }
    #endregion

    #region 商店回调
    private void RegisterCallback()
    {
        if (shopState == null || hasRegisteredCallback) return;

        shopState.centerCardIds.OnAdd += OnCenterCardChanged;
        shopState.centerCardIds.OnSet += OnCenterCardSet;
        shopState.centerCardIds.OnRemove += OnCenterCardRemoved;
        shopState.centerCardIds.OnClear += OnCenterCardCleared;

        hasRegisteredCallback = true;
    }

    private void UnregisterCallback()
    {
        if (shopState == null || !hasRegisteredCallback) return;

        shopState.centerCardIds.OnAdd -= OnCenterCardChanged;
        shopState.centerCardIds.OnSet -= OnCenterCardSet;
        shopState.centerCardIds.OnRemove -= OnCenterCardRemoved;
        shopState.centerCardIds.OnClear -= OnCenterCardCleared;

        hasRegisteredCallback = false;
    }

    private void OnCenterCardChanged(int index)
    {
        RefreshCenterShop();
    }

    private void OnCenterCardSet(int index, string item)
    {
        RefreshCenterShop();
    }

    private void OnCenterCardRemoved(int index, string item)
    {
        RefreshCenterShop();
    }

    private void OnCenterCardCleared()
    {
        RefreshCenterShop();
    }
    #endregion

    #region 交互
    public void OnSlotClicked(ShopSlotUI clickedSlot)
    {
        if (clickedSlot == null || clickedSlot.card == null) return;

        if (currentSelectedSlot != clickedSlot)
        {
            if (currentSelectedSlot != null)
                currentSelectedSlot.SetSelected(false);

            currentSelectedSlot = clickedSlot;
            currentSelectedSlot.SetSelected(true);
            return;
        }

        if (localPlayer == null) return;
        if (!CanAffordCardLocal(clickedSlot))
            return;

        localPlayer.RequestBuyCard(clickedSlot.slotIndex);
        currentSelectedSlot.SetSelected(false);
        currentSelectedSlot = null;
    }

    private bool CanAffordCardLocal(ShopSlotUI slot)
    {
        if (slot == null || slot.card == null || localPlayer == null)
        {
            return false;
        }

        if (!localPlayer.isMyTurn)
        {
            HintManager.Instance.ShowHint("不是你的回合");
            return false;
        }

        CardData card = slot.card;

        if (card.cardCategory == CardCategory.Monster)
        {
            if (localPlayer.attack < card.cost)
            {
                if (HintManager.Instance != null)
                {
                    HintManager.Instance.ShowHint("攻击不足");
                }

                return false;
            }
        }
        else
        {
            if (localPlayer.mana < card.cost)
            {
                if (HintManager.Instance != null)
                {
                    HintManager.Instance.ShowHint("费用不足");
                }

                return false;
            }
        }

        return true;
    }
    #endregion

    #region 渲染
    private void RefreshCenterShop()
    {
        if (shopState == null) return;
        if (centerSlots == null || centerSlots.Count == 0) return;

        for (int i = 0; i < centerSlots.Count; i++)
        {
            string cardId = "";

            if (i < shopState.centerCardIds.Count)
            {
                cardId = shopState.centerCardIds[i];
            }

            centerSlots[i].SetCard(cardId);
            centerSlots[i].slotIndex = i;
        }
    }

    private void RefreshBaseShop()
    {
        List<string> baseCardIds = shopState.baseCardIds;

        for (int i = 0; i < baseSlots.Count; i++)
        {
            string cardId = "";

            if (i < baseCardIds.Count)
            {
                cardId = baseCardIds[i];
            }

            baseSlots[i].SetCard(cardId);
            baseSlots[i].slotIndex = i + 5;
        }
    }
    #endregion
}

