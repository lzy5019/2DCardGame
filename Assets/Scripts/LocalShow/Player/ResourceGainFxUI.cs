using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ResourceGainVisualType
{
    Mana = 0,
    Attack = 1,
    Score = 2
}

public enum ResourceGainVisualSourceType
{
    Default = 0,
    CenterShopSlot = 1,
    BaseShopSlot = 2
}

public class ResourceGainFxUI : MonoBehaviour
{
    public static ResourceGainFxUI Instance;

    [Header("Token Movement")]
    [SerializeField] private float spawnScale = 0.55f;
    [SerializeField] private float travelScale = 0.92f;
    [SerializeField] private float moveDurationMin = 0.42f;
    [SerializeField] private float moveDurationMax = 0.58f;
    [SerializeField] private float staggerInterval = 0.07f;
    [SerializeField, Range(0.15f, 0.8f)] private float accelerationPhaseRatio = 0.38f;
    [SerializeField] private float spawnScatterRadius = 18f;
    [SerializeField] private float targetScatterRadius = 10f;
    [SerializeField] private float fadeOutStart = 0.88f;

    [Header("Targets")]
    [SerializeField] private RectTransform spawnOriginOverride;
    [SerializeField] private RectTransform manaTargetOverride;
    [SerializeField] private RectTransform attackTargetOverride;
    [SerializeField] private RectTransform scoreTargetOverride;

    [Header("Token Prefabs")]
    [SerializeField] private RectTransform manaTokenPrefab;
    [SerializeField] private RectTransform attackTokenPrefab;
    [SerializeField] private RectTransform scoreTokenPrefab;

    [Header("Fallback Colors")]
    [SerializeField] private Color manaFallbackColor = new Color(0.2f, 0.65f, 1f, 1f);
    [SerializeField] private Color attackFallbackColor = new Color(1f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color scoreFallbackColor = new Color(1f, 0.88f, 0.18f, 1f);

    [Header("Fallback Shape")]
    [SerializeField] private float fallbackTokenSize = 22f;

    private static Sprite cachedFallbackTokenSprite;

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

    public static bool TryQueue(PlayerState owner, ResourceGainVisualType resourceType, int amount, ResourceGainVisualSourceType sourceType = ResourceGainVisualSourceType.Default, int sourceSlotIndex = -1)
    {
        if (owner == null || amount <= 0)
            return false;

        ResourceGainFxUI fx = EnsureInstance();
        if (fx == null)
            return false;

        return fx.QueueInternal(owner, resourceType, amount, sourceType, sourceSlotIndex);
    }

    private static ResourceGainFxUI EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject actionFxObject = GameObject.Find("Action FX");
        if (actionFxObject != null)
            return actionFxObject.AddComponent<ResourceGainFxUI>();

        GameObject actionCanvasObject = GameObject.Find("Action Canvas");
        if (actionCanvasObject != null)
            return actionCanvasObject.AddComponent<ResourceGainFxUI>();

        GameObject localCanvasObject = GameObject.Find("Local Canvas");
        if (localCanvasObject != null)
            return localCanvasObject.AddComponent<ResourceGainFxUI>();

        GameObject fxObject = new GameObject("ResourceGainFxUI");
        return fxObject.AddComponent<ResourceGainFxUI>();
    }

    private bool QueueInternal(PlayerState owner, ResourceGainVisualType resourceType, int amount, ResourceGainVisualSourceType sourceType, int sourceSlotIndex)
    {
        if (!TryResolveCanvasContext(sourceType, sourceSlotIndex, out RectTransform canvasRect, out Camera uiCamera, out Vector2 spawnPosition))
            return false;

        Vector2 targetPosition = GetTargetPosition(resourceType, canvasRect, uiCamera);
        for (int i = 0; i < amount; i++)
        {
            float delay = i * staggerInterval + Random.Range(0f, staggerInterval * 0.35f);
            float duration = Random.Range(moveDurationMin, moveDurationMax);
            Vector2 tokenStart = spawnPosition + Random.insideUnitCircle * spawnScatterRadius;
            Vector2 tokenTarget = targetPosition + Random.insideUnitCircle * targetScatterRadius;
            StartCoroutine(PlayTokenRoutine(owner, resourceType, canvasRect, tokenStart, tokenTarget, delay, duration));
        }

        return true;
    }

