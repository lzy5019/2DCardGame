using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopPurchaseFxUI : MonoBehaviour
{
    #region 界面引用
    [Header("卡牌显示")]
    [SerializeField] private Image cardImage;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("粒子根节点")]
    [SerializeField] private RectTransform particleRoot;
    [SerializeField] private GameObject sparkDotPrefab;

    [Header("购买材质")]
    [SerializeField] private Material purchaseMaterialTemplate;
    #endregion

    #region 卡牌动画
    [Header("卡牌动画")]
    [SerializeField] private float introDuration = 0f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float introScaleMultiplier = 1f;
    [SerializeField] private float fadedScaleMultiplier = 0.9f;
    [SerializeField] private float introProgressTarget = 0.22f;
    [SerializeField] private float endFadeStart = 0.8f;
    [SerializeField] private string progressPropertyName = "_PurchaseProgress";
    #endregion

    #region 光点生成
    [Header("光点生成")]
    [SerializeField] private int minSparkCount = 20;
    [SerializeField] private int maxSparkCount = 30;
    [SerializeField] private Vector2 sparkSizeRange = new Vector2(20f, 100f);
    [SerializeField] private Vector2 sparkAlphaRange = new Vector2(0.45f, 1f);
    [SerializeField] private Vector2 scatterRadiusRange = new Vector2(20f, 45f);
    [SerializeField] private Vector2 scatterDurationRange = new Vector2(0.05f, 0.1f);
    [SerializeField] private Vector2 flyDurationRange = new Vector2(0.1f, 0.4f);
    [SerializeField] private float targetSpreadRadius = 50f;
    [SerializeField] private Color[] sparkColors =
    {
        new Color(1f, 0.95f, 0.72f, 1f),
        new Color(0.78f, 1f, 0.98f, 1f),
        new Color(0.6f, 0.95f, 1f, 1f),
    };
    #endregion

    #region 运行时缓存
    private readonly List<GameObject> spawnedSparkObjects = new List<GameObject>();
    private RectTransform cachedRect;
    private Vector3 originalRootScale = Vector3.one;
    private Material originalMaterial;
    private Material runtimePurchaseMaterial;
    private Coroutine currentPlayCoroutine;
    #endregion

    #region 生命周期
    private void Awake()
    {
        cachedRect = transform as RectTransform;
        AutoBindIfNeeded();
        CacheOriginalState();
        HideImmediate();
    }

    private void OnDisable()
    {
        StopAllRunningCoroutines();
        ResetToIdleState();
        ClearSpawnedSparks();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMaterial();
    }
    #endregion

    #region 播放接口
    public void PlayToTarget(Sprite cardSprite, RectTransform targetRect)
    {
        StopAllRunningCoroutines();
        currentPlayCoroutine = StartCoroutine(PlayToTargetRoutine(cardSprite, targetRect));
    }

    public IEnumerator PlayToTargetRoutine(Sprite cardSprite, RectTransform targetRect)
    {
        AutoBindIfNeeded();
        CacheOriginalState();
        ClearSpawnedSparks();

        if (cardImage == null || particleRoot == null || sparkDotPrefab == null || targetRect == null)
        {
            Debug.LogWarning("ShopPurchaseFxUI: 缺少必要引用，无法播放购买特效。");
            yield break;
        }

        if (!PrepareRuntimeMaterial())
        {
            Debug.LogWarning("ShopPurchaseFxUI: 缺少购买材质模板，无法播放购买特效。");
            yield break;
        }

        ResetVisualState(cardSprite);

        Vector2 sparkStartPosition = GetSparkStartPosition();
        Vector2 targetLocalPosition = ConvertWorldPositionToLocal(targetRect.position, particleRoot);

        yield return PlayCardPresentationRoutine(sparkStartPosition, targetLocalPosition);

        HideImmediate();
        currentPlayCoroutine = null;
    }

    public void HideImmediate()
    {
        ResetToIdleState();
        ClearSpawnedSparks();
    }
    #endregion

    #region 卡牌动画
    private IEnumerator PlayCardPresentationRoutine(Vector2 sparkStartPosition, Vector2 targetLocalPosition)
    {
        if (cachedRect == null)
            yield break;

        float clampedIntroDuration = Mathf.Max(0f, introDuration);
        float clampedFadeDuration = Mathf.Max(0f, fadeDuration);
        float totalDuration = clampedIntroDuration + clampedFadeDuration;
        bool sparksSpawned = false;
        float longestSparkDuration = 0f;

        if (totalDuration <= Mathf.Epsilon)
        {
            ApplyContinuousScale(1f, clampedIntroDuration, totalDuration);
            UpdatePurchaseProgressAndFade(clampedIntroDuration, clampedIntroDuration, clampedFadeDuration);
            longestSparkDuration = SpawnSparks(sparkStartPosition, targetLocalPosition);
            yield return new WaitForSeconds(longestSparkDuration);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float totalT = Mathf.Clamp01(elapsed / totalDuration);

            ApplyContinuousScale(totalT, clampedIntroDuration, totalDuration);
            UpdatePurchaseProgressAndFade(elapsed, clampedIntroDuration, clampedFadeDuration);

            if (!sparksSpawned && elapsed >= clampedIntroDuration)
            {
                longestSparkDuration = SpawnSparks(sparkStartPosition, targetLocalPosition);
                sparksSpawned = true;
            }

            yield return null;
        }

        ApplyContinuousScale(1f, clampedIntroDuration, totalDuration);
        UpdatePurchaseProgressAndFade(totalDuration, clampedIntroDuration, clampedFadeDuration);

        if (!sparksSpawned)
        {
            longestSparkDuration = SpawnSparks(sparkStartPosition, targetLocalPosition);
        }

        yield return new WaitForSeconds(longestSparkDuration);
    }

    private void ApplyContinuousScale(float totalT, float introPhaseDuration, float totalDuration)
    {
        if (cachedRect == null)
            return;

        float scaleMultiplier = EvaluateContinuousScaleMultiplier(totalT, introPhaseDuration, totalDuration);
        cachedRect.localScale = originalRootScale * scaleMultiplier;
    }

    private float EvaluateContinuousScaleMultiplier(float totalT, float introPhaseDuration, float totalDuration)
    {
        float clampedT = Mathf.Clamp01(totalT);

        if (totalDuration <= Mathf.Epsilon)
            return fadedScaleMultiplier;

        float introNormalizedTime = Mathf.Clamp01(introPhaseDuration / totalDuration);
        if (introNormalizedTime <= Mathf.Epsilon || introNormalizedTime >= 1f - Mathf.Epsilon)
        {
            return Mathf.Lerp(1f, fadedScaleMultiplier, EaseInOutCubic(clampedT));
        }

        float startScaleMultiplier = 1f;
        float midScaleMultiplier = introScaleMultiplier;
        float endScaleMultiplier = fadedScaleMultiplier;

        float firstSlope = (midScaleMultiplier - startScaleMultiplier) / introNormalizedTime;
        float secondSlope = (endScaleMultiplier - midScaleMultiplier) / (1f - introNormalizedTime);
        float middleSlope = (firstSlope + secondSlope) * 0.5f;

        if (clampedT <= introNormalizedTime)
        {
            return EvaluateHermiteSegment(
                clampedT,
                0f,
                introNormalizedTime,
                startScaleMultiplier,
                midScaleMultiplier,
                firstSlope,
                middleSlope
            );
        }

        return EvaluateHermiteSegment(
            clampedT,
            introNormalizedTime,
            1f,
            midScaleMultiplier,
            endScaleMultiplier,
            middleSlope,
            secondSlope
        );
    }

    private float EvaluateHermiteSegment(float t, float startTime, float endTime, float startValue, float endValue, float startSlope, float endSlope)
    {
        float segmentDuration = endTime - startTime;
        if (segmentDuration <= Mathf.Epsilon)
            return endValue;

        float normalizedT = Mathf.Clamp01((t - startTime) / segmentDuration);
        float t2 = normalizedT * normalizedT;
        float t3 = t2 * normalizedT;

        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + normalizedT;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;

        float value =
            h00 * startValue +
            h10 * segmentDuration * startSlope +
            h01 * endValue +
            h11 * segmentDuration * endSlope;

        float minValue = Mathf.Min(startValue, endValue);
        float maxValue = Mathf.Max(startValue, endValue);
        return Mathf.Clamp(value, minValue, maxValue);
    }

    private void UpdatePurchaseProgressAndFade(float elapsed, float introPhaseDuration, float fadePhaseDuration)
    {
        if (elapsed <= introPhaseDuration && introPhaseDuration > Mathf.Epsilon)
        {
            float introT = Mathf.Clamp01(elapsed / introPhaseDuration);
            float easedIntroT = EaseInOutCubic(introT);
            SetPurchaseProgress(Mathf.Lerp(0f, introProgressTarget, easedIntroT));

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
            }

            return;
        }

        float fadeElapsed = Mathf.Max(0f, elapsed - introPhaseDuration);
        float fadeT = fadePhaseDuration <= Mathf.Epsilon
            ? 1f
            : Mathf.Clamp01(fadeElapsed / fadePhaseDuration);
        float easedFadeT = EaseInOutCubic(fadeT);

        SetPurchaseProgress(Mathf.Lerp(introProgressTarget, 1f, easedFadeT));

        if (rootCanvasGroup != null)
        {
            float alphaFadeT = Mathf.InverseLerp(endFadeStart, 1f, fadeT);
            rootCanvasGroup.alpha = Mathf.Lerp(1f, 0f, alphaFadeT);
        }
    }
    #endregion

    #region 动画工具
    private float SpawnSparks(Vector2 sparkStartPosition, Vector2 targetLocalPosition)
    {
        int sparkCount = Random.Range(minSparkCount, maxSparkCount + 1);
        float longestSparkDuration = 0f;

        for (int i = 0; i < sparkCount; i++)
        {
            GameObject sparkObject = Instantiate(sparkDotPrefab, particleRoot, false);
            sparkObject.name = $"Spark_{i}";
            spawnedSparkObjects.Add(sparkObject);

            float sparkSize = Random.Range(sparkSizeRange.x, sparkSizeRange.y);
            float sparkAlpha = Random.Range(sparkAlphaRange.x, sparkAlphaRange.y);
            float scatterDistance = Random.Range(scatterRadiusRange.x, scatterRadiusRange.y);
            float scatterDuration = Random.Range(scatterDurationRange.x, scatterDurationRange.y);
            float flyDuration = Random.Range(flyDurationRange.x, flyDurationRange.y);
            float totalDuration = scatterDuration + flyDuration;

            if (totalDuration > longestSparkDuration)
            {
                longestSparkDuration = totalDuration;
            }

            Vector2 scatterDirection = Random.insideUnitCircle;
            if (scatterDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                scatterDirection = Vector2.up;
            }

            Vector2 scatterPosition = sparkStartPosition + scatterDirection.normalized * scatterDistance;
            Vector2 targetOffset = Random.insideUnitCircle * targetSpreadRadius;
            Color sparkColor = GetRandomSparkColor();

            StartCoroutine(
                AnimateSparkRoutine(
                    sparkObject,
                    sparkStartPosition,
                    scatterPosition,
                    targetLocalPosition + targetOffset,
                    sparkSize,
                    sparkAlpha,
                    sparkColor,
                    scatterDuration,
                    flyDuration
                )
            );
        }

        return longestSparkDuration;
    }
    #endregion

    #region 光点动画
    private IEnumerator AnimateSparkRoutine(
        GameObject sparkObject,
        Vector2 startPosition,
        Vector2 scatterPosition,
        Vector2 targetPosition,
        float sparkSize,
        float sparkAlpha,
        Color sparkColor,
        float scatterDuration,
        float flyDuration)
    {
        if (sparkObject == null)
            yield break;

        RectTransform sparkRect = sparkObject.GetComponent<RectTransform>();
        Image sparkImage = sparkObject.GetComponent<Image>();
        CanvasGroup sparkCanvasGroup = sparkObject.GetComponent<CanvasGroup>();

        if (sparkRect == null || sparkImage == null)
        {
            spawnedSparkObjects.Remove(sparkObject);
            Destroy(sparkObject);
            yield break;
        }

        sparkRect.anchoredPosition = startPosition;
        sparkRect.localScale = Vector3.one;
        sparkRect.sizeDelta = Vector2.one * sparkSize;

        Color startColor = sparkColor;
        startColor.a = sparkAlpha;
        sparkImage.color = startColor;

        if (sparkCanvasGroup != null)
        {
            sparkCanvasGroup.alpha = sparkAlpha;
        }

        float elapsed = 0f;
        while (elapsed < scatterDuration)
        {
            if (sparkObject == null || sparkRect == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scatterDuration);
            float easedT = EaseOutCubic(t);
            sparkRect.anchoredPosition = Vector2.Lerp(startPosition, scatterPosition, easedT);
            yield return null;
        }

        elapsed = 0f;
        Vector3 startScale = sparkRect.localScale;
        Vector3 endScale = Vector3.one * 0.2f;

        while (elapsed < flyDuration)
        {
            if (sparkObject == null || sparkRect == null || sparkImage == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            float easedT = EaseInOutCubic(t);

            sparkRect.anchoredPosition = Vector2.Lerp(scatterPosition, targetPosition, easedT);
            sparkRect.localScale = Vector3.Lerp(startScale, endScale, easedT);

            float currentAlpha = Mathf.Lerp(sparkAlpha, 0f, easedT);
            if (sparkCanvasGroup != null)
            {
                sparkCanvasGroup.alpha = currentAlpha;
            }
            else
            {
                Color currentColor = sparkImage.color;
                currentColor.a = currentAlpha;
                sparkImage.color = currentColor;
            }

            yield return null;
        }

        spawnedSparkObjects.Remove(sparkObject);
        Destroy(sparkObject);
    }
    #endregion

    #region 工具方法
    private void AutoBindIfNeeded()
    {
        if (cardImage == null)
        {
            Transform cardImageTransform = transform.Find("Image");
            if (cardImageTransform != null)
            {
                cardImage = cardImageTransform.GetComponent<Image>();
            }
        }

        if (particleRoot == null)
        {
            Transform particleRootTransform = transform.Find("Particle Root");
            if (particleRootTransform != null)
            {
                particleRoot = particleRootTransform as RectTransform;
            }
        }

        if (rootCanvasGroup == null)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void CacheOriginalState()
    {
        if (cachedRect != null)
        {
            originalRootScale = cachedRect.localScale;
        }

        if (cardImage != null)
        {
            originalMaterial = cardImage.material;
        }
    }

    private bool PrepareRuntimeMaterial()
    {
        if (runtimePurchaseMaterial != null)
            return true;

        if (purchaseMaterialTemplate != null)
        {
            runtimePurchaseMaterial = new Material(purchaseMaterialTemplate);
        }
        else
        {
            Shader purchaseShader = Shader.Find("UI/PurchaseArcaneAbsorb");
            if (purchaseShader == null)
                return false;

            runtimePurchaseMaterial = new Material(purchaseShader);
        }

        runtimePurchaseMaterial.name = "Runtime Purchase Arcane Absorb";
        return true;
    }

    private void ResetVisualState(Sprite cardSprite)
    {
        if (cardImage != null)
        {
            cardImage.sprite = cardSprite;
            cardImage.enabled = cardSprite != null;
            cardImage.material = runtimePurchaseMaterial;

            Color color = cardImage.color;
            color.a = 1f;
            cardImage.color = color;
        }

        if (cachedRect != null)
        {
            cachedRect.localScale = originalRootScale;
        }

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
        }

        SetPurchaseProgress(0f);
    }

    private void ResetToIdleState()
    {
        if (cardImage != null)
        {
            cardImage.sprite = null;
            cardImage.enabled = false;
            cardImage.material = originalMaterial;

            Color color = cardImage.color;
            color.a = 0f;
            cardImage.color = color;
        }

        if (cachedRect != null)
        {
            cachedRect.localScale = originalRootScale;
        }

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
        }

        SetPurchaseProgress(0f);
    }

    private void ClearSpawnedSparks()
    {
        for (int i = 0; i < spawnedSparkObjects.Count; i++)
        {
            if (spawnedSparkObjects[i] != null)
            {
                Destroy(spawnedSparkObjects[i]);
            }
        }

        spawnedSparkObjects.Clear();
    }

    private void StopAllRunningCoroutines()
    {
        StopAllCoroutines();
        currentPlayCoroutine = null;
    }

    private void ReleaseRuntimeMaterial()
    {
        if (runtimePurchaseMaterial == null)
            return;

        Destroy(runtimePurchaseMaterial);
        runtimePurchaseMaterial = null;
    }

    private void SetPurchaseProgress(float progress)
    {
        if (runtimePurchaseMaterial == null || string.IsNullOrEmpty(progressPropertyName))
            return;

        runtimePurchaseMaterial.SetFloat(progressPropertyName, Mathf.Clamp01(progress));
    }

    private Vector2 GetSparkStartPosition()
    {
        RectTransform startRect = cardImage != null ? cardImage.rectTransform : cachedRect;
        if (startRect == null || particleRoot == null)
        {
            return Vector2.zero;
        }

        return ConvertWorldPositionToLocal(startRect.position, particleRoot);
    }

    private Vector2 ConvertWorldPositionToLocal(Vector3 worldPosition, RectTransform targetLocalRoot)
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Camera canvasCamera = null;

        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = rootCanvas.worldCamera;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetLocalRoot,
            screenPoint,
            canvasCamera,
            out Vector2 localPoint
        );

        return localPoint;
    }

    private Color GetRandomSparkColor()
    {
        if (sparkColors == null || sparkColors.Length == 0)
        {
            return Color.white;
        }

        int randomIndex = Random.Range(0, sparkColors.Length);
        return sparkColors[randomIndex];
    }

    private float EaseOutCubic(float t)
    {
        float oneMinusT = 1f - t;
        return 1f - oneMinusT * oneMinusT * oneMinusT;
    }

    private float EaseInOutCubic(float t)
    {
        if (t < 0.5f)
        {
            return 4f * t * t * t;
        }

        float adjusted = -2f * t + 2f;
        return 1f - (adjusted * adjusted * adjusted) / 2f;
    }
    #endregion
}
