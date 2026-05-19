using TMPro;
using UnityEngine;

public class ScorePoolUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text scorePoolText;

    [Header("Display")]
    [SerializeField] private string prefix = "";
    [SerializeField] private string suffix = "";
    [SerializeField] private string unavailableText = "-";

    private MatchManager observedMatchManager;
    private int lastDisplayedValue = int.MinValue;

    private void OnEnable()
    {
        TryBindMatchManager();
        RefreshFromCurrentValue();
    }

    private void OnDisable()
    {
        UnbindMatchManager();
    }

    private void Update()
    {
        if (observedMatchManager == null)
        {
            TryBindMatchManager();
            RefreshFromCurrentValue();
        }
    }

    private void TryBindMatchManager()
    {
        MatchManager currentMatchManager = MatchManager.Instance;
        if (currentMatchManager == observedMatchManager)
            return;

        UnbindMatchManager();
        observedMatchManager = currentMatchManager;

        if (observedMatchManager != null)
        {
            observedMatchManager.RemainingScorePoolChanged += HandleRemainingScorePoolChanged;
        }
    }

    private void UnbindMatchManager()
    {
        if (observedMatchManager != null)
        {
            observedMatchManager.RemainingScorePoolChanged -= HandleRemainingScorePoolChanged;
            observedMatchManager = null;
        }
    }

    private void HandleRemainingScorePoolChanged(int newValue)
    {
        SetDisplayedValue(newValue);
    }

    private void RefreshFromCurrentValue()
    {
        if (observedMatchManager == null)
        {
            SetUnavailable();
            return;
        }

        SetDisplayedValue(observedMatchManager.GetRemainingScorePool());
    }

    private void SetDisplayedValue(int value)
    {
        int clampedValue = Mathf.Max(0, value);
        if (clampedValue == lastDisplayedValue && scorePoolText != null)
            return;

        lastDisplayedValue = clampedValue;

        if (scorePoolText != null)
        {
            scorePoolText.text = $"{prefix}{clampedValue}{suffix}";
        }
    }

    private void SetUnavailable()
    {
        lastDisplayedValue = int.MinValue;

        if (scorePoolText != null)
        {
            scorePoolText.text = unavailableText;
        }
    }
}
