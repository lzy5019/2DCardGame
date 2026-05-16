using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum PlayedCardResolveDestinationType
{
    PlayedPile = 0,
    DrawPile = 1,
    DiscardPile = 2,
    Banish = 3
}

public class HandCardPlayFxUI : MonoBehaviour
{
    public static HandCardPlayFxUI Instance;

    [Header("Play Cast FX")]
    [SerializeField] private float moveToCastDuration = 0.16f;
    [SerializeField] private float holdAtCastDuration = 0.12f;
    [SerializeField] private float moveToDestinationDuration = 0.3f;
    [SerializeField] private float castScaleMultiplier = 1.08f;
    [SerializeField] private float resolveEndScaleMultiplier = 0.3f;
    [SerializeField] private RectTransform castTargetOverride;
    [SerializeField] private RectTransform playedPileTargetOverride;
    [SerializeField] private RectTransform drawPileTargetOverride;
    [SerializeField] private RectTransform discardPileTargetOverride;

    [Header("Exile Resolve FX")]
    [SerializeField] private float banishDuration = 0.6f;
    [SerializeField, Range(0.05f, 1f)] private float banishFadeStart = 0.78f;
    [SerializeField] private Material exileMaterialTemplate;
    [SerializeField] private string progressPropertyName = "_ExileProgress";

    private const float PendingResolveTimeout = 15f;

    private sealed class ActivePlayRequest
    {
        public int requestId;
        public string cardId;
        public RectTransform canvasRect;
        public Camera uiCamera;
        public RectTransform overlayRect;
        public Image overlayImage;
        public CanvasGroup overlayCanvasGroup;
        public Vector3 startScale;
        public Vector2 castPosition;
        public bool hasResolution;
        public PlayedCardResolveDestinationType destinationType;
        public Coroutine routine;
    }

    private sealed class PendingResolveInfo
    {
        public string cardId;
        public PlayedCardResolveDestinationType destinationType;
        public float expireTime;
    }

    private readonly Dictionary<int, ActivePlayRequest> activeRequests = new Dictionary<int, ActivePlayRequest>();
    private readonly Dictionary<int, PendingResolveInfo> pendingResolutions = new Dictionary<int, PendingResolveInfo>();

    public static bool IsBusy
    {
        get
        {
            return Instance != null && (Instance.activeRequests.Count > 0 || Instance.pendingResolutions.Count > 0);
        }
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

    private void Update()
    {
        if (pendingResolutions.Count <= 0)
            return;

        float now = Time.unscaledTime;
        List<int> expiredRequestIds = null;

        foreach (KeyValuePair<int, PendingResolveInfo> pair in pendingResolutions)
        {
            if (pair.Value == null || pair.Value.expireTime > now)
                continue;

            if (expiredRequestIds == null)
            {
                expiredRequestIds = new List<int>();
            }

            expiredRequestIds.Add(pair.Key);
        }

        if (expiredRequestIds == null)
            return;

        for (int i = 0; i < expiredRequestIds.Count; i++)
        {
            pendingResolutions.Remove(expiredRequestIds[i]);
        }
    }

    public static bool TryBegin(int requestId, GameObject sourceCardObject, string cardId, Vector2 releaseScreenPosition)
    {
        if (requestId <= 0 || sourceCardObject == null)
            return false;

        HandCardPlayFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        return fx.BeginInternal(requestId, sourceCardObject, cardId, releaseScreenPosition);
    }

    public static void NotifyResolved(int requestId, string cardId, PlayedCardResolveDestinationType destinationType)
    {
        if (requestId <= 0)
            return;

        HandCardPlayFxUI fx = EnsureInstance();
        if (fx == null)
            return;

        fx.NotifyResolvedInternal(requestId, cardId, destinationType);
    }

    public static bool TryGetPrimaryCastContext(out RectTransform canvasRect, out Camera uiCamera, out Vector2 castPosition)
    {
        canvasRect = null;
        uiCamera = null;
        castPosition = Vector2.zero;

        if (Instance == null || Instance.activeRequests.Count <= 0)
            return false;

        foreach (KeyValuePair<int, ActivePlayRequest> pair in Instance.activeRequests)
        {
            ActivePlayRequest request = pair.Value;
            if (request == null || request.canvasRect == null)
                continue;

            canvasRect = request.canvasRect;
            uiCamera = request.uiCamera;
            castPosition = request.castPosition;
            return true;
        }

        return false;
    }

    private static HandCardPlayFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject actionFxObject = GameObject.Find("Action FX");
        if (actionFxObject != null)
            return actionFxObject.AddComponent<HandCardPlayFxUI>();

        GameObject actionCanvasObject = GameObject.Find("Action Canvas");
        if (actionCanvasObject != null)
            return actionCanvasObject.AddComponent<HandCardPlayFxUI>();

        GameObject fxObject = new GameObject("HandCardPlayFxUI");
        return fxObject.AddComponent<HandCardPlayFxUI>();
    }

