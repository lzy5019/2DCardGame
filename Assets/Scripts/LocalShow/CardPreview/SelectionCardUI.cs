using System;
using UnityEngine;
using UnityEngine.UI;

public class SelectionCardUI : MonoBehaviour
{
    public Image cardImage;
    public GameObject outlineObject;
    public Button button;
    public CanvasGroup canvasGroup;

    public int optionIndex;

    private Action<int> onClick;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

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

    public void Setup(Sprite sprite, int index, Action<int> clickCallback)
    {
        optionIndex = index;
        onClick = clickCallback;

        if (cardImage != null)
        {
            cardImage.sprite = sprite;
            cardImage.preserveAspect = true;
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (outlineObject != null)
        {
            outlineObject.SetActive(selected);
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = interactable ? 1f : 0.75f;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }
    }

    private void OnClickSelf()
    {
        onClick?.Invoke(optionIndex);
    }
}
