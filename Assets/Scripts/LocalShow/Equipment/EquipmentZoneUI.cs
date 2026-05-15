using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class EquipmentZoneUI : MonoBehaviour
{
    public static EquipmentZoneUI Instance;

    #region 界面引用
    [Header("界面引用")]
    [SerializeField] private Transform weaponRoot;
    [SerializeField] private Transform equipmentContentRoot;
    [SerializeField] private Transform equipmentContentRoot2;
    [SerializeField] private Transform equipmentContentRoot3;
    [SerializeField] private GameObject cardPrefab;

    [Header("可选对象")]
    [SerializeField] private GameObject emptyHintObject;
    #endregion

    #region 玩家状态
    private PlayerState localPlayer;
    private readonly List<GameObject> spawnedEquipmentCards = new List<GameObject>();
    private GameObject spawnedWeaponCard;
    private string cachedWeaponCardId = "";
    private bool cachedWeaponUsed = false;
    #endregion

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (localPlayer == null)
            return;

        bool weaponChanged = cachedWeaponCardId != localPlayer.equippedWeaponCardId
            || cachedWeaponUsed != localPlayer.equippedWeaponUsed;

        if (weaponChanged)
        {
            cachedWeaponCardId = localPlayer.equippedWeaponCardId;
            cachedWeaponUsed = localPlayer.equippedWeaponUsed;
            RefreshWeapon();
        }

        RefreshCardStatesOnly();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterCurrentPlayer();
    }
    #endregion

    #region 注册
    public void RegisterLocalPlayer(PlayerState player)
    {
        if (player == null)
            return;

        if (localPlayer == player)
            return;

        UnregisterCurrentPlayer();

        localPlayer = player;
        localPlayer.equippedCardIds.Callback += OnEquippedCardsChanged;
        localPlayer.equippedCardUsedFlags.Callback += OnEquippedUsedFlagsChanged;

        cachedWeaponCardId = localPlayer.equippedWeaponCardId;
        cachedWeaponUsed = localPlayer.equippedWeaponUsed;

        RefreshAll();
    }

    public void UnregisterCurrentPlayer()
    {
        if (localPlayer != null)
        {
            localPlayer.equippedCardIds.Callback -= OnEquippedCardsChanged;
            localPlayer.equippedCardUsedFlags.Callback -= OnEquippedUsedFlagsChanged;
            localPlayer = null;
        }

        ClearAll();
    }
    #endregion

    #region 按钮请求
    public void RequestUseWeaponFromUI()
    {
        if (localPlayer == null)
            return;

        localPlayer.RequestUseWeapon();
    }

    public void RequestUseEquipmentFromUI(int equipmentIndex)
    {
        if (localPlayer == null)
            return;

        localPlayer.RequestUseEquipment(equipmentIndex);
    }
    #endregion

    #region 同步回调
    private void OnEquippedCardsChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        RefreshEquipmentList();
    }

    private void OnEquippedUsedFlagsChanged(SyncList<bool>.Operation op, int index, bool oldItem, bool newItem)
    {
        RefreshCardStatesOnly();
    }
    #endregion

    #region 渲染
    private void RefreshAll()
    {
        RefreshWeapon();
        RefreshEquipmentList();
        RefreshEmptyHint();
    }

    private void RefreshWeapon()
    {
        if (spawnedWeaponCard != null)
        {
            Destroy(spawnedWeaponCard);
            spawnedWeaponCard = null;
        }

        if (localPlayer == null || string.IsNullOrEmpty(localPlayer.equippedWeaponCardId))
        {
            RefreshEmptyHint();
            return;
        }

        if (weaponRoot == null || cardPrefab == null)
            return;

        CardData cardData = CardDatabase.Instance.GetCardById(localPlayer.equippedWeaponCardId);
        if (cardData == null)
            return;

        spawnedWeaponCard = Instantiate(cardPrefab, weaponRoot);

        EquipmentCardUI cardUI = spawnedWeaponCard.GetComponent<EquipmentCardUI>();
        if (cardUI != null)
        {
            cardUI.Setup(
                localPlayer.equippedWeaponCardId,
                cardData,
                true,
                -1,
                localPlayer.equippedWeaponUsed,
                CanUseWeapon()
            );
        }

        RefreshEmptyHint();
    }

    private void RefreshEquipmentList()
    {
        ClearEquipmentCards();

        if (localPlayer == null || cardPrefab == null || GetFirstValidEquipmentRoot() == null)
        {
            RefreshEmptyHint();
            return;
        }

        for (int i = 0; i < localPlayer.equippedCardIds.Count; i++)
        {
            string cardId = localPlayer.equippedCardIds[i];
            if (string.IsNullOrEmpty(cardId))
                continue;

            CardData cardData = CardDatabase.Instance.GetCardById(cardId);
            if (cardData == null)
                continue;

            Transform parentRoot = GetEquipmentRootByDisplayIndex(i);
            if (parentRoot == null)
                continue;

            GameObject obj = Instantiate(cardPrefab, parentRoot);
            spawnedEquipmentCards.Add(obj);

            EquipmentCardUI cardUI = obj.GetComponent<EquipmentCardUI>();
            if (cardUI != null)
            {
                bool isUsed = i < localPlayer.equippedCardUsedFlags.Count && localPlayer.equippedCardUsedFlags[i];
                cardUI.Setup(
                    cardId,
                    cardData,
                    false,
                    i,
                    isUsed,
                    CanUseEquipment(i)
                );
            }
        }

        RefreshEmptyHint();
    }

    private void RefreshCardStatesOnly()
    {
        if (localPlayer == null)
            return;

        if (spawnedWeaponCard != null)
        {
            EquipmentCardUI weaponUI = spawnedWeaponCard.GetComponent<EquipmentCardUI>();
            if (weaponUI != null)
            {
                weaponUI.SetUsed(localPlayer.equippedWeaponUsed);
                weaponUI.SetHighlight(CanUseWeapon());
            }
        }

        for (int i = 0; i < spawnedEquipmentCards.Count; i++)
        {
            if (spawnedEquipmentCards[i] == null)
                continue;

            EquipmentCardUI cardUI = spawnedEquipmentCards[i].GetComponent<EquipmentCardUI>();
            if (cardUI == null)
                continue;

            bool isUsed = i < localPlayer.equippedCardUsedFlags.Count && localPlayer.equippedCardUsedFlags[i];
            cardUI.SetUsed(isUsed);
            cardUI.SetHighlight(CanUseEquipment(i));
        }
    }
    #endregion

    #region 可用性检查
    private bool CanUseWeapon()
    {
        if (localPlayer == null) return false;
        if (!localPlayer.isMyTurn) return false;
        if (string.IsNullOrEmpty(localPlayer.equippedWeaponCardId)) return false;
        if (localPlayer.equippedWeaponUsed) return false;
        return true;
    }

    private bool CanUseEquipment(int index)
    {
        if (localPlayer == null) return false;
        if (!localPlayer.isMyTurn) return false;
        if (index < 0 || index >= localPlayer.equippedCardIds.Count) return false;
        if (index < localPlayer.equippedCardUsedFlags.Count && localPlayer.equippedCardUsedFlags[index]) return false;
        return true;
    }
    #endregion

    #region 清理
    private void RefreshEmptyHint()
    {
        if (emptyHintObject == null)
            return;

        bool hasWeapon = localPlayer != null && !string.IsNullOrEmpty(localPlayer.equippedWeaponCardId);
        bool hasEquipment = localPlayer != null && localPlayer.equippedCardIds.Count > 0;

        emptyHintObject.SetActive(!hasWeapon && !hasEquipment);
    }

    private void ClearEquipmentCards()
    {
        for (int i = 0; i < spawnedEquipmentCards.Count; i++)
        {
            if (spawnedEquipmentCards[i] != null)
            {
                Destroy(spawnedEquipmentCards[i]);
            }
        }

        spawnedEquipmentCards.Clear();
    }

    private Transform GetEquipmentRootByDisplayIndex(int equipmentDisplayIndex)
    {
        Transform[] roots =
        {
            equipmentContentRoot,
            equipmentContentRoot2,
            equipmentContentRoot3
        };

        if (equipmentDisplayIndex >= 0)
        {
            Transform targetRoot = roots[equipmentDisplayIndex % roots.Length];
            if (targetRoot != null)
            {
                return targetRoot;
            }
        }

        return GetFirstValidEquipmentRoot();
    }

    private Transform GetFirstValidEquipmentRoot()
    {
        if (equipmentContentRoot != null)
            return equipmentContentRoot;
        if (equipmentContentRoot2 != null)
            return equipmentContentRoot2;
        if (equipmentContentRoot3 != null)
            return equipmentContentRoot3;

        return null;
    }

    private void ClearAll()
    {
        ClearEquipmentCards();

        if (spawnedWeaponCard != null)
        {
            Destroy(spawnedWeaponCard);
            spawnedWeaponCard = null;
        }

        if (emptyHintObject != null)
        {
            emptyHintObject.SetActive(false);
        }
    }
    #endregion
}

