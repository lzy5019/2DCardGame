using Mirror;
using TMPro;
using UnityEngine;

public class TurnTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text turnCountText;

    private void Update()
    {
        MatchManager matchManager = MatchManager.Instance;

        if (timerText == null && turnCountText == null)
            return;

        if (matchManager == null || !matchManager.gameStarted)
        {
            if (timerText != null)
            {
                timerText.text = "";
            }

            if (turnCountText != null)
            {
                turnCountText.text = "";
            }

            return;
        }

        if (turnCountText != null)
        {
            turnCountText.text = $"回合{matchManager.turnCount}";
        }

        if (timerText == null)
            return;

        if (matchManager.waitingForPublicActionDrain)
        {
            timerText.text = "--";
            return;
        }

        double remain = matchManager.currentTurnEndTime - NetworkTime.time;
        remain = Mathf.Max(0f, (float)remain);

        timerText.text = Mathf.CeilToInt((float)remain).ToString();
    }
}
