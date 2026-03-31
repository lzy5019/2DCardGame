using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Collections;

public class ShopPanelUI : MonoBehaviour
{
    public static ShopPanelUI Instance;

    public GameObject baseShopPanel;
    public GameObject centerShopPanel;

    public List<ShopSlotUI> baseSlots = new List<ShopSlotUI>();
    public List<ShopSlotUI> centerSlots = new List<ShopSlotUI>();

    private ShopState shopState;
    private bool hasRegisteredCallback = false;

    // 用于实现玩家商店选中功能
    public ShopSlotUI currentSelectedSlot;
    private PlayerState localPlayer = null;

    private void Awake()
    {
        Instance = this;
        GetAllSlots();
    }
    public void RegisterLocalPlayer(PlayerState player)
    {
        localPlayer = player;
    }

    private void Start()
    {
        StartCoroutine(WaitForShopState());
    }

    private System.Collections.IEnumerator WaitForShopState()
    {
        yield return new WaitUntil(() => ShopState.Instance != null);

        shopState = ShopState.Instance;
        RegisterCallback();
        RefreshBaseShop();
        RefreshCenterShop();
    }

    private void OnDestroy()
    {
        UnregisterCallback();
    }
    private void GetAllSlots()      // 抓取所有slot
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

    #region 监听事件触发函数
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

    public void OnSlotClicked(ShopSlotUI clickedSlot)
    {
        if (clickedSlot == null || clickedSlot.card == null) return;

        // 第一次点击：选中
        if (currentSelectedSlot != clickedSlot)
        {
            if (currentSelectedSlot != null)
                currentSelectedSlot.SetSelected(false);

            currentSelectedSlot = clickedSlot;
            currentSelectedSlot.SetSelected(true);
            return;
        }
         
        // 第二次点击：购买
        if (localPlayer == null) return;

        localPlayer.RequestBuyCard(clickedSlot.slotIndex);
        currentSelectedSlot.SetSelected(false);
        currentSelectedSlot = null;
    }
    #endregion
    private void RefreshCenterShop()        // 刷新商店显示
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
            baseSlots[i].slotIndex = i+5;
        }
    }
}