    private bool BeginInternal(int requestId, GameObject sourceCardObject, string cardId, Vector2 releaseScreenPosition)
    {
        if (activeRequests.ContainsKey(requestId))
            return false;

        RectTransform sourceRect = sourceCardObject.GetComponent<RectTransform>();
        Image sourceImage = sourceCardObject.GetComponent<Image>();
        if (sourceRect == null || sourceImage == null)
            return false;

        Sprite sourceSprite = sourceImage.sprite;
        if (sourceSprite == null && CardDatabase.Instance != null)
        {
            CardData sourceCardData = CardDatabase.Instance.GetCardById(cardId);
            if (sourceCardData != null)
            {
                sourceSprite = sourceCardData.cardSprite;
            }
        }

        if (sourceSprite == null)
            return false;

        Canvas rootCanvas = GetRootCanvas(sourceCardObject.transform);
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return false;

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 startPosition = ResolveReleasePosition(canvasRect, sourceRect, uiCamera, releaseScreenPosition);

        GameObject overlayObject = new GameObject(
            "HandCardPlayOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(canvasRect, false);
        overlayRect.SetAsLastSibling();
        overlayRect.sizeDelta = sourceRect.rect.size;
        overlayRect.localScale = GetRelativeScale(sourceRect, canvasRect);
        overlayRect.anchoredPosition = startPosition;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.sprite = sourceSprite;
        overlayImage.preserveAspect = true;
        overlayImage.raycastTarget = false;

        CanvasGroup overlayCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 1f;
        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;

        ConcealSourceCard(sourceCardObject);
        Destroy(sourceCardObject);

        ActivePlayRequest request = new ActivePlayRequest
        {
            requestId = requestId,
            cardId = cardId,
            canvasRect = canvasRect,
            uiCamera = uiCamera,
            overlayRect = overlayRect,
            overlayImage = overlayImage,
            overlayCanvasGroup = overlayCanvasGroup,
            startScale = overlayRect.localScale,
            castPosition = GetCastTargetPosition(canvasRect, uiCamera)
        };

        if (pendingResolutions.TryGetValue(requestId, out PendingResolveInfo pendingResolve))
        {
            request.hasResolution = true;
            request.destinationType = pendingResolve.destinationType;
            if (string.IsNullOrEmpty(request.cardId))
            {
                request.cardId = pendingResolve.cardId;
            }

            pendingResolutions.Remove(requestId);
        }

        activeRequests.Add(requestId, request);
        request.routine = StartCoroutine(PlayRoutine(request, startPosition));
        return true;
    }

    private void NotifyResolvedInternal(int requestId, string cardId, PlayedCardResolveDestinationType destinationType)
    {
        if (activeRequests.TryGetValue(requestId, out ActivePlayRequest activeRequest))
        {
            activeRequest.destinationType = destinationType;
            activeRequest.hasResolution = true;
            if (string.IsNullOrEmpty(activeRequest.cardId))
            {
                activeRequest.cardId = cardId;
            }

            return;
        }

        pendingResolutions[requestId] = new PendingResolveInfo
        {
            cardId = cardId,
            destinationType = destinationType,
            expireTime = Time.unscaledTime + PendingResolveTimeout
        };
    }

    private IEnumerator PlayRoutine(ActivePlayRequest request, Vector2 startPosition)
    {
        if (request == null || request.overlayRect == null)
        {
            yield break;
        }

        Vector3 castScale = request.startScale * castScaleMultiplier;
        yield return AnimateOverlay(
            request.overlayRect,
            request.startScale,
            castScale,
            startPosition,
            request.castPosition,
            moveToCastDuration,
            null);

        if (holdAtCastDuration > 0f)
        {
            yield return new WaitForSeconds(holdAtCastDuration);
        }

        while (!request.hasResolution)
        {
            yield return null;
        }

        if (request.destinationType == PlayedCardResolveDestinationType.Banish)
        {
            yield return AnimateBanish(request);
        }
        else
        {
            Vector2 targetPosition = GetDestinationPosition(request.canvasRect, request.uiCamera, request.destinationType);
            Vector3 endScale = request.startScale * resolveEndScaleMultiplier;
            yield return AnimateOverlay(
                request.overlayRect,
                castScale,
                endScale,
                request.castPosition,
                targetPosition,
                moveToDestinationDuration,
                request.overlayCanvasGroup);
        }

        CleanupRequest(request);
    }

    private IEnumerator AnimateOverlay(
        RectTransform overlayRect,
        Vector3 fromScale,
        Vector3 toScale,
        Vector2 fromPosition,
        Vector2 toPosition,
        float duration,
        CanvasGroup overlayCanvasGroup)
    {
        if (overlayRect == null)
            yield break;

        if (duration <= 0f)
        {
            overlayRect.localScale = toScale;
            overlayRect.anchoredPosition = toPosition;
            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = toScale == Vector3.zero ? 0f : 1f;
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutCubic(t);
            overlayRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedT);
            overlayRect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, easedT);

            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            }

