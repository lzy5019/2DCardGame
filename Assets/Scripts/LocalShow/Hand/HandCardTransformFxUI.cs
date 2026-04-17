using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HandCardTransformFxUI : MonoBehaviour
{
    public static HandCardTransformFxUI Instance;

    [Header("Transform FX")]
    [SerializeField] private float moveToFocusDuration = 0.4f;
    [SerializeField] private float holdBeforeSwapDuration = 0.2f;
    [SerializeField] private float swapPulseDuration = 0.2f;
    [SerializeField] private float holdAfterSwapDuration = 0.2f;
    [SerializeField] private float moveBackDuration = 0.2f;
    [SerializeField] private float focusScaleMultiplier = 1.15f;
    [SerializeField] private float swapScaleMultiplier = 1.28f;
    [SerializeField] private RectTransform focusTargetOverride;

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
    }

    public static bool TryPlay(GameObject sourceCardObject, string oldCardId, string newCardId, Action onSwap)
    {
        if (sourceCardObject == null)
            return false;
        if (CardDatabase.Instance == null)
            return false;

        Image sourceImage = sourceCardObject.GetComponent<Image>();
        if (sourceImage == null)
            return false;

        CardData newCardData = CardDatabase.Instance.GetCardById(newCardId);
        if (newCardData == null || newCardData.cardSprite == null)
            return false;

        Sprite oldSprite = sourceImage.sprite;
        if (oldSprite == null)
        {
            CardData oldCardData = CardDatabase.Instance.GetCardById(oldCardId);
            if (oldCardData == null || oldCardData.cardSprite == null)
                return false;

            oldSprite = oldCardData.cardSprite;
        }

        HandCardTransformFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        fx.StartCoroutine(fx.PlayRoutine(sourceCardObject, oldSprite, newCardData.cardSprite, onSwap));
        return true;
    }

    private static HandCardTransformFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject fxObject = new GameObject("HandCardTransformFxUI");
        return fxObject.AddComponent<HandCardTransformFxUI>();
    }

    private IEnumerator PlayRoutine(GameObject sourceCardObject, Sprite oldSprite, Sprite newSprite, Action onSwap)
    {
        if (sourceCardObject == null)
            yield break;

        RectTransform sourceRect = sourceCardObject.GetComponent<RectTransform>();
        Image sourceImage = sourceCardObject.GetComponent<Image>();
        HandCardUI handCardUI = sourceCardObject.GetComponent<HandCardUI>();
        CardPreviewTrigger previewTrigger = sourceCardObject.GetComponent<CardPreviewTrigger>();
        if (sourceRect == null || sourceImage == null)
        {
            onSwap?.Invoke();
            yield break;
        }

        Canvas rootCanvas = GetRootCanvas(sourceCardObject.transform);
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            onSwap?.Invoke();
            yield break;
        }

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        GameObject overlayObject = new GameObject(
            "HandCardTransformOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(canvasRect, false);
        overlayRect.SetAsLastSibling();
        overlayRect.sizeDelta = sourceRect.rect.size;
        overlayRect.localScale = GetRelativeScale(sourceRect, canvasRect);

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.sprite = oldSprite;
        overlayImage.preserveAspect = true;
        overlayImage.raycastTarget = false;

        Vector2 startPosition = WorldToCanvasPosition(canvasRect, sourceRect, uiCamera);
        Vector2 focusPosition = GetFocusPosition(canvasRect, uiCamera);
        overlayRect.anchoredPosition = startPosition;

        CanvasGroup sourceCanvasGroup = sourceCardObject.GetComponent<CanvasGroup>();
        bool hadCanvasGroup = sourceCanvasGroup != null;
        if (!hadCanvasGroup)
        {
            sourceCanvasGroup = sourceCardObject.AddComponent<CanvasGroup>();
        }

        bool originalImageEnabled = sourceImage.enabled;
        bool originalHandCardUIEnabled = handCardUI != null && handCardUI.enabled;
        bool originalPreviewEnabled = previewTrigger != null && previewTrigger.enabled;
        float originalAlpha = sourceCanvasGroup.alpha;
        bool originalBlocksRaycasts = sourceCanvasGroup.blocksRaycasts;
        bool originalInteractable = sourceCanvasGroup.interactable;

        sourceImage.enabled = false;
        sourceCanvasGroup.alpha = 0f;
        sourceCanvasGroup.blocksRaycasts = false;
        sourceCanvasGroup.interactable = false;

        if (handCardUI != null)
        {
            handCardUI.isHovering = false;
            handCardUI.isDragging = false;
            handCardUI.enabled = false;
        }

        if (previewTrigger != null)
        {
            previewTrigger.enabled = false;
        }

        Vector3 startScale = overlayRect.localScale;
        Vector3 focusScale = startScale * focusScaleMultiplier;
        Vector3 swapScale = startScale * swapScaleMultiplier;

        yield return AnimateOverlay(overlayRect, startPosition, focusPosition, startScale, focusScale, moveToFocusDuration);

        if (holdBeforeSwapDuration > 0f)
        {
            yield return new WaitForSeconds(holdBeforeSwapDuration);
        }

        yield return AnimateScale(overlayRect, focusScale, swapScale, swapPulseDuration * 0.5f);

        onSwap?.Invoke();
        overlayImage.sprite = newSprite;

        yield return AnimateScale(overlayRect, swapScale, focusScale, swapPulseDuration * 0.5f);

        if (holdAfterSwapDuration > 0f)
        {
            yield return new WaitForSeconds(holdAfterSwapDuration);
        }

        yield return AnimateOverlay(overlayRect, focusPosition, startPosition, focusScale, startScale, moveBackDuration);

        if (overlayObject != null)
        {
            Destroy(overlayObject);
        }

        if (sourceImage != null)
        {
            sourceImage.enabled = originalImageEnabled;
        }

        if (sourceCanvasGroup != null)
        {
            sourceCanvasGroup.alpha = originalAlpha;
            sourceCanvasGroup.blocksRaycasts = originalBlocksRaycasts;
            sourceCanvasGroup.interactable = originalInteractable;

            if (!hadCanvasGroup)
            {
                Destroy(sourceCanvasGroup);
            }
        }

        if (previewTrigger != null)
        {
            previewTrigger.enabled = originalPreviewEnabled;
        }

        if (handCardUI != null)
        {
            handCardUI.enabled = originalHandCardUIEnabled;

            if (handCardUI.handDisplayManager != null)
            {
                handCardUI.handDisplayManager.UpdateHandLayout();
            }
        }
    }

    private IEnumerator AnimateOverlay(
        RectTransform overlayRect,
        Vector2 fromPosition,
        Vector2 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        float duration)
    {
        if (overlayRect == null)
            yield break;

        if (duration <= 0f)
        {
            overlayRect.anchoredPosition = toPosition;
            overlayRect.localScale = toScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutCubic(t);
            overlayRect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, easedT);
            overlayRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedT);
            yield return null;
        }

        overlayRect.anchoredPosition = toPosition;
        overlayRect.localScale = toScale;
    }

    private IEnumerator AnimateScale(RectTransform overlayRect, Vector3 fromScale, Vector3 toScale, float duration)
    {
        if (overlayRect == null)
            yield break;

        if (duration <= 0f)
        {
            overlayRect.localScale = toScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutCubic(t);
            overlayRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedT);
            yield return null;
        }

        overlayRect.localScale = toScale;
    }

    private Vector2 GetFocusPosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (focusTargetOverride == null)
            return Vector2.zero;

        return WorldToCanvasPosition(canvasRect, focusTargetOverride, uiCamera);
    }

    private static Vector2 WorldToCanvasPosition(RectTransform canvasRect, RectTransform targetRect, Camera uiCamera)
    {
        Vector3 worldCenter = targetRect.TransformPoint(targetRect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCenter);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            return localPoint;

        return Vector2.zero;
    }

    private static Vector3 GetRelativeScale(RectTransform sourceRect, RectTransform canvasRect)
    {
        Vector3 sourceLossyScale = sourceRect.lossyScale;
        Vector3 canvasLossyScale = canvasRect.lossyScale;

        float x = Mathf.Approximately(canvasLossyScale.x, 0f) ? 1f : sourceLossyScale.x / canvasLossyScale.x;
        float y = Mathf.Approximately(canvasLossyScale.y, 0f) ? 1f : sourceLossyScale.y / canvasLossyScale.y;
        float z = Mathf.Approximately(canvasLossyScale.z, 0f) ? 1f : sourceLossyScale.z / canvasLossyScale.z;
        return new Vector3(x, y, z);
    }

    private static Canvas GetRootCanvas(Transform target)
    {
        Canvas[] canvases = target.GetComponentsInParent<Canvas>(true);
        if (canvases == null || canvases.Length == 0)
            return null;

        return canvases[canvases.Length - 1];
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }
}
