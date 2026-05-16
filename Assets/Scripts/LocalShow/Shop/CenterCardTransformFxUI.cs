using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CenterCardTransformFxUI : MonoBehaviour
{
    public static CenterCardTransformFxUI Instance;

    [Header("Transform FX")]
    [SerializeField] private float moveToFocusDuration = 0.35f;
    [SerializeField] private float holdBeforeSwapDuration = 0.16f;
    [SerializeField] private float swapPulseDuration = 0.18f;
    [SerializeField] private float holdAfterSwapDuration = 0.16f;
    [SerializeField] private float moveBackDuration = 0.2f;
    [SerializeField] private float focusScaleMultiplier = 1.1f;
    [SerializeField] private float swapScaleMultiplier = 1.22f;
    [SerializeField] private RectTransform focusTargetOverride;

    private readonly Dictionary<int, Coroutine> activeRoutineBySlotId = new Dictionary<int, Coroutine>();
    private readonly Dictionary<int, GameObject> activeOverlayBySlotId = new Dictionary<int, GameObject>();

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

    public static bool TryPlay(ShopSlotUI slot, string oldCardId, string newCardId, Action onSwap)
    {
        if (slot == null)
            return false;
        if (CardDatabase.Instance == null)
            return false;
        if (string.IsNullOrEmpty(oldCardId) || string.IsNullOrEmpty(newCardId))
            return false;

        CardData oldCardData = CardDatabase.Instance.GetCardById(oldCardId);
        CardData newCardData = CardDatabase.Instance.GetCardById(newCardId);
        if (oldCardData == null || oldCardData.cardSprite == null)
            return false;
        if (newCardData == null || newCardData.cardSprite == null)
            return false;

        CenterCardTransformFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        fx.StartTransform(slot, oldCardData.cardSprite, newCardData.cardSprite, onSwap);
        return true;
    }

    private static CenterCardTransformFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject fxObject = new GameObject("CenterCardTransformFxUI");
        return fxObject.AddComponent<CenterCardTransformFxUI>();
    }

    private void StartTransform(ShopSlotUI slot, Sprite oldSprite, Sprite newSprite, Action onSwap)
    {
        int slotId = slot.GetInstanceID();
        StopActiveRoutine(slotId);
        activeRoutineBySlotId[slotId] = StartCoroutine(PlayRoutine(slot, slotId, oldSprite, newSprite, onSwap));
    }

    private void StopActiveRoutine(int slotId)
    {
        if (activeRoutineBySlotId.TryGetValue(slotId, out Coroutine activeRoutine) && activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutineBySlotId.Remove(slotId);

        if (activeOverlayBySlotId.TryGetValue(slotId, out GameObject overlayObject) && overlayObject != null)
        {
            Destroy(overlayObject);
        }

        activeOverlayBySlotId.Remove(slotId);
    }

    private IEnumerator PlayRoutine(ShopSlotUI slot, int slotId, Sprite oldSprite, Sprite newSprite, Action onSwap)
    {
        if (slot == null || slot.cardImage == null)
        {
            onSwap?.Invoke();
            CleanupSlotEntry(slotId);
            yield break;
        }

        RectTransform sourceRect = slot.cardImage.rectTransform;
        if (sourceRect == null)
        {
            onSwap?.Invoke();
            CleanupSlotEntry(slotId);
            yield break;
        }

        Canvas rootCanvas = GetRootCanvas(slot.transform);
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            onSwap?.Invoke();
            CleanupSlotEntry(slotId);
            yield break;
        }

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Image sourceImage = slot.cardImage;

        GameObject overlayObject = new GameObject(
            "CenterCardTransformOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        activeOverlayBySlotId[slotId] = overlayObject;

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
        Vector2 focusPosition = GetFocusPosition(canvasRect, sourceRect, uiCamera);
        overlayRect.anchoredPosition = startPosition;

        bool originalImageEnabled = sourceImage.enabled;
        sourceImage.enabled = false;

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
        if (sourceImage != null)
        {
            sourceImage.enabled = false;
        }

        overlayImage.sprite = newSprite;
        yield return AnimateFlash(overlayImage, overlayRect, swapScale, focusScale, swapPulseDuration * 0.5f);

        if (holdAfterSwapDuration > 0f)
        {
            yield return new WaitForSeconds(holdAfterSwapDuration);
        }

        yield return AnimateOverlay(overlayRect, focusPosition, startPosition, focusScale, startScale, moveBackDuration);

        if (sourceImage != null)
        {
            sourceImage.enabled = originalImageEnabled;
        }

        if (overlayObject != null)
        {
            Destroy(overlayObject);
        }

        CleanupSlotEntry(slotId);
    }

    private void CleanupSlotEntry(int slotId)
    {
        activeRoutineBySlotId.Remove(slotId);
        activeOverlayBySlotId.Remove(slotId);
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

    private IEnumerator AnimateFlash(Image overlayImage, RectTransform overlayRect, Vector3 fromScale, Vector3 toScale, float duration)
    {
        if (overlayImage == null || overlayRect == null)
            yield break;

        Color originalColor = overlayImage.color;
        Color flashColor = Color.white;

        if (duration <= 0f)
        {
            overlayRect.localScale = toScale;
            overlayImage.color = originalColor;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutCubic(t);
            overlayRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedT);
            overlayImage.color = Color.LerpUnclamped(flashColor, originalColor, easedT);
            yield return null;
        }

        overlayRect.localScale = toScale;
        overlayImage.color = originalColor;
    }

    private Vector2 GetFocusPosition(RectTransform canvasRect, RectTransform sourceRect, Camera uiCamera)
    {
        if (focusTargetOverride != null)
            return WorldToCanvasPosition(canvasRect, focusTargetOverride, uiCamera);

        float defaultY = canvasRect.rect.height * 0.14f;
        return new Vector2(0f, defaultY);
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
