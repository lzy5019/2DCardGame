using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class EquipmentZoneUI : MonoBehaviour
{
    public static EquipmentZoneUI Instance;

    [Header("基础引用")]
    [SerializeField] private Transform weaponRoot;
    [SerializeField] private Transform equipmentContentRoot;
    [SerializeField] private GameObject cardPrefab;

    [Header("可选")]
    [SerializeField] private GameObject emptyHintObject;

    private PlayerState localPlayer;

    private readonly List<GameObject> spawnedEquipmentCards = new List<GameObject>();
    private GameObject spawnedWeaponCard;

    private string cachedWeaponCardId = "";
    private bool cachedWeaponUsed = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterCurrentPlayer();
    }

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

    private void OnEquippedCardsChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        RefreshEquipmentList();
    }

    private void OnEquippedUsedFlagsChanged(SyncList<bool>.Operation op, int index, bool oldItem, bool newItem)
    {
        RefreshCardStatesOnly();
    }

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

        if (localPlayer == null || equipmentContentRoot == null || cardPrefab == null)
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

            GameObject obj = Instantiate(cardPrefab, equipmentContentRoot);
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
}
