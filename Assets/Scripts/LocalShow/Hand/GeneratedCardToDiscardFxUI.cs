using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class GeneratedCardToDiscardFxUI : MonoBehaviour
{
    public static GeneratedCardToDiscardFxUI Instance;

    [Header("Generated To Discard FX")]
    [SerializeField] private float moveFromSourceDuration = 0.22f;
    [SerializeField] private float holdAtShowcaseDuration = 0.18f;
    [SerializeField] private float moveToDiscardDuration = 0.24f;
    [SerializeField] private float showcaseScaleMultiplier = 1.12f;
    [SerializeField] private Vector2 overlaySize = new Vector2(140f, 196f);

    [Header("Anchors")]
    [SerializeField] private RectTransform generatedOriginOverride;
    [SerializeField] private RectTransform showcaseTargetOverride;
    [SerializeField] private RectTransform discardTargetOverride;

    private readonly Queue<FxRequest> pendingRequests = new Queue<FxRequest>();
    private Coroutine queueRoutine;

    private sealed class FxRequest
    {
        public Sprite cardSprite;
        public RectTransform canvasRect;
        public Camera uiCamera;
        public Vector2 size;
        public int targetPlayerIndex = -1;
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

    public static bool TryQueue(string cardId)
    {
        return TryQueue(cardId, -1);
    }

    public static bool TryQueue(string cardId, int targetPlayerIndex)
    {
        if (string.IsNullOrEmpty(cardId))
            return false;
        if (CardDatabase.Instance == null)
            return false;

        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null || cardData.cardSprite == null)
            return false;

        GeneratedCardToDiscardFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        FxRequest request = fx.CreateRequest(cardData.cardSprite, targetPlayerIndex);
        if (request == null)
            return false;

        fx.pendingRequests.Enqueue(request);
        if (fx.queueRoutine == null)
        {
            fx.queueRoutine = fx.StartCoroutine(fx.PlayQueueRoutine());
        }

        return true;
    }

    private static GeneratedCardToDiscardFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject fxObject = new GameObject("GeneratedCardToDiscardFxUI");
        return fxObject.AddComponent<GeneratedCardToDiscardFxUI>();
    }

    private FxRequest CreateRequest(Sprite cardSprite, int targetPlayerIndex)
    {
        if (cardSprite == null)
            return null;

        if (!TryResolveCanvasContext(targetPlayerIndex, out RectTransform canvasRect, out Camera uiCamera))
            return null;
        return new FxRequest
        {
            cardSprite = cardSprite,
            canvasRect = canvasRect,
            uiCamera = uiCamera,
            size = overlaySize,
            targetPlayerIndex = targetPlayerIndex
        };
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (pendingRequests.Count > 0)
        {
            FxRequest request = pendingRequests.Dequeue();
            yield return PlaySingleRoutine(request);
        }

        queueRoutine = null;
    }

    private IEnumerator PlaySingleRoutine(FxRequest request)
    {
        if (request == null || request.canvasRect == null || request.cardSprite == null)
            yield break;

        RectTransform canvasRect = request.canvasRect;
        Camera uiCamera = request.uiCamera;
        bool routeToLocalDiscard = ShouldRouteToLocalDiscard(request.targetPlayerIndex);

        GameObject overlayObject = new GameObject(
            "GeneratedCardToDiscardOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(canvasRect, false);
        overlayRect.SetAsLastSibling();
        overlayRect.sizeDelta = request.size;
        overlayRect.localScale = Vector3.one;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.sprite = request.cardSprite;
        overlayImage.preserveAspect = true;
        overlayImage.raycastTarget = false;

        Vector3 startScale = Vector3.one;
        Vector3 showcaseScale = startScale * showcaseScaleMultiplier;
        Vector2 startPosition = GetGeneratedOriginPosition(canvasRect, uiCamera);
        Vector2 showcasePosition = GetShowcasePosition(canvasRect, uiCamera);
        Vector2 finalPosition = routeToLocalDiscard
            ? GetDiscardTargetPosition(canvasRect, uiCamera)
            : GetOtherPlayerTargetPosition(canvasRect, uiCamera, request.targetPlayerIndex);

        overlayRect.anchoredPosition = startPosition;

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

        yield return AnimateOverlay(
            overlayRect,
            showcasePosition,
            finalPosition,
            showcaseScale,
            startScale,
            moveToDiscardDuration
        );

        if (overlayObject != null)
        {
            Destroy(overlayObject);
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

    private Vector2 GetGeneratedOriginPosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (generatedOriginOverride != null)
            return WorldToCanvasPosition(canvasRect, generatedOriginOverride, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(0f, -rect.height * 0.08f);
    }

    private Vector2 GetShowcasePosition(RectTransform canvasRect, Camera uiCamera)
    {
        if (showcaseTargetOverride != null)
            return WorldToCanvasPosition(canvasRect, showcaseTargetOverride, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(rect.width * 0.08f, 0f);
    }

    private Vector2 GetDiscardTargetPosition(RectTransform canvasRect, Camera uiCamera)
    {
        RectTransform discardRect = discardTargetOverride;
        if (discardRect == null && PileCountUI.Instance != null && PileCountUI.Instance.discardPileText != null)
        {
            discardRect = PileCountUI.Instance.discardPileText.rectTransform;
        }
        if (discardRect != null)
            return WorldToCanvasPosition(canvasRect, discardRect, uiCamera);

        Rect rect = canvasRect.rect;
        return new Vector2(-rect.width * 0.24f, -rect.height * 0.3f);
    }

    private Vector2 GetOtherPlayerTargetPosition(RectTransform canvasRect, Camera uiCamera, int targetPlayerIndex)
    {
        if (PlayerListManager.Instance != null &&
            PlayerListManager.Instance.TryGetPlayerNameRect(targetPlayerIndex, out RectTransform targetRect) &&
            targetRect != null)
        {
            return WorldToCanvasPosition(canvasRect, targetRect, uiCamera);
        }

        Rect rect = canvasRect.rect;
        return new Vector2(-rect.width * 0.24f, rect.height * 0.08f);
    }

    private bool TryResolveCanvasContext(int targetPlayerIndex, out RectTransform canvasRect, out Camera uiCamera)
    {
        canvasRect = null;
        uiCamera = null;

        RectTransform referenceRect = generatedOriginOverride;
        if (referenceRect == null)
        {
            referenceRect = showcaseTargetOverride;
        }
        if (referenceRect == null && discardTargetOverride != null)
        {
            referenceRect = discardTargetOverride;
        }
        if (referenceRect == null &&
            targetPlayerIndex >= 0 &&
            PlayerListManager.Instance != null &&
            PlayerListManager.Instance.TryGetPlayerNameRect(targetPlayerIndex, out RectTransform playerNameRect))
        {
            referenceRect = playerNameRect;
        }
        if (referenceRect == null && PileCountUI.Instance != null && PileCountUI.Instance.discardPileText != null)
        {
            referenceRect = PileCountUI.Instance.discardPileText.rectTransform;
        }
        if (referenceRect == null)
            return false;

        Canvas rootCanvas = GetRootCanvas(referenceRect.transform);
        canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return false;

        uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        return true;
    }

    private static bool ShouldRouteToLocalDiscard(int targetPlayerIndex)
    {
        if (targetPlayerIndex < 0)
            return true;
        if (NetworkClient.localPlayer == null)
            return false;

        PlayerState localPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
        if (localPlayer == null)
            return false;

        return localPlayer.playerIndex == targetPlayerIndex;
    }

    private static Vector2 WorldToCanvasPosition(RectTransform canvasRect, RectTransform targetRect, Camera uiCamera)
    {
        Vector3 worldCenter = targetRect.TransformPoint(targetRect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCenter);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            return localPoint;

        return Vector2.zero;
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
