using Mirror;
using TMPro;
using UnityEngine;

public class TurnTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;

    private void Update()
    {
        if (timerText == null)
            return;

        if (MatchManager.Instance == null || !MatchManager.Instance.gameStarted)
        {
            timerText.text = "";
            return;
        }

        if (MatchManager.Instance.waitingForPublicActionDrain)
        {
            timerText.text = "--";
            return;
        }

        double remain = MatchManager.Instance.currentTurnEndTime - NetworkTime.time;
        remain = Mathf.Max(0f, (float)remain);

        timerText.text = Mathf.CeilToInt((float)remain).ToString();
    }
}