            yield return null;
        }

        overlayRect.localScale = toScale;
        overlayRect.anchoredPosition = toPosition;
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
        }
    }

    private IEnumerator AnimateBanish(ActivePlayRequest request)
    {
        if (request == null || request.overlayRect == null || request.overlayImage == null)
            yield break;

        Material runtimeMaterial = PrepareRuntimeMaterial();
        if (runtimeMaterial != null)
        {
            request.overlayImage.material = runtimeMaterial;
            runtimeMaterial.SetFloat(progressPropertyName, 0f);
        }

        float elapsed = 0f;
        while (elapsed < banishDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(banishDuration, 0.0001f));
            float easedT = EaseInOutCubic(t);

            if (runtimeMaterial != null)
            {
                runtimeMaterial.SetFloat(progressPropertyName, easedT);
            }

            if (request.overlayCanvasGroup != null)
            {
                float fadeT = Mathf.InverseLerp(banishFadeStart, 1f, t);
                request.overlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeT);
            }

            yield return null;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    private void CleanupRequest(ActivePlayRequest request)
    {
        if (request == null)
            return;

        if (request.requestId > 0)
        {
            activeRequests.Remove(request.requestId);
            pendingResolutions.Remove(request.requestId);
        }

        if (request.overlayRect != null)
        {
            Destroy(request.overlayRect.gameObject);
        }
    }

    private void ConcealSourceCard(GameObject sourceCardObject)
    {
        if (sourceCardObject == null)
            return;

        CanvasGroup canvasGroup = sourceCardObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = sourceCardObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        CardPreviewTrigger previewTrigger = sourceCardObject.GetComponent<CardPreviewTrigger>();
        if (previewTrigger != null)
        {
            previewTrigger.enabled = false;
        }

        HandCardUI handCardUI = sourceCardObject.GetComponent<HandCardUI>();
        if (handCardUI != null)
        {
            handCardUI.isHovering = false;
            handCardUI.isDragging = false;
        }
    }

    private Material PrepareRuntimeMaterial()
    {
        if (exileMaterialTemplate != null)
        {
            Material runtimeMaterial = new Material(exileMaterialTemplate);
            runtimeMaterial.name = "Runtime Hand Play Exile";
            return runtimeMaterial;
        }

        Shader exileShader = Shader.Find("UI/ExileVoidShatter");
        if (exileShader == null)
            return null;

        Material fallbackMaterial = new Material(exileShader);
        fallbackMaterial.name = "Runtime Hand Play Exile";
        return fallbackMaterial;
    }

    private Vector2 ResolveReleasePosition(RectTransform canvasRect, RectTransform sourceRect, Camera uiCamera, Vector2 releaseScreenPosition)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, releaseScreenPosition, uiCamera, out Vector2 localPoint))
            return localPoint;

        return WorldToCanvasPosition(canvasRect, sourceRect, uiCamera);
    }

    private Vector2 GetCastTargetPosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (castTargetOverride != null)
            return WorldToCanvasPosition(canvasRect, castTargetOverride, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(0f, rect.height * 0.08f);
    }

    private Vector2 GetDestinationPosition(RectTransform canvasRect, Camera uiCamera, PlayedCardResolveDestinationType destinationType)
    {
        RectTransform destinationRect = null;

        switch (destinationType)
        {
            case PlayedCardResolveDestinationType.PlayedPile:
                destinationRect = playedPileTargetOverride;
                if (destinationRect == null && PileCountUI.Instance != null && PileCountUI.Instance.playedPileText != null)
                {
                    destinationRect = PileCountUI.Instance.playedPileText.rectTransform;
                }
                break;

            case PlayedCardResolveDestinationType.DrawPile:
                destinationRect = drawPileTargetOverride;
                if (destinationRect == null && PileCountUI.Instance != null && PileCountUI.Instance.drawPileText != null)
                {
                    destinationRect = PileCountUI.Instance.drawPileText.rectTransform;
                }
                break;

            case PlayedCardResolveDestinationType.DiscardPile:
                destinationRect = discardPileTargetOverride;
                if (destinationRect == null && PileCountUI.Instance != null && PileCountUI.Instance.discardPileText != null)
                {
                    destinationRect = PileCountUI.Instance.discardPileText.rectTransform;
                }
                break;
        }

        if (destinationRect != null)
            return WorldToCanvasPosition(canvasRect, destinationRect, uiCamera);

        Rect rect = canvasRect.rect;
        switch (destinationType)
        {
            case PlayedCardResolveDestinationType.PlayedPile:
                return new Vector2(0f, -rect.height * 0.28f);
            case PlayedCardResolveDestinationType.DrawPile:
                return new Vector2(-rect.width * 0.18f, -rect.height * 0.3f);
            case PlayedCardResolveDestinationType.DiscardPile:
            default:
                return new Vector2(-rect.width * 0.36f, -rect.height * 0.3f);
        }
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
        if (sourceRect == null || canvasRect == null)
            return Vector3.one;

        Vector3 sourceLossyScale = sourceRect.lossyScale;
        Vector3 canvasLossyScale = canvasRect.lossyScale;

        float scaleX = Mathf.Approximately(canvasLossyScale.x, 0f) ? 1f : sourceLossyScale.x / canvasLossyScale.x;
        float scaleY = Mathf.Approximately(canvasLossyScale.y, 0f) ? 1f : sourceLossyScale.y / canvasLossyScale.y;
        float scaleZ = Mathf.Approximately(canvasLossyScale.z, 0f) ? 1f : sourceLossyScale.z / canvasLossyScale.z;

        return new Vector3(scaleX, scaleY, scaleZ);
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

    private static float EaseInOutCubic(float t)
    {
        if (t < 0.5f)
            return 4f * t * t * t;

        float inverse = -2f * t + 2f;
        return 1f - (inverse * inverse * inverse) * 0.5f;
    }
}
