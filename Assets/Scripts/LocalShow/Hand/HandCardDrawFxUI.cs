using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public enum HandCardDrawFxMode
{
    ToHand = 0,
    ShowcaseThenDisappear = 1
}

public enum HandCardDrawFxRequestType
{
    DrawCard = 0,
    Reshuffle = 1,
    PileToHand = 2
}

public class HandCardDrawFxUI : MonoBehaviour
{
    public static HandCardDrawFxUI Instance;

    [Header("Draw FX")]
    [SerializeField] private float moveFromPileDuration = 0.22f;
    [SerializeField] private float holdAtShowcaseDuration = 0.16f;
    [SerializeField] private float moveToHandDuration = 0.24f;
    [SerializeField] private float showcaseScaleMultiplier = 1.12f;
    [SerializeField] private RectTransform drawOriginOverride;
    [SerializeField] private RectTransform showcaseTargetOverride;
    [Header("Reshuffle FX")]
    [SerializeField] private RectTransform reshuffleOriginOverride;
    [SerializeField] private RectTransform reshuffleTargetOverride;
    [SerializeField] private int minReshuffleSparkCount = 12;
    [SerializeField] private int maxReshuffleSparkCount = 20;
    [SerializeField] private Vector2 reshuffleSparkSizeRange = new Vector2(8f, 20f);
    [SerializeField] private Vector2 reshuffleDurationRange = new Vector2(0.3f, 0.55f);
    [SerializeField] private float reshuffleArrivalPadding = 0.08f;

    private readonly Queue<DrawFxRequest> pendingRequests = new Queue<DrawFxRequest>();
    private Coroutine queueRoutine;

    public static bool IsBusy
    {
        get
        {
            return Instance != null && (Instance.queueRoutine != null || Instance.pendingRequests.Count > 0);
        }
    }

    private sealed class DrawFxRequest
    {
        public HandCardDrawFxRequestType requestType;
        public HandCardDrawFxMode mode;
        public string cardId;
        public int movedCardCount;
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
        HandCardDrawFxMode mode = HandCardDrawFxMode.ToHand,
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

        HandCardDrawFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        DrawFxRequest request = fx.CreateRequest(targetCardObject, cardId, cardData.cardSprite, mode, onStarted, onResolved);
        if (request == null)
            return false;

        fx.pendingRequests.Enqueue(request);
        if (fx.queueRoutine == null)
        {
            fx.queueRoutine = fx.StartCoroutine(fx.PlayQueueRoutine());
        }

        return true;
    }

    public static bool TryQueueReshuffle(int movedCardCount, Action onStarted = null, Action onResolved = null)
    {
        if (movedCardCount <= 0)
            return false;

        HandCardDrawFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        DrawFxRequest request = fx.CreateReshuffleRequest(movedCardCount, onStarted, onResolved);
        if (request == null)
            return false;

        fx.pendingRequests.Enqueue(request);
        if (fx.queueRoutine == null)
        {
            fx.queueRoutine = fx.StartCoroutine(fx.PlayQueueRoutine());
        }

        return true;
    }

