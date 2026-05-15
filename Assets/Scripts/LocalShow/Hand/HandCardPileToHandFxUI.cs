using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum HandCardPileToHandFxSourceType
{
    DiscardPile = 0,
    DrawPileSearch = 1,
    Generated = 2
}

public class HandCardPileToHandFxUI : MonoBehaviour
{
    public static HandCardPileToHandFxUI Instance;

    [Header("Pile To Hand FX")]
    [SerializeField] private float moveFromSourceDuration = 0.22f;
    [SerializeField] private float holdAtShowcaseDuration = 0.18f;
    [SerializeField] private float moveToHandDuration = 0.24f;
    [SerializeField] private float showcaseScaleMultiplier = 1.12f;

    [Header("Origins")]
    [SerializeField] private RectTransform discardOriginOverride;
    [SerializeField] private RectTransform drawPileSearchOriginOverride;
    [SerializeField] private RectTransform generatedOriginOverride;

    [Header("Showcase")]
    [SerializeField] private RectTransform showcaseTargetOverride;

    private readonly Queue<PileToHandFxRequest> pendingRequests = new Queue<PileToHandFxRequest>();
    private Coroutine queueRoutine;

    public static bool IsBusy
    {
        get
        {
            return Instance != null && (Instance.queueRoutine != null || Instance.pendingRequests.Count > 0);
        }
    }

    private sealed class PileToHandFxRequest
    {
        public HandCardPileToHandFxSourceType sourceType;
        public string cardId;
        public GameObject targetCardObject;
        public Sprite cardSprite;
        public CanvasGroup targetCanvasGroup;
        public bool hadCanvasGroup;
        public float originalAlpha;
        public bool originalBlocksRaycasts;
        public bool originalInteractable;
        public bool originalPreviewEnabled;
        public RectTransform canvasRect;
        public Camera uiCamera;
        public Vector2 overlaySize;
        public Vector3 overlayStartScale;
        public Vector2 fallbackTargetPosition;
        public Action onStarted;
        public Action onResolved;
    }

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

    public static bool TryQueue(
        GameObject targetCardObject,
        string cardId,
        HandCardPileToHandFxSourceType sourceType = HandCardPileToHandFxSourceType.DiscardPile,
        Action onStarted = null,
        Action onResolved = null)
    {
        if (targetCardObject == null)
            return false;
        if (CardDatabase.Instance == null)
            return false;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null || cardData.cardSprite == null)
            return false;

        HandCardPileToHandFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        PileToHandFxRequest request = fx.CreateRequest(targetCardObject, cardId, cardData.cardSprite, sourceType, onStarted, onResolved);
        if (request == null)
            return false;

        fx.pendingRequests.Enqueue(request);
        if (fx.queueRoutine == null)
        {
            fx.queueRoutine = fx.StartCoroutine(fx.PlayQueueRoutine());
        }

