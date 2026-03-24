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
        previewCanvas.SetActive(false);
    }

    public void ShowPreview(Sprite cardSprite)
    {
        previewCanvas.SetActive(true);
        previewImage.sprite = cardSprite;
    }

    public void HidePreview()
    {
        previewCanvas.SetActive(false);
    }
}