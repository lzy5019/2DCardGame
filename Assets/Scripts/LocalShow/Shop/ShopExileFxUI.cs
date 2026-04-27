using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ShopExileFxUI : MonoBehaviour
{
    [Header("Card Display")]
    [SerializeField] private Image cardImage;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Exile Material")]
    [SerializeField] private Material exileMaterialTemplate;

    [Header("Exile Animation")]
    [SerializeField] private float exileDuration = 0.62f;
    [SerializeField] private float introScaleMultiplier = 1.08f;
    [SerializeField, Range(0.05f, 1f)] private float scaleUpProgress = 0.38f;
    [SerializeField] private float endFadeStart = 0.78f;
    [SerializeField] private string progressPropertyName = "_ExileProgress";

    private RectTransform cachedRect;
    private Vector3 originalRootScale = Vector3.one;
    private Material runtimeExileMaterial;
    private Material originalMaterial;
    private Coroutine currentPlayCoroutine;

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

    public void PlayExile(Sprite cardSprite)
    {
        StopCurrentPlayRoutine();
        currentPlayCoroutine = StartCoroutine(PlayExileRoutine(cardSprite));
    }

    public IEnumerator PlayExileRoutine(Sprite cardSprite)
    {
        CacheOriginalState();

        if (cardImage == null)
        {
            Debug.LogWarning("ShopExileFxUI: missing card Image, unable to play exile effect.");
            yield break;
        }

        if (!PrepareRuntimeMaterial())
        {
            Debug.LogWarning("ShopExileFxUI: missing exile material template, unable to play exile effect.");
            yield break;
        }

        ResetVisualState(cardSprite);

        float elapsed = 0f;
        while (elapsed < exileDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / exileDuration);
            float easedT = EaseInOutCubic(t);

            runtimeExileMaterial.SetFloat(progressPropertyName, easedT);
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
        if (runtimeExileMaterial != null)
            return true;

        if (exileMaterialTemplate != null)
        {
            runtimeExileMaterial = new Material(exileMaterialTemplate);
        }
        else
        {
            Shader exileShader = Shader.Find("UI/ExileVoidShatter");
            if (exileShader == null)
                return false;

            runtimeExileMaterial = new Material(exileShader);
        }

        runtimeExileMaterial.name = "Runtime Exile Void Shatter";
        return true;
    }

    private void ResetVisualState(Sprite cardSprite)
    {
        if (cardImage == null)
            return;

        cardImage.sprite = cardSprite;
        cardImage.enabled = cardSprite != null;
        cardImage.material = runtimeExileMaterial;

        if (runtimeExileMaterial != null)
        {
            runtimeExileMaterial.SetFloat(progressPropertyName, 0f);
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

        if (runtimeExileMaterial != null)
        {
            runtimeExileMaterial.SetFloat(progressPropertyName, 0f);
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
        if (runtimeExileMaterial == null)
            return;

        Destroy(runtimeExileMaterial);
        runtimeExileMaterial = null;
    }

    private void StopCurrentPlayRoutine()
    {
        if (currentPlayCoroutine == null)
            return;

        StopCoroutine(currentPlayCoroutine);
        currentPlayCoroutine = null;
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

    private float EaseOutCubic(float t)
    {
        float oneMinusT = 1f - t;
        return 1f - oneMinusT * oneMinusT * oneMinusT;
    }
}
