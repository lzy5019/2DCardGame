using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum HandCardExileFxSource
{
    Hand = 0,
    DrawPile = 1,
    DiscardPile = 2,
    PlayedPile = 3
}

public class HandCardExileFxUI : MonoBehaviour
{
    public static HandCardExileFxUI Instance;

    [Header("Exile FX")]
    [SerializeField] private float moveFromHandDuration = 0.24f;
    [SerializeField] private float moveFromPileDuration = 0.28f;
    [SerializeField] private float holdBeforeExileDuration = 0.08f;
    [SerializeField] private float exileDuration = 0.62f;
    [SerializeField] private float travelScaleMultiplier = 1.08f;
    [SerializeField, Range(0.05f, 1f)] private float endFadeStart = 0.78f;
    [SerializeField] private Vector2 defaultCardSize = new Vector2(252f, 352f);
    [SerializeField] private RectTransform exileTargetOverride;
    [SerializeField] private RectTransform drawPileOriginOverride;
    [SerializeField] private RectTransform discardPileOriginOverride;
    [SerializeField] private RectTransform playedPileOriginOverride;
    [SerializeField] private Material exileMaterialTemplate;
    [SerializeField] private string progressPropertyName = "_ExileProgress";

    private readonly Queue<ExileFxRequest> pendingRequests = new Queue<ExileFxRequest>();
    private Coroutine queueRoutine;

    private sealed class ExileFxRequest
    {
        public HandCardExileFxSource sourceType;
        public string cardId;
        public Sprite cardSprite;
        public RectTransform canvasRect;
        public Camera uiCamera;
        public Vector2 startPosition;
        public Vector2 targetPosition;
        public Vector2 overlaySize;
        public Vector3 startScale;
        public Action onStarted;
        public Action onResolved;
    }

    public static bool IsBusy
    {
        get
        {
            return Instance != null && (Instance.queueRoutine != null || Instance.pendingRequests.Count > 0);
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

    public static bool TryQueueFromHand(GameObject sourceCardObject, string cardId, Action onStarted = null, Action onResolved = null)
    {
        if (sourceCardObject == null)
            return false;

        HandCardExileFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        ExileFxRequest request = fx.CreateHandRequest(sourceCardObject, cardId, onStarted, onResolved);
        if (request == null)
            return false;

        fx.pendingRequests.Enqueue(request);
        fx.EnsureQueueRoutine();
        return true;
    }

    public static bool TryQueueFromPile(string cardId, HandCardExileFxSource sourceType, Action onStarted = null, Action onResolved = null)
    {
        if (sourceType == HandCardExileFxSource.Hand)
            return false;

        HandCardExileFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        ExileFxRequest request = fx.CreatePileRequest(cardId, sourceType, onStarted, onResolved);
        if (request == null)
            return false;

        fx.pendingRequests.Enqueue(request);
        fx.EnsureQueueRoutine();
        return true;
    }

    private static HandCardExileFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject actionFxObject = GameObject.Find("Action FX");
        if (actionFxObject != null)
            return actionFxObject.AddComponent<HandCardExileFxUI>();

        GameObject actionCanvasObject = GameObject.Find("Action Canvas");
        if (actionCanvasObject != null)
            return actionCanvasObject.AddComponent<HandCardExileFxUI>();

        GameObject fxObject = new GameObject("HandCardExileFxUI");
        return fxObject.AddComponent<HandCardExileFxUI>();
    }

    private void EnsureQueueRoutine()
    {
        if (queueRoutine == null)
        {
            queueRoutine = StartCoroutine(PlayQueueRoutine());
        }
    }

    private ExileFxRequest CreateHandRequest(GameObject sourceCardObject, string cardId, Action onStarted, Action onResolved)
    {
        RectTransform sourceRect = sourceCardObject.GetComponent<RectTransform>();
        Image sourceImage = sourceCardObject.GetComponent<Image>();
        if (sourceRect == null || sourceImage == null)
            return null;

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
            return null;

        RectTransform canvasRect = ResolveCanvasRect(sourceCardObject.transform);
        if (canvasRect == null)
            return null;

        Canvas rootCanvas = canvasRect.GetComponent<Canvas>();
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        ConcealSourceCard(sourceCardObject);

        return new ExileFxRequest
        {
            sourceType = HandCardExileFxSource.Hand,
            cardId = cardId,
            cardSprite = sourceSprite,
            canvasRect = canvasRect,
            uiCamera = uiCamera,
            startPosition = WorldToCanvasPosition(canvasRect, sourceRect, uiCamera),
            targetPosition = GetExileTargetPosition(canvasRect, uiCamera),
            overlaySize = sourceRect.rect.size,
            startScale = GetRelativeScale(sourceRect, canvasRect),
            onStarted = onStarted,
            onResolved = onResolved
        };
    }

    private ExileFxRequest CreatePileRequest(string cardId, HandCardExileFxSource sourceType, Action onStarted, Action onResolved)
    {
        if (CardDatabase.Instance == null)
            return null;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null || cardData.cardSprite == null)
            return null;

        RectTransform originRect = GetPileOriginRect(sourceType);
        RectTransform canvasRect = ResolveCanvasRect(originRect != null ? originRect.transform : transform);
        if (canvasRect == null)
            return null;

        Canvas rootCanvas = canvasRect.GetComponent<Canvas>();
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        return new ExileFxRequest
        {
            sourceType = sourceType,
            cardId = cardId,
            cardSprite = cardData.cardSprite,
            canvasRect = canvasRect,
            uiCamera = uiCamera,
            startPosition = GetPileOriginPosition(canvasRect, uiCamera, sourceType),
            targetPosition = GetExileTargetPosition(canvasRect, uiCamera),
            overlaySize = defaultCardSize,
            startScale = Vector3.one,
            onStarted = onStarted,
            onResolved = onResolved
        };
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (pendingRequests.Count > 0)
        {
            ExileFxRequest request = pendingRequests.Dequeue();
            yield return PlaySingleRoutine(request);
        }

        queueRoutine = null;
    }

