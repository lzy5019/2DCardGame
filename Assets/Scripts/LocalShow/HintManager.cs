using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HintManager : MonoBehaviour
{
    public static HintManager Instance { get; private set; }

    [Header("引用")]
    [SerializeField] private RectTransform hintPanel;
    [SerializeField] private TMP_Text hintTemplate;

    [Header("时间设置")]
    [SerializeField] private float visibleDuration = 0.9f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("动画设置")]
    [SerializeField] private float moveUpDistance = 30f;

    private readonly List<HintEntry> activeHints = new List<HintEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (hintTemplate != null)
        {
            hintTemplate.gameObject.SetActive(false);
        }
    }

    public void ShowHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (hintPanel == null || hintTemplate == null)
        {
            Debug.LogWarning("HintManager is missing hintPanel or hintTemplate.");
            return;
        }

        TMP_Text hintText = Instantiate(hintTemplate, hintPanel);
        hintText.gameObject.SetActive(true);
        hintText.transform.SetAsLastSibling();

        RectTransform rect = hintText.rectTransform;
        Vector2 startPosition = rect.anchoredPosition;

        CanvasGroup canvasGroup = hintText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = hintText.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;

        HintEntry newEntry = new HintEntry
        {
            text = hintText,
            rectTransform = rect,
            canvasGroup = canvasGroup,
            startAnchoredPosition = startPosition
        };

        hintText.text = message;
        newEntry.coroutine = StartCoroutine(PlayHintRoutine(newEntry));

        activeHints.Add(newEntry);
    }

    public void ClearHints()
    {
        foreach (HintEntry entry in activeHints)
        {
            if (entry.coroutine != null)
            {
                StopCoroutine(entry.coroutine);
            }

            if (entry.text != null)
            {
                Destroy(entry.text.gameObject);
            }
        }

        activeHints.Clear();
    }

    private IEnumerator PlayHintRoutine(HintEntry entry)
    {
        float elapsed = 0f;
        float totalDuration = visibleDuration + fadeDuration;
        Vector2 targetPosition = entry.startAnchoredPosition + Vector2.up * moveUpDistance;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float normalized = Mathf.Clamp01(elapsed / totalDuration);
            float fadeStartTime = visibleDuration / totalDuration;
            float fadeT = normalized <= fadeStartTime
                ? 0f
                : Mathf.InverseLerp(fadeStartTime, 1f, normalized);

            if (entry.rectTransform != null)
            {
                entry.rectTransform.anchoredPosition = Vector2.Lerp(
                    entry.startAnchoredPosition,
                    targetPosition,
                    normalized
                );
            }

            if (entry.canvasGroup != null)
            {
                entry.canvasGroup.alpha = 1f - fadeT;
            }

            yield return null;
        }

        activeHints.Remove(entry);

        if (entry.text != null)
        {
            Destroy(entry.text.gameObject);
        }
    }

    private sealed class HintEntry
    {
        public TMP_Text text;
        public RectTransform rectTransform;
        public CanvasGroup canvasGroup;
        public Coroutine coroutine;
        public Vector2 startAnchoredPosition;
    }


}

