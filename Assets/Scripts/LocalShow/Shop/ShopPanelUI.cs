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
    [SerializeField] private RectTransform buyFxTarget;
    [SerializeField] private ShopPurchaseFxUI purchaseFxPrefab;
    [SerializeField] private ShopDefeatFxUI defeatFxPrefab;
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

        if (card.cardType == CardType.Enemy)
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

    #region 本地特效
    public void PlayLocalShopResultFx(int slotIndex, string cardId, bool isBaseShop)
    {
        if (string.IsNullOrEmpty(cardId))
            return;

        CardData card = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetCardById(cardId)
            : null;

        if (card == null || card.cardSprite == null)
            return;

        ShopSlotUI targetSlot = GetTargetSlot(slotIndex, isBaseShop);
        if (targetSlot == null)
            return;

        RectTransform targetSlotRect = targetSlot.transform as RectTransform;
        if (targetSlotRect == null)
            return;

        if (card.cardType == CardType.Enemy)
        {
            if (defeatFxPrefab == null)
                return;

            ShopDefeatFxUI defeatFx = Instantiate(defeatFxPrefab, targetSlotRect, false);
            PrepareSpawnedFxRect(defeatFx.transform as RectTransform);
            StartCoroutine(PlayDefeatFxAndDestroy(defeatFx, card.cardSprite));
            return;
        }

        if (buyFxTarget == null || purchaseFxPrefab == null)
            return;

        ShopPurchaseFxUI purchaseFx = Instantiate(purchaseFxPrefab, targetSlotRect, false);
        PrepareSpawnedFxRect(purchaseFx.transform as RectTransform);
        StartCoroutine(PlayPurchaseFxAndDestroy(purchaseFx, card.cardSprite, buyFxTarget));
    }

    private ShopSlotUI GetTargetSlot(int slotIndex, bool isBaseShop)
    {
        List<ShopSlotUI> slotList = isBaseShop ? baseSlots : centerSlots;
        if (slotList == null)
            return null;

        if (slotIndex < 0 || slotIndex >= slotList.Count)
            return null;

        return slotList[slotIndex];
    }

    private IEnumerator PlayPurchaseFxAndDestroy(ShopPurchaseFxUI purchaseFx, Sprite cardSprite, RectTransform targetRect)
    {
        if (purchaseFx == null)
            yield break;

        yield return purchaseFx.PlayToTargetRoutine(cardSprite, targetRect);

        if (purchaseFx != null)
        {
            Destroy(purchaseFx.gameObject);
        }
    }

    private IEnumerator PlayDefeatFxAndDestroy(ShopDefeatFxUI defeatFx, Sprite cardSprite)
    {
        if (defeatFx == null)
            yield break;

        yield return defeatFx.PlayDefeatRoutine(cardSprite);

        if (defeatFx != null)
        {
            Destroy(defeatFx.gameObject);
        }
    }

    private void PrepareSpawnedFxRect(RectTransform fxRect)
    {
        if (fxRect == null)
            return;

        fxRect.anchorMin = Vector2.zero;
        fxRect.anchorMax = Vector2.one;
        fxRect.offsetMin = Vector2.zero;
        fxRect.offsetMax = Vector2.zero;
        fxRect.anchoredPosition = Vector2.zero;
        fxRect.localScale = Vector3.one;
        fxRect.localRotation = Quaternion.identity;
        fxRect.SetAsLastSibling();
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