        return true;
    }

    private static HandCardPileToHandFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject fxObject = new GameObject("HandCardPileToHandFxUI");
        return fxObject.AddComponent<HandCardPileToHandFxUI>();
    }

    private PileToHandFxRequest CreateRequest(
        GameObject targetCardObject,
        string cardId,
        Sprite cardSprite,
        HandCardPileToHandFxSourceType sourceType,
        Action onStarted,
        Action onResolved)
    {
        if (targetCardObject == null || cardSprite == null)
            return null;

        RectTransform targetRect = targetCardObject.GetComponent<RectTransform>();
        if (targetRect == null)
            return null;

        Canvas rootCanvas = GetRootCanvas(targetCardObject.transform);
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return null;

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        PileToHandFxRequest request = new PileToHandFxRequest
        {
            sourceType = sourceType,
            cardId = cardId,
            targetCardObject = targetCardObject,
            cardSprite = cardSprite,
            canvasRect = canvasRect,
            uiCamera = uiCamera,
            overlaySize = targetRect.rect.size,
            overlayStartScale = GetRelativeScale(targetRect, canvasRect),
            fallbackTargetPosition = WorldToCanvasPosition(canvasRect, targetRect, uiCamera),
            onStarted = onStarted,
            onResolved = onResolved
        };

        CanvasGroup canvasGroup = targetCardObject.GetComponent<CanvasGroup>();
        request.hadCanvasGroup = canvasGroup != null;
        if (!request.hadCanvasGroup)
        {
            canvasGroup = targetCardObject.AddComponent<CanvasGroup>();
        }

        request.targetCanvasGroup = canvasGroup;
        request.originalAlpha = canvasGroup.alpha;
        request.originalBlocksRaycasts = canvasGroup.blocksRaycasts;
        request.originalInteractable = canvasGroup.interactable;

        CardPreviewTrigger previewTrigger = targetCardObject.GetComponent<CardPreviewTrigger>();
        request.originalPreviewEnabled = previewTrigger != null && previewTrigger.enabled;

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (previewTrigger != null)
        {
            previewTrigger.enabled = false;
        }

        HandCardUI handCardUI = targetCardObject.GetComponent<HandCardUI>();
        if (handCardUI != null)
        {
            handCardUI.isHovering = false;
            handCardUI.isDragging = false;
        }

        return request;
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (pendingRequests.Count > 0)
        {
            PileToHandFxRequest request = pendingRequests.Dequeue();
            yield return PlaySingleRoutine(request);
        }

        queueRoutine = null;
    }

    private IEnumerator PlaySingleRoutine(PileToHandFxRequest request)
    {
        if (request == null || request.canvasRect == null || request.cardSprite == null)
            yield break;

        RectTransform canvasRect = request.canvasRect;
        Camera uiCamera = request.uiCamera;
        RectTransform targetRect = request.targetCardObject != null ? request.targetCardObject.GetComponent<RectTransform>() : null;

        GameObject overlayObject = new GameObject(
            "HandCardPileToHandOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(canvasRect, false);
        overlayRect.SetAsLastSibling();
        overlayRect.sizeDelta = request.overlaySize;
        overlayRect.localScale = request.overlayStartScale;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.sprite = request.cardSprite;
        overlayImage.preserveAspect = true;
        overlayImage.raycastTarget = false;

        Vector3 startScale = request.overlayStartScale;
        Vector3 showcaseScale = startScale * showcaseScaleMultiplier;
        Vector2 startPosition = GetSourceOriginPosition(request.sourceType, canvasRect, uiCamera);
        Vector2 showcasePosition = GetShowcasePosition(canvasRect, uiCamera);
        Vector2 targetPosition = request.fallbackTargetPosition;

        overlayRect.anchoredPosition = startPosition;
        request.onStarted?.Invoke();

        yield return AnimateOverlay(
            overlayRect,
            startPosition,
            showcasePosition,
            startScale,
            showcaseScale,
            moveFromSourceDuration
        );

        if (holdAtShowcaseDuration > 0f)
        {
            yield return new WaitForSeconds(holdAtShowcaseDuration);
        }

        if (targetRect != null)
        {
            targetPosition = WorldToCanvasPosition(canvasRect, targetRect, uiCamera);
        }

        yield return AnimateOverlay(
            overlayRect,
            showcasePosition,
            targetPosition,
            showcaseScale,
            startScale,
            moveToHandDuration
        );

        if (overlayObject != null)
        {
            Destroy(overlayObject);
        }

        RestoreTargetCard(request);
        request.onResolved?.Invoke();
    }

    private void RestoreTargetCard(PileToHandFxRequest request)
    {
        if (request == null || request.targetCardObject == null)
            return;

        RectTransform targetRect = request.targetCardObject.GetComponent<RectTransform>();
        HandCardUI handCardUI = request.targetCardObject.GetComponent<HandCardUI>();
        CardPreviewTrigger previewTrigger = request.targetCardObject.GetComponent<CardPreviewTrigger>();

        if (targetRect != null && handCardUI != null)
        {
            targetRect.anchoredPosition = handCardUI.targetPosition;
            targetRect.localRotation = Quaternion.Euler(0f, 0f, handCardUI.targetRotation);
            targetRect.localScale = Vector3.one;
        }

        if (request.targetCanvasGroup != null)
        {
            request.targetCanvasGroup.alpha = request.originalAlpha;
            request.targetCanvasGroup.blocksRaycasts = request.originalBlocksRaycasts;
            request.targetCanvasGroup.interactable = request.originalInteractable;

            if (!request.hadCanvasGroup)
            {
                Destroy(request.targetCanvasGroup);
            }
        }

        if (previewTrigger != null)
        {
            previewTrigger.enabled = request.originalPreviewEnabled;
        }

        if (handCardUI != null && handCardUI.handDisplayManager != null)
        {
            handCardUI.handDisplayManager.UpdateHandLayout();
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

    private Vector2 GetSourceOriginPosition(HandCardPileToHandFxSourceType sourceType, RectTransform canvasRect, Camera uiCamera)
    {
        RectTransform originRect = GetSourceOriginRect(sourceType);
        if (originRect != null)
            return WorldToCanvasPosition(canvasRect, originRect, uiCamera);

        Rect rect = canvasRect.rect;
        switch (sourceType)
        {
            case HandCardPileToHandFxSourceType.DiscardPile:
                return new Vector2(-rect.width * 0.24f, -rect.height * 0.3f);

            case HandCardPileToHandFxSourceType.DrawPileSearch:
                return new Vector2(-rect.width * 0.42f, -rect.height * 0.3f);

            case HandCardPileToHandFxSourceType.Generated:
                return new Vector2(0f, -rect.height * 0.08f);

            default:
                return new Vector2(-rect.width * 0.24f, -rect.height * 0.3f);
        }
    }

    private RectTransform GetSourceOriginRect(HandCardPileToHandFxSourceType sourceType)
    {
        switch (sourceType)
        {
            case HandCardPileToHandFxSourceType.DiscardPile:
                if (discardOriginOverride != null)
                    return discardOriginOverride;
                if (PileCountUI.Instance != null && PileCountUI.Instance.discardPileText != null)
                    return PileCountUI.Instance.discardPileText.rectTransform;
                return null;

            case HandCardPileToHandFxSourceType.DrawPileSearch:
                if (drawPileSearchOriginOverride != null)
                    return drawPileSearchOriginOverride;
                if (PileCountUI.Instance != null && PileCountUI.Instance.drawPileText != null)
                    return PileCountUI.Instance.drawPileText.rectTransform;
                return null;

            case HandCardPileToHandFxSourceType.Generated:
                return generatedOriginOverride;

            default:
                return null;
        }
    }

    private Vector2 GetShowcasePosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (showcaseTargetOverride != null)
            return WorldToCanvasPosition(canvasRect, showcaseTargetOverride, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(rect.width * 0.08f, 0f);
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
