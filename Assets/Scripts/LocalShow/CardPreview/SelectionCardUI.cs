using System;
using UnityEngine;
using UnityEngine.UI;

public class SelectionCardUI : MonoBehaviour
{
    public Image cardImage;
    public GameObject outlineObject;
    public Button button;

    public int optionIndex;

    private Action<int> onClick;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnClickSelf);
        }

        SetSelected(false);
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

    private void OnClickSelf()
    {
        onClick?.Invoke(optionIndex);
    }
}
