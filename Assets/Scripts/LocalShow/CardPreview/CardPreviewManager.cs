using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardPreviewManager : MonoBehaviour
{
    [Serializable]
    public class EffectKeywordDefinition
    {
        public int effectId;
        public string title;

        [TextArea(2, 5)]
        public string description;
    }

    public static CardPreviewManager Instance;

    [Header("基础预览")]
    public GameObject previewCanvas;
    public Image previewImage;
    public Image derivedCardImage;

    [Header("关键词面板")]
    public GameObject keywordPanel;
    public Transform keywordRoot;
    public GameObject keywordPrefab;

    [Header("关键词动画")]
    public float keywordRevealDelay = 0.8f;
    public float keywordRevealDuration = 0.2f;
    public Vector2 keywordFloatOffset = new Vector2(40f, 0f);

    [Header("EffectId 文案配置")]
    public List<EffectKeywordDefinition> effectKeywordDefinitions = new List<EffectKeywordDefinition>();

    [Header("衍生卡预览")]
    public float derivedCardRevealDelay = 0.9f;
    public float derivedCardSwitchInterval = 1.2f;

    private readonly List<GameObject> spawnedKeywordItems = new List<GameObject>();

    private CanvasGroup keywordCanvasGroup;
    private RectTransform keywordPanelRect;
    private Vector2 keywordPanelBaseAnchoredPosition;
    private Coroutine keywordRevealCoroutine;
    private Coroutine derivedCardRevealCoroutine;
    private UnityEngine.Object previewOwner;
    private bool hideWhenRightMouseReleased;

    private void Awake()
    {
        Instance = this;
        AutoBindIfNeeded();
        PrepareKeywordPanel();
        HidePreview();
    }

    private void Update()
    {
        if (previewCanvas == null || !previewCanvas.activeSelf)
            return;

        if (hideWhenRightMouseReleased && !Input.GetMouseButton(1))
        {
            HidePreview();
            return;
        }

        if (!IsPreviewOwnerAlive())
        {
            HidePreview();
        }
    }

    public void ShowPreview(Sprite sprite)
    {
        ShowPreview(sprite, null);
    }

    public void ShowPreview(Sprite sprite, CardData cardData)
    {
        ShowPreview(sprite, cardData, null, false);
    }

    public void ShowPreview(Sprite sprite, CardData cardData, UnityEngine.Object owner, bool autoHideOnRightMouseRelease)
    {
        if (sprite == null)
            return;

        AutoBindIfNeeded();

        if (previewCanvas != null)
        {
            previewCanvas.SetActive(true);
        }

        if (previewImage != null)
        {
            previewImage.sprite = sprite;
            previewImage.enabled = true;
            previewImage.preserveAspect = true;
        }

        previewOwner = owner;
        hideWhenRightMouseReleased = autoHideOnRightMouseRelease;

        ResetKeywordPanelVisual();
        RebuildKeywordItems(cardData);
        ResetDerivedCardVisual();

        if (keywordRevealCoroutine != null)
        {
            StopCoroutine(keywordRevealCoroutine);
            keywordRevealCoroutine = null;
        }
        if (derivedCardRevealCoroutine != null)
        {
            StopCoroutine(derivedCardRevealCoroutine);
            derivedCardRevealCoroutine = null;
        }

        if (spawnedKeywordItems.Count > 0)
        {
            keywordRevealCoroutine = StartCoroutine(RevealKeywordPanelRoutine());
        }

        List<Sprite> derivedCardSprites = CollectDerivedCardSprites(cardData);
        if (derivedCardSprites.Count > 0)
        {
            derivedCardRevealCoroutine = StartCoroutine(RevealDerivedCardsRoutine(derivedCardSprites));
        }
    }

    public void HidePreview()
    {
        previewOwner = null;
        hideWhenRightMouseReleased = false;

        if (keywordRevealCoroutine != null)
        {
            StopCoroutine(keywordRevealCoroutine);
            keywordRevealCoroutine = null;
        }
        if (derivedCardRevealCoroutine != null)
        {
            StopCoroutine(derivedCardRevealCoroutine);
            derivedCardRevealCoroutine = null;
        }

        ClearKeywordItems();
        ResetDerivedCardVisual();

        if (keywordPanel != null)
        {
            keywordPanel.SetActive(false);
        }

        if (previewCanvas != null)
        {
            previewCanvas.SetActive(false);
        }
    }

    public void HidePreviewIfOwner(UnityEngine.Object owner)
    {
        if (owner == null)
            return;
        if (previewOwner != owner)
            return;

        HidePreview();
    }

    private void AutoBindIfNeeded()
    {
        if (previewCanvas == null)
        {
            previewCanvas = gameObject;
        }

        if (previewImage == null && previewCanvas != null)
        {
            Transform imageTransform = previewCanvas.transform.Find("CardPreviewPanel/CardPreviewImage");
            if (imageTransform != null)
            {
                previewImage = imageTransform.GetComponent<Image>();
            }
        }

        if (derivedCardImage == null && previewCanvas != null)
        {
            Transform derivedImageTransform = previewCanvas.transform.Find("CardPreviewPanel/DerivedCardImage");
            if (derivedImageTransform != null)
            {
                derivedCardImage = derivedImageTransform.GetComponent<Image>();
            }
        }

        if (keywordPanel == null && previewCanvas != null)
        {
            Transform keywordPanelTransform = previewCanvas.transform.Find("CardPreviewPanel/Keyword Panel");
            if (keywordPanelTransform != null)
            {
                keywordPanel = keywordPanelTransform.gameObject;
            }
        }

        if (keywordRoot == null && keywordPanel != null)
        {
            keywordRoot = keywordPanel.transform;
        }

        if (keywordPrefab == null && keywordPanel != null)
        {
            Transform keywordPrefabTransform = keywordPanel.transform.Find("Keyword Prefab");
            if (keywordPrefabTransform != null)
            {
                keywordPrefab = keywordPrefabTransform.gameObject;
            }
        }
    }

    private void PrepareKeywordPanel()
    {
        if (keywordPanel == null)
            return;

        keywordPanelRect = keywordPanel.GetComponent<RectTransform>();
        if (keywordPanelRect != null)
        {
            keywordPanelBaseAnchoredPosition = keywordPanelRect.anchoredPosition;
        }

        keywordCanvasGroup = keywordPanel.GetComponent<CanvasGroup>();
        if (keywordCanvasGroup == null)
        {
            keywordCanvasGroup = keywordPanel.AddComponent<CanvasGroup>();
        }

        if (keywordPrefab != null && keywordPrefab.scene.IsValid())
        {
            keywordPrefab.SetActive(false);
        }

        keywordPanel.SetActive(false);
        keywordCanvasGroup.alpha = 0f;
    }

    private void ResetKeywordPanelVisual()
    {
        if (keywordPanel == null)
            return;

        if (keywordPanelRect == null)
        {
            keywordPanelRect = keywordPanel.GetComponent<RectTransform>();
        }

        if (keywordCanvasGroup == null)
        {
            keywordCanvasGroup = keywordPanel.GetComponent<CanvasGroup>();
            if (keywordCanvasGroup == null)
            {
                keywordCanvasGroup = keywordPanel.AddComponent<CanvasGroup>();
            }
        }

        keywordPanel.SetActive(false);
        keywordCanvasGroup.alpha = 0f;

        if (keywordPanelRect != null)
        {
            keywordPanelRect.anchoredPosition = keywordPanelBaseAnchoredPosition + keywordFloatOffset;
        }
    }

    private void ResetDerivedCardVisual()
    {
        if (derivedCardImage == null)
            return;

        derivedCardImage.sprite = null;
        derivedCardImage.enabled = false;
    }

    private void RebuildKeywordItems(CardData cardData)
    {
        ClearKeywordItems();

        if (cardData == null || cardData.effectId == null || cardData.effectId.Count == 0)
            return;
        if (keywordPanel == null || keywordRoot == null || keywordPrefab == null)
            return;

        for (int i = 0; i < cardData.effectId.Count; i++)
        {
            int effectId = cardData.effectId[i];
            GameObject keywordItem = Instantiate(keywordPrefab, keywordRoot);
            keywordItem.SetActive(true);

            ApplyKeywordDefinition(keywordItem, effectId);
            spawnedKeywordItems.Add(keywordItem);
        }
    }

    private void ClearKeywordItems()
    {
        for (int i = 0; i < spawnedKeywordItems.Count; i++)
        {
            if (spawnedKeywordItems[i] != null)
            {
                Destroy(spawnedKeywordItems[i]);
            }
        }

        spawnedKeywordItems.Clear();
    }

    private List<Sprite> CollectDerivedCardSprites(CardData cardData)
    {
        List<Sprite> result = new List<Sprite>();

        if (cardData == null || cardData.derivedCards == null || cardData.derivedCards.Count == 0)
            return result;

        for (int i = 0; i < cardData.derivedCards.Count; i++)
        {
            CardData derivedCardData = cardData.derivedCards[i];
            if (derivedCardData == null || derivedCardData.cardSprite == null)
                continue;

            result.Add(derivedCardData.cardSprite);
        }

        return result;
    }

    private IEnumerator RevealKeywordPanelRoutine()
    {
        if (keywordPanel == null)
            yield break;

        keywordPanel.SetActive(true);

        if (keywordRevealDelay > 0f)
        {
            yield return new WaitForSeconds(keywordRevealDelay);
        }

        if (keywordCanvasGroup == null)
            yield break;

        float elapsed = 0f;
        Vector2 startPosition = keywordPanelBaseAnchoredPosition + keywordFloatOffset;
        Vector2 targetPosition = keywordPanelBaseAnchoredPosition;

        keywordCanvasGroup.alpha = 0f;
        if (keywordPanelRect != null)
        {
            keywordPanelRect.anchoredPosition = startPosition;
        }

        float duration = Mathf.Max(0.01f, keywordRevealDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            keywordCanvasGroup.alpha = t;
            if (keywordPanelRect != null)
            {
                keywordPanelRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            }

            yield return null;
        }

        keywordCanvasGroup.alpha = 1f;
        if (keywordPanelRect != null)
        {
            keywordPanelRect.anchoredPosition = targetPosition;
        }

        keywordRevealCoroutine = null;
    }

    private IEnumerator RevealDerivedCardsRoutine(List<Sprite> derivedCardSprites)
    {
        if (derivedCardImage == null || derivedCardSprites == null || derivedCardSprites.Count == 0)
            yield break;

        if (derivedCardRevealDelay > 0f)
        {
            yield return new WaitForSeconds(derivedCardRevealDelay);
        }

        derivedCardImage.enabled = true;
        derivedCardImage.preserveAspect = true;
        derivedCardImage.sprite = derivedCardSprites[0];

        if (derivedCardSprites.Count == 1)
        {
            derivedCardRevealCoroutine = null;
            yield break;
        }

        float switchInterval = Mathf.Max(0.1f, derivedCardSwitchInterval);
        int spriteIndex = 0;

        while (true)
        {
            yield return new WaitForSeconds(switchInterval);

            spriteIndex = (spriteIndex + 1) % derivedCardSprites.Count;
            derivedCardImage.sprite = derivedCardSprites[spriteIndex];
        }
    }

    private void ApplyKeywordDefinition(GameObject keywordItem, int effectId)
    {
        EffectKeywordDefinition definition = FindKeywordDefinition(effectId);

        string title = definition != null && !string.IsNullOrEmpty(definition.title)
            ? definition.title
            : $"Effect {effectId}";
        string description = definition != null
            ? definition.description
            : $"在 CardPreviewManager 中补充 effectId {effectId} 的说明。";

        SetChildText(keywordItem.transform, "title", title);
        SetChildText(keywordItem.transform, "text", description);
    }

    private EffectKeywordDefinition FindKeywordDefinition(int effectId)
    {
        for (int i = 0; i < effectKeywordDefinitions.Count; i++)
        {
            EffectKeywordDefinition definition = effectKeywordDefinitions[i];
            if (definition != null && definition.effectId == effectId)
            {
                return definition;
            }
        }

        return null;
    }

    private void SetChildText(Transform root, string childName, string value)
    {
        if (root == null)
            return;

        Transform child = root.Find(childName);
        if (child == null)
            return;

        TMP_Text tmpText = child.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = value;
            return;
        }

        Text legacyText = child.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.text = value;
        }
    }

    private bool IsPreviewOwnerAlive()
    {
        if (previewOwner == null)
            return true;

        if (previewOwner is Behaviour behaviour)
            return behaviour.isActiveAndEnabled;

        if (previewOwner is GameObject gameObject)
            return gameObject.activeInHierarchy;

        if (previewOwner is Component component)
            return component.gameObject.activeInHierarchy;

        return true;
    }

}