    private IEnumerator PlayTokenRoutine(
        PlayerState owner,
        ResourceGainVisualType resourceType,
        RectTransform canvasRect,
        Vector2 startPosition,
        Vector2 targetPosition,
        float delay,
        float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        RectTransform tokenRect = InstantiateToken(resourceType, canvasRect);
        if (tokenRect == null)
        {
            owner?.NotifyLocalResourceVisualResolved(resourceType);
            yield break;
        }

        tokenRect.SetAsLastSibling();
        tokenRect.anchoredPosition = startPosition;

        CanvasGroup tokenCanvasGroup = tokenRect.GetComponent<CanvasGroup>();
        if (tokenCanvasGroup == null)
        {
            tokenCanvasGroup = tokenRect.gameObject.AddComponent<CanvasGroup>();
        }

        tokenCanvasGroup.alpha = 1f;
        tokenCanvasGroup.blocksRaycasts = false;
        tokenCanvasGroup.interactable = false;

        Vector3 baseScale = tokenRect.localScale;
        tokenRect.localScale = baseScale * spawnScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.0001f));
            float distanceT = EvaluateAccelerateThenCruise(t, accelerationPhaseRatio);

            tokenRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, distanceT);
            tokenRect.localScale = baseScale * Mathf.LerpUnclamped(spawnScale, travelScale, EaseOutCubic(t));

            float fadeT = Mathf.InverseLerp(fadeOutStart, 1f, t);
            tokenCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeT);

            yield return null;
        }

        tokenRect.anchoredPosition = targetPosition;
        tokenCanvasGroup.alpha = 0f;

        if (owner != null)
        {
            owner.NotifyLocalResourceVisualResolved(resourceType);
        }

        Destroy(tokenRect.gameObject);
    }

    private bool TryResolveCanvasContext(ResourceGainVisualSourceType sourceType, int sourceSlotIndex, out RectTransform canvasRect, out Camera uiCamera, out Vector2 spawnPosition)
    {
        if (TryResolveShopSlotContext(sourceType, sourceSlotIndex, out canvasRect, out uiCamera, out spawnPosition))
            return true;

        if (HandCardPlayFxUI.TryGetPrimaryCastContext(out canvasRect, out uiCamera, out spawnPosition) && canvasRect != null)
            return true;

        Canvas rootCanvas = GetRootCanvas(transform);
        canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (canvasRect == null)
        {
            spawnPosition = Vector2.zero;
            return false;
        }

        if (spawnOriginOverride != null)
        {
            spawnPosition = WorldToCanvasPosition(canvasRect, spawnOriginOverride, uiCamera);
        }
        else
        {
            Rect rect = canvasRect.rect;
            spawnPosition = new Vector2(0f, rect.height * 0.04f);
        }

        return true;
    }

    private bool TryResolveShopSlotContext(ResourceGainVisualSourceType sourceType, int sourceSlotIndex, out RectTransform canvasRect, out Camera uiCamera, out Vector2 spawnPosition)
    {
        canvasRect = null;
        uiCamera = null;
        spawnPosition = Vector2.zero;

        if (sourceType != ResourceGainVisualSourceType.CenterShopSlot && sourceType != ResourceGainVisualSourceType.BaseShopSlot)
            return false;
        if (ShopPanelUI.Instance == null)
            return false;

        bool isBaseShop = sourceType == ResourceGainVisualSourceType.BaseShopSlot;
        if (!ShopPanelUI.Instance.TryGetSlotRect(sourceSlotIndex, isBaseShop, out RectTransform slotRect))
            return false;

        Canvas rootCanvas = GetRootCanvas(slotRect);
        canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (canvasRect == null)
            return false;

        spawnPosition = WorldToCanvasPosition(canvasRect, slotRect, uiCamera);
        return true;
    }

    private Vector2 GetTargetPosition(ResourceGainVisualType resourceType, RectTransform canvasRect, Camera uiCamera)
    {
        RectTransform targetRect = null;

        switch (resourceType)
        {
            case ResourceGainVisualType.Attack:
                targetRect = attackTargetOverride;
                if (targetRect == null && PlayerResourceUI.Instance != null)
                {
                    targetRect = PlayerResourceUI.Instance.GetAttackTargetRect();
                }
                break;

            case ResourceGainVisualType.Score:
                targetRect = scoreTargetOverride;
                if (targetRect == null && PlayerResourceUI.Instance != null)
                {
                    targetRect = PlayerResourceUI.Instance.GetScoreTargetRect();
                }
                break;

            case ResourceGainVisualType.Mana:
            default:
                targetRect = manaTargetOverride;
                if (targetRect == null && PlayerResourceUI.Instance != null)
                {
                    targetRect = PlayerResourceUI.Instance.GetManaTargetRect();
                }
                break;
        }

        if (targetRect != null)
            return WorldToCanvasPosition(canvasRect, targetRect, uiCamera);

        Rect rect = canvasRect.rect;
        switch (resourceType)
        {
            case ResourceGainVisualType.Attack:
                return new Vector2(rect.width * 0.16f, -rect.height * 0.34f);

            case ResourceGainVisualType.Score:
                return new Vector2(0f, -rect.height * 0.34f);

            case ResourceGainVisualType.Mana:
            default:
                return new Vector2(-rect.width * 0.16f, -rect.height * 0.34f);
        }
    }

    private RectTransform InstantiateToken(ResourceGainVisualType resourceType, RectTransform canvasRect)
    {
        switch (resourceType)
        {
            case ResourceGainVisualType.Attack:
                if (attackTokenPrefab != null)
                    return Instantiate(attackTokenPrefab, canvasRect, false);
                break;

            case ResourceGainVisualType.Score:
                if (scoreTokenPrefab != null)
                    return Instantiate(scoreTokenPrefab, canvasRect, false);
                break;

            case ResourceGainVisualType.Mana:
            default:
                if (manaTokenPrefab != null)
                    return Instantiate(manaTokenPrefab, canvasRect, false);
                break;
        }

        return CreateFallbackToken(resourceType, canvasRect);
    }

    private RectTransform CreateFallbackToken(ResourceGainVisualType resourceType, RectTransform canvasRect)
    {
        GameObject tokenObject = new GameObject(
            "ResourceGainToken",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform tokenRect = tokenObject.GetComponent<RectTransform>();
        tokenRect.SetParent(canvasRect, false);
        tokenRect.sizeDelta = new Vector2(fallbackTokenSize, fallbackTokenSize);

        Image tokenImage = tokenObject.GetComponent<Image>();
        tokenImage.sprite = GetFallbackTokenSprite();
        tokenImage.color = GetFallbackTokenColor(resourceType);
        tokenImage.preserveAspect = true;
        tokenImage.raycastTarget = false;

        return tokenRect;
    }

    private Color GetFallbackTokenColor(ResourceGainVisualType resourceType)
    {
        switch (resourceType)
        {
            case ResourceGainVisualType.Attack:
                return attackFallbackColor;

            case ResourceGainVisualType.Score:
                return scoreFallbackColor;

            case ResourceGainVisualType.Mana:
            default:
                return manaFallbackColor;
        }
    }

    private static Sprite GetFallbackTokenSprite()
    {
        if (cachedFallbackTokenSprite != null)
            return cachedFallbackTokenSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = "ResourceGainFallbackToken";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = (size - 2) * 0.5f;
        float softEdge = 1.6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.Clamp01((distance - (radius - softEdge)) / Mathf.Max(softEdge, 0.0001f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        cachedFallbackTokenSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedFallbackTokenSprite;
    }

    private static float EvaluateAccelerateThenCruise(float t, float accelerationRatio)
    {
        float p = Mathf.Clamp(accelerationRatio, 0.05f, 0.95f);
        float a = 1f / (p * (1f - p * 0.5f));

        if (t <= p)
        {
            return 0.5f * a * t * t;
        }

        float distanceAtSwitch = 0.5f * a * p * p;
        float maxVelocity = a * p;
        return distanceAtSwitch + maxVelocity * (t - p);
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
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
}