    private IEnumerator PlaySingleRoutine(ExileFxRequest request)
    {
        if (request == null || request.canvasRect == null || request.cardSprite == null)
            yield break;

        GameObject overlayObject = new GameObject(
            "HandCardExileOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(request.canvasRect, false);
        overlayRect.SetAsLastSibling();
        overlayRect.sizeDelta = request.overlaySize;
        overlayRect.localScale = request.startScale;
        overlayRect.anchoredPosition = request.startPosition;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.sprite = request.cardSprite;
        overlayImage.preserveAspect = true;
        overlayImage.raycastTarget = false;

        CanvasGroup overlayCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 1f;
        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;

        request.onStarted?.Invoke();

        float moveDuration = request.sourceType == HandCardExileFxSource.Hand ? moveFromHandDuration : moveFromPileDuration;
        Vector3 travelScale = request.startScale * travelScaleMultiplier;
        yield return AnimateOverlay(
            overlayRect,
            request.startPosition,
            request.targetPosition,
            request.startScale,
            travelScale,
            moveDuration);

        if (holdBeforeExileDuration > 0f)
        {
            yield return new WaitForSeconds(holdBeforeExileDuration);
        }

        Material runtimeMaterial = PrepareRuntimeMaterial();
        if (runtimeMaterial != null)
        {
            overlayImage.material = runtimeMaterial;
            runtimeMaterial.SetFloat(progressPropertyName, 0f);
        }

        float elapsed = 0f;
        while (elapsed < exileDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(exileDuration, 0.0001f));
            float easedT = EaseInOutCubic(t);

            if (runtimeMaterial != null)
            {
                runtimeMaterial.SetFloat(progressPropertyName, easedT);
            }

            float alphaFadeT = Mathf.InverseLerp(endFadeStart, 1f, t);
            overlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, alphaFadeT);
            yield return null;
        }

        if (overlayObject != null)
        {
            Destroy(overlayObject);
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }

        request.onResolved?.Invoke();
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
            runtimeMaterial.name = "Runtime Hand Exile Void Shatter";
            return runtimeMaterial;
        }

        Shader exileShader = Shader.Find("UI/ExileVoidShatter");
        if (exileShader == null)
            return null;

        Material fallbackMaterial = new Material(exileShader);
        fallbackMaterial.name = "Runtime Hand Exile Void Shatter";
        return fallbackMaterial;
    }

    private RectTransform ResolveCanvasRect(Transform referenceTransform)
    {
        Transform canvasReference = exileTargetOverride != null ? exileTargetOverride : referenceTransform;
        Canvas rootCanvas = GetRootCanvas(canvasReference);
        if (rootCanvas == null)
            return null;

        return rootCanvas.transform as RectTransform;
    }

    private RectTransform GetPileOriginRect(HandCardExileFxSource sourceType)
    {
        switch (sourceType)
        {
            case HandCardExileFxSource.DrawPile:
                if (drawPileOriginOverride != null)
                    return drawPileOriginOverride;
                if (PileCountUI.Instance != null && PileCountUI.Instance.drawPileText != null)
                    return PileCountUI.Instance.drawPileText.rectTransform;
                break;

            case HandCardExileFxSource.DiscardPile:
                if (discardPileOriginOverride != null)
                    return discardPileOriginOverride;
                if (PileCountUI.Instance != null && PileCountUI.Instance.discardPileText != null)
                    return PileCountUI.Instance.discardPileText.rectTransform;
                break;

            case HandCardExileFxSource.PlayedPile:
                if (playedPileOriginOverride != null)
                    return playedPileOriginOverride;
                if (PileCountUI.Instance != null && PileCountUI.Instance.playedPileText != null)
                    return PileCountUI.Instance.playedPileText.rectTransform;
                break;
        }

        return null;
    }

    private Vector2 GetPileOriginPosition(RectTransform canvasRect, Camera uiCamera, HandCardExileFxSource sourceType)
    {
        RectTransform originRect = GetPileOriginRect(sourceType);
        if (originRect != null)
            return WorldToCanvasPosition(canvasRect, originRect, uiCamera);

        Rect rect = canvasRect.rect;
        switch (sourceType)
        {
            case HandCardExileFxSource.DiscardPile:
                return new Vector2(-rect.width * 0.36f, -rect.height * 0.3f);

            case HandCardExileFxSource.PlayedPile:
                return new Vector2(0f, -rect.height * 0.3f);

            case HandCardExileFxSource.DrawPile:
            default:
                return new Vector2(-rect.width * 0.18f, -rect.height * 0.3f);
        }
    }

    private Vector2 GetExileTargetPosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (exileTargetOverride != null)
            return WorldToCanvasPosition(canvasRect, exileTargetOverride, uiCamera);

        return Vector2.zero;
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
        if (target == null)
            return null;

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
        {
            return 4f * t * t * t;
        }

        float adjusted = -2f * t + 2f;
        return 1f - (adjusted * adjusted * adjusted) / 2f;
    }
}