    private static HandCardDrawFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject fxObject = new GameObject("HandCardDrawFxUI");
        return fxObject.AddComponent<HandCardDrawFxUI>();
    }

    private DrawFxRequest CreateRequest(
        GameObject targetCardObject,
        string cardId,
        Sprite cardSprite,
        HandCardDrawFxMode mode,
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

        DrawFxRequest request = new DrawFxRequest
        {
            requestType = HandCardDrawFxRequestType.DrawCard,
            mode = mode,
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

    private DrawFxRequest CreateReshuffleRequest(int movedCardCount, Action onStarted, Action onResolved)
    {
        RectTransform originRect = GetReshuffleOriginRect();
        RectTransform targetRect = GetReshuffleTargetRect();
        RectTransform anchorRect = originRect != null ? originRect : targetRect;
        if (anchorRect == null)
            return null;

        Canvas rootCanvas = GetRootCanvas(anchorRect.transform);
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return null;

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        return new DrawFxRequest
        {
            requestType = HandCardDrawFxRequestType.Reshuffle,
            movedCardCount = movedCardCount,
            canvasRect = canvasRect,
            uiCamera = uiCamera,
            onStarted = onStarted,
            onResolved = onResolved
        };
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (pendingRequests.Count > 0)
        {
            DrawFxRequest request = pendingRequests.Dequeue();
            yield return PlaySingleRoutine(request);
        }

        queueRoutine = null;
    }

    private IEnumerator PlaySingleRoutine(DrawFxRequest request)
    {
        if (request == null || request.canvasRect == null)
            yield break;

        if (request.requestType == HandCardDrawFxRequestType.Reshuffle)
        {
            yield return PlayReshuffleRoutine(request);
            yield break;
        }

        if (request.cardSprite == null)
            yield break;

        RectTransform canvasRect = request.canvasRect;
        Camera uiCamera = request.uiCamera;
        RectTransform targetRect = request.targetCardObject != null ? request.targetCardObject.GetComponent<RectTransform>() : null;

        GameObject overlayObject = new GameObject(
            "HandCardDrawOverlay",
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
        Vector2 startPosition = GetDrawOriginPosition(canvasRect, uiCamera);
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
            moveFromPileDuration
        );

        if (holdAtShowcaseDuration > 0f)
        {
            yield return new WaitForSeconds(holdAtShowcaseDuration);
        }

        if (request.mode == HandCardDrawFxMode.ShowcaseThenDisappear)
        {
            if (overlayObject != null)
            {
                Destroy(overlayObject);
            }

            request.onResolved?.Invoke();
            yield break;
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

    private IEnumerator PlayReshuffleRoutine(DrawFxRequest request)
    {
        RectTransform canvasRect = request.canvasRect;
        Camera uiCamera = request.uiCamera;
        Vector2 originPosition = GetReshuffleOriginPosition(canvasRect, uiCamera);
        Vector2 targetPosition = GetReshuffleTargetPosition(canvasRect, uiCamera);

        request.onStarted?.Invoke();

        int sparkCount = Mathf.Clamp(request.movedCardCount * 4, minReshuffleSparkCount, maxReshuffleSparkCount);
        if (sparkCount <= 0)
        {
            request.onResolved?.Invoke();
            yield break;
        }

        float longestDuration = 0f;
        List<GameObject> spawnedSparkObjects = new List<GameObject>(sparkCount);

        for (int i = 0; i < sparkCount; i++)
        {
            GameObject sparkObject = new GameObject(
                $"ReshuffleSpark_{i}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(CanvasGroup)
            );

            spawnedSparkObjects.Add(sparkObject);

            RectTransform sparkRect = sparkObject.GetComponent<RectTransform>();
            sparkRect.SetParent(canvasRect, false);
            sparkRect.SetAsLastSibling();

            float sparkSize = UnityEngine.Random.Range(reshuffleSparkSizeRange.x, reshuffleSparkSizeRange.y);
            sparkRect.sizeDelta = Vector2.one * sparkSize;
            sparkRect.localScale = Vector3.one;

            RawImage sparkImage = sparkObject.GetComponent<RawImage>();
            sparkImage.texture = Texture2D.whiteTexture;
            sparkImage.color = GetRandomReshuffleSparkColor();
            sparkImage.raycastTarget = false;

            CanvasGroup sparkCanvasGroup = sparkObject.GetComponent<CanvasGroup>();
            sparkCanvasGroup.alpha = UnityEngine.Random.Range(0.55f, 1f);
            sparkCanvasGroup.blocksRaycasts = false;
            sparkCanvasGroup.interactable = false;

            float duration = UnityEngine.Random.Range(reshuffleDurationRange.x, reshuffleDurationRange.y);
            longestDuration = Mathf.Max(longestDuration, duration);

            Vector2 startOffset = UnityEngine.Random.insideUnitCircle * 26f;
            Vector2 endOffset = UnityEngine.Random.insideUnitCircle * 12f;
            float arcHeight = UnityEngine.Random.Range(24f, 86f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

            StartCoroutine(PlaySingleReshuffleSparkRoutine(
                sparkObject,
                sparkRect,
                sparkImage,
                sparkCanvasGroup,
                originPosition + startOffset,
                targetPosition + endOffset,
                arcHeight,
                duration));
        }

        if (longestDuration > 0f)
        {
            yield return new WaitForSeconds(longestDuration + reshuffleArrivalPadding);
        }

        for (int i = 0; i < spawnedSparkObjects.Count; i++)
        {
            if (spawnedSparkObjects[i] != null)
            {
                Destroy(spawnedSparkObjects[i]);
            }
        }

        request.onResolved?.Invoke();
    }

    private IEnumerator PlaySingleReshuffleSparkRoutine(
        GameObject sparkObject,
        RectTransform sparkRect,
        RawImage sparkImage,
        CanvasGroup sparkCanvasGroup,
        Vector2 startPosition,
        Vector2 targetPosition,
        float arcHeight,
        float duration)
    {
        if (sparkObject == null || sparkRect == null || sparkImage == null || sparkCanvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            sparkRect.anchoredPosition = targetPosition;
            sparkCanvasGroup.alpha = 0f;
            yield break;
        }

        sparkRect.anchoredPosition = startPosition;
        Vector2 midpoint = (startPosition + targetPosition) * 0.5f;
        Vector2 perpendicular = new Vector2(-(targetPosition.y - startPosition.y), targetPosition.x - startPosition.x);
        if (perpendicular.sqrMagnitude > 0.001f)
        {
            perpendicular.Normalize();
        }

        Vector2 controlPoint = midpoint + perpendicular * arcHeight;
        float elapsed = 0f;
        float startAlpha = sparkCanvasGroup.alpha;
        Vector3 startScale = sparkRect.localScale;
        Vector3 endScale = startScale * 0.35f;

        while (elapsed < duration)
        {
            if (sparkObject == null || sparkRect == null || sparkImage == null || sparkCanvasGroup == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutCubic(t);

            sparkRect.anchoredPosition = EvaluateQuadraticBezier(startPosition, controlPoint, targetPosition, easedT);
            sparkRect.localScale = Vector3.LerpUnclamped(startScale, endScale, easedT);
            sparkCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, easedT);

            yield return null;
        }

        sparkRect.anchoredPosition = targetPosition;
        sparkRect.localScale = endScale;
        sparkCanvasGroup.alpha = 0f;
    }

    private void RestoreTargetCard(DrawFxRequest request)
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

    private Vector2 GetDrawOriginPosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (drawOriginOverride != null)
            return WorldToCanvasPosition(canvasRect, drawOriginOverride, uiCamera);
        if (PileCountUI.Instance != null && PileCountUI.Instance.drawPileText != null)
            return WorldToCanvasPosition(canvasRect, PileCountUI.Instance.drawPileText.rectTransform, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(-rect.width * 0.42f, -rect.height * 0.32f);
    }

    private Vector2 GetShowcasePosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (showcaseTargetOverride != null)
            return WorldToCanvasPosition(canvasRect, showcaseTargetOverride, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(rect.width * 0.28f, 0f);
    }

    private RectTransform GetReshuffleOriginRect()
    {
        if (reshuffleOriginOverride != null)
            return reshuffleOriginOverride;
        if (PileCountUI.Instance != null && PileCountUI.Instance.discardPileText != null)
            return PileCountUI.Instance.discardPileText.rectTransform;

        return null;
    }

    private RectTransform GetReshuffleTargetRect()
    {
        if (reshuffleTargetOverride != null)
            return reshuffleTargetOverride;
        if (PileCountUI.Instance != null && PileCountUI.Instance.drawPileText != null)
            return PileCountUI.Instance.drawPileText.rectTransform;

        return null;
    }

    private Vector2 GetReshuffleOriginPosition(RectTransform canvasRect, Camera uiCamera)
    {
        RectTransform originRect = GetReshuffleOriginRect();
        if (originRect != null)
            return WorldToCanvasPosition(canvasRect, originRect, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(-rect.width * 0.36f, -rect.height * 0.3f);
    }

    private Vector2 GetReshuffleTargetPosition(RectTransform canvasRect, Camera uiCamera)
    {
        RectTransform targetRect = GetReshuffleTargetRect();
        if (targetRect != null)
            return WorldToCanvasPosition(canvasRect, targetRect, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(-rect.width * 0.18f, -rect.height * 0.3f);
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

    private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * start + 2f * oneMinusT * t * control + t * t * end;
    }

    private static Color GetRandomReshuffleSparkColor()
    {
        Color[] palette =
        {
            new Color(0.48f, 0.84f, 1f, 1f),
            new Color(0.85f, 0.95f, 1f, 1f),
            new Color(0.62f, 1f, 0.94f, 1f),
            new Color(1f, 0.92f, 0.62f, 1f)
        };

        return palette[UnityEngine.Random.Range(0, palette.Length)];
    }
}
