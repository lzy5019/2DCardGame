using UnityEngine;
using UnityEngine.UI;

public class CardPreviewManager : MonoBehaviour
{
    public static CardPreviewManager Instance;

    public GameObject previewCanvas;
    public Image previewImage;

    private void Awake()
    {
        Instance = this;
        HidePreview();
    }

    public void ShowPreview(Sprite sprite)
    {
        if (sprite == null) return;

        previewCanvas.SetActive(true);
        previewImage.sprite = sprite;
        previewImage.enabled = true;
    }

    public void HidePreview()
    {
        previewCanvas.SetActive(false);
    }
}