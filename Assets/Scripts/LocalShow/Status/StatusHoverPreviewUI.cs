using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatusHoverPreviewUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Preview Timing")]
    [SerializeField] private float hoverDelaySeconds = 0.4f;

    [Header("Preview Layout")]
    [SerializeField] private RectTransform previewRoot;
    [SerializeField] private Vector2 previewSize = new Vector2(300f, 420f);
    [SerializeField] private Vector2 previewOffset = Vector2.zero;

    [Header("Fallback Source")]
    [SerializeField] private Image fallbackSourceImage;

    private Coroutine hoverCoroutine;
    private RectTransform previewRectTransform;
    private Image previewImage;
    private Vector2 latestPointerScreenPosition;
    private bool pointerInside;

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        latestPointerScreenPosition = eventData.position;
        RestartHoverCoroutine();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        latestPointerScreenPosition = eventData.position;

        if (previewRectTransform != null)
        {
            UpdatePreviewPosition();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        StopHoverCoroutine();
        DestroyPreviewObject();
    }

    private void OnDisable()
    {
        pointerInside = false;
        StopHoverCoroutine();
        DestroyPreviewObject();
    }

    private void RestartHoverCoroutine()
    {
        StopHoverCoroutine();
        hoverCoroutine = StartCoroutine(ShowPreviewAfterDelay());
    }

    private void StopHoverCoroutine()
    {
        if (hoverCoroutine == null)
            return;

        StopCoroutine(hoverCoroutine);
        hoverCoroutine = null;
    }

    private IEnumerator ShowPreviewAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hoverDelaySeconds));
        hoverCoroutine = null;

        if (!pointerInside)
            yield break;

        Sprite previewSprite = ResolvePreviewSprite();
        if (previewSprite == null)
            yield break;

        EnsurePreviewObject(previewSprite);
        UpdatePreviewPosition();
    }

    private void EnsurePreviewObject(Sprite previewSprite)
    {
        if (previewRectTransform == null)
        {
            RectTransform parentRoot = ResolvePreviewRoot();
            if (parentRoot == null)
                return;

            GameObject previewObject = new GameObject("StatusHoverPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewRectTransform = previewObject.GetComponent<RectTransform>();
            previewImage = previewObject.GetComponent<Image>();

            previewRectTransform.SetParent(parentRoot, false);
            previewRectTransform.anchorMin = Vector2.zero;
            previewRectTransform.anchorMax = Vector2.zero;
            previewRectTransform.pivot = new Vector2(1f, 0f);
            previewRectTransform.sizeDelta = previewSize;

            previewImage.raycastTarget = false;
            previewImage.preserveAspect = true;
        }

        if (previewImage != null)
        {
            previewImage.sprite = previewSprite;
            previewImage.enabled = previewSprite != null;
        }

        previewRectTransform.SetAsLastSibling();
    }

    private void UpdatePreviewPosition()
    {
        RectTransform parentRoot = previewRectTransform != null ? previewRectTransform.parent as RectTransform : null;
        if (previewRectTransform == null || parentRoot == null)
            return;

        Camera uiCamera = ResolveUiCamera(parentRoot);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRoot, latestPointerScreenPosition, uiCamera, out Vector2 localPoint))
            return;

        Vector2 bottomLeftAnchoredPoint = localPoint + new Vector2(
            parentRoot.rect.width * parentRoot.pivot.x,
            parentRoot.rect.height * parentRoot.pivot.y);

        previewRectTransform.anchoredPosition = bottomLeftAnchoredPoint + previewOffset;
    }

    private Sprite ResolvePreviewSprite()
    {
        StatusItemUI statusItemUI = GetComponent<StatusItemUI>();
        if (statusItemUI != null && statusItemUI.CardData != null && statusItemUI.CardData.cardSprite != null)
            return statusItemUI.CardData.cardSprite;

        if (fallbackSourceImage != null && fallbackSourceImage.sprite != null)
            return fallbackSourceImage.sprite;

        Image localImage = GetComponent<Image>();
        if (localImage != null && localImage.sprite != null)
            return localImage.sprite;

        return null;
    }

    private RectTransform ResolvePreviewRoot()
    {
        if (previewRoot != null)
            return previewRoot;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            return null;

        Canvas rootCanvas = parentCanvas.rootCanvas != null ? parentCanvas.rootCanvas : parentCanvas;
        return rootCanvas.transform as RectTransform;
    }

    private Camera ResolveUiCamera(RectTransform parentRoot)
    {
        Canvas parentCanvas = parentRoot.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            return null;
        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return parentCanvas.worldCamera;
    }

    private void DestroyPreviewObject()
    {
        if (previewRectTransform == null)
            return;

        Destroy(previewRectTransform.gameObject);
        previewRectTransform = null;
        previewImage = null;
    }
}
