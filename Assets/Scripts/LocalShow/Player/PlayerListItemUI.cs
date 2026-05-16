using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text handCardNumText;
    [SerializeField] private GameObject highLight;

    private float refreshInterval = 0.2f;
    private PlayerState targetPlayer;
    private float refreshTimer = 0f;

    public int BoundPlayerIndex => targetPlayer != null ? targetPlayer.playerIndex : -1;

    private void Awake()
    {
        AutoAssignReferences();
    }

    public void Bind(PlayerState player)
    {
        AutoAssignReferences();
        targetPlayer = player;
        RefreshView();
    }

    private void Update()
    {
        if (targetPlayer == null) return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshInterval)
            return;

        refreshTimer = 0f;
        RefreshView();
    }

    public void RefreshView()
    {
        if (targetPlayer == null) return;

        AutoAssignReferences();

        nameText.text = targetPlayer.playerName;
        manaText.text = targetPlayer.GetDisplayedMana().ToString();
        attackText.text = targetPlayer.GetDisplayedAttack().ToString();
        scoreText.text = targetPlayer.GetDisplayedScore().ToString();
        handCardNumText.text = targetPlayer.GetDisplayedHandCount().ToString();
        if (highLight != null)
        {
            highLight.SetActive(targetPlayer.isMyTurn);
        }
    }

    public RectTransform GetNameTargetRect()
    {
        AutoAssignReferences();
        if (nameText != null)
            return nameText.rectTransform;

        return transform as RectTransform;
    }

    private void AutoAssignReferences()
    {
        if (nameText == null)
        {
            nameText = FindTextByName("Name");
        }

        if (manaText == null)
        {
            manaText = FindTextByName("Mana");
        }

        if (attackText == null)
        {
            attackText = FindTextByName("Attack");
        }

        if (scoreText == null)
        {
            scoreText = FindTextByName("Score");
        }

        if (handCardNumText == null)
        {
            handCardNumText = FindTextByName("Num");
        }

        if (highLight == null)
        {
            Transform highlightTransform = FindChildRecursive(transform, "Highlight");
            if (highlightTransform != null)
            {
                highLight = highlightTransform.gameObject;
            }
        }
    }

    private TMP_Text FindTextByName(string objectName)
    {
        Transform targetTransform = FindChildRecursive(transform, objectName);
        if (targetTransform == null)
            return null;

        return targetTransform.GetComponent<TMP_Text>();
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == objectName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}

public class SelectionPlayerItemUI : MonoBehaviour
{
    [SerializeField] private PlayerListItemUI playerListItemUI;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedObject;
    [SerializeField] private CanvasGroup disabledCanvasGroup;

    private Action<int> onClick;
    private int optionIndex;

    private void Awake()
    {
        AutoAssignReferences();

        if (button != null)
        {
            button.onClick.AddListener(OnClickSelf);
        }

        SetSelected(false);
        SetInteractable(true);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickSelf);
        }
    }

    public void Setup(PlayerState targetPlayer, int index, Action<int> clickCallback)
    {
        AutoAssignReferences();
        optionIndex = index;
        onClick = clickCallback;

        if (playerListItemUI != null)
        {
            playerListItemUI.Bind(targetPlayer);
        }

        if (button != null && !button.gameObject.activeSelf)
        {
            button.gameObject.SetActive(true);
        }

        SetSelected(false);
        SetInteractable(true);
    }

    public void SetSelected(bool selected)
    {
        AutoAssignReferences();
        if (selectedObject != null)
        {
            selectedObject.SetActive(selected);
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }

        if (disabledCanvasGroup != null)
        {
            disabledCanvasGroup.alpha = interactable ? 1f : 0.45f;
            disabledCanvasGroup.blocksRaycasts = interactable;
            disabledCanvasGroup.interactable = interactable;
        }
    }

    private void OnClickSelf()
    {
        onClick?.Invoke(optionIndex);
    }

    private void AutoAssignReferences()
    {
        if (playerListItemUI == null)
        {
            playerListItemUI = GetComponent<PlayerListItemUI>();
            if (playerListItemUI == null)
            {
                playerListItemUI = GetComponentInChildren<PlayerListItemUI>(true);
            }
        }

        if (button == null)
        {
            button = GetComponentInChildren<Button>(true);
        }

        if (selectedObject == null)
        {
            Transform selectedTransform = FindChildRecursive(transform, "Selection Prefab");
            if (selectedTransform == null)
            {
                selectedTransform = FindChildRecursive(transform, "Outline");
            }

            if (selectedTransform != null)
            {
                selectedObject = selectedTransform.gameObject;
            }
        }

        if (disabledCanvasGroup == null)
        {
            disabledCanvasGroup = GetComponent<CanvasGroup>();
            if (disabledCanvasGroup == null)
            {
                disabledCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == objectName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
