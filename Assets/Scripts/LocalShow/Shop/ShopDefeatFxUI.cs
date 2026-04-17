using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ShopDefeatFxUI : MonoBehaviour
{
    #region 界面引用
    [Header("卡牌显示")]
    [SerializeField] private Image cardImage;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("击败材质")]
    [SerializeField] private Material defeatMaterialTemplate;
    #endregion

    #region 动画参数
    [Header("击败动画")]
    [SerializeField] private float defeatDuration = 0.55f;
    [SerializeField] private float introScaleMultiplier = 1.12f;
    [SerializeField, Range(0.05f, 1f)] private float scaleUpProgress = 0.45f;
    [SerializeField] private float endFadeStart = 0.82f;
    [SerializeField] private string progressPropertyName = "_DefeatProgress";
    #endregion

    #region 运行时缓存
    private RectTransform cachedRect;
    private Vector3 originalRootScale = Vector3.one;
    private Material runtimeDefeatMaterial;
    private Material originalMaterial;
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
        StopCurrentPlayRoutine();
        ResetToIdleState();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMaterial();
    }
    #endregion

    #region 播放接口
    public void PlayDefeat(Sprite cardSprite)
    {
        StopCurrentPlayRoutine();
        currentPlayCoroutine = StartCoroutine(PlayDefeatRoutine(cardSprite));
    }

    public IEnumerator PlayDefeatRoutine(Sprite cardSprite)
    {
        AutoBindIfNeeded();
        CacheOriginalState();

        if (cardImage == null)
        {
            Debug.LogWarning("ShopDefeatFxUI: 缺少卡牌 Image，无法播放击败特效。");
            yield break;
        }

        if (!PrepareRuntimeMaterial())
        {
            Debug.LogWarning("ShopDefeatFxUI: 缺少击败材质模板，无法播放击败特效。");
            yield break;
        }

        ResetVisualState(cardSprite);

        float elapsed = 0f;
        while (elapsed < defeatDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / defeatDuration);
            float easedT = EaseInOutCubic(t);

            runtimeDefeatMaterial.SetFloat(progressPropertyName, easedT);
            UpdateScale(t);

            if (rootCanvasGroup != null)
            {
                float alphaFadeT = Mathf.InverseLerp(endFadeStart, 1f, t);
                rootCanvasGroup.alpha = Mathf.Lerp(1f, 0f, alphaFadeT);
            }

            yield return null;
        }

        HideImmediate();
    }

    public void HideImmediate()
    {
        ResetToIdleState();
    }
    #endregion

    #region 初始化
    private void AutoBindIfNeeded()
    {
        if (cardImage == null)
        {
            Transform imageTransform = transform.Find("Image");
            if (imageTransform != null)
            {
                cardImage = imageTransform.GetComponent<Image>();
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
        if (runtimeDefeatMaterial != null)
            return true;

        if (defeatMaterialTemplate != null)
        {
            runtimeDefeatMaterial = new Material(defeatMaterialTemplate);
        }
        else
        {
            Shader defeatShader = Shader.Find("UI/DefeatSlashAsh");
            if (defeatShader == null)
                return false;

            runtimeDefeatMaterial = new Material(defeatShader);
        }

        runtimeDefeatMaterial.name = "Runtime Defeat Slash Ash";
        return true;
    }

    private void ResetVisualState(Sprite cardSprite)
    {
        if (cardImage == null)
            return;

        cardImage.sprite = cardSprite;
        cardImage.enabled = cardSprite != null;
        cardImage.material = runtimeDefeatMaterial;

        if (runtimeDefeatMaterial != null)
        {
            runtimeDefeatMaterial.SetFloat(progressPropertyName, 0f);
        }

        if (cachedRect != null)
        {
            cachedRect.localScale = originalRootScale;
        }

        Color color = cardImage.color;
        color.a = 1f;
        cardImage.color = color;

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
        }
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

        if (runtimeDefeatMaterial != null)
        {
            runtimeDefeatMaterial.SetFloat(progressPropertyName, 0f);
        }

        if (cachedRect != null)
        {
            cachedRect.localScale = originalRootScale;
        }

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
        }
    }

    private void UpdateScale(float normalizedProgress)
    {
        if (cachedRect == null)
            return;

        float scaleProgress = Mathf.Clamp01(normalizedProgress / Mathf.Max(scaleUpProgress, 0.05f));
        float easedScaleProgress = EaseOutCubic(scaleProgress);
        float scaleMultiplier = Mathf.Lerp(1f, introScaleMultiplier, easedScaleProgress);
        cachedRect.localScale = originalRootScale * scaleMultiplier;
    }

    private void ReleaseRuntimeMaterial()
    {
        if (runtimeDefeatMaterial == null)
            return;

        Destroy(runtimeDefeatMaterial);
        runtimeDefeatMaterial = null;
    }

    private void StopCurrentPlayRoutine()
    {
        if (currentPlayCoroutine == null)
            return;

        StopCoroutine(currentPlayCoroutine);
        currentPlayCoroutine = null;
    }
    #endregion

    #region 缓动
    private float EaseInOutCubic(float t)
    {
        if (t < 0.5f)
        {
            return 4f * t * t * t;
        }

        float adjusted = -2f * t + 2f;
        return 1f - (adjusted * adjusted * adjusted) / 2f;
    }

    private float EaseOutCubic(float t)
    {
        float oneMinusT = 1f - t;
        return 1f - oneMinusT * oneMinusT * oneMinusT;
    }
    #endregion
}
