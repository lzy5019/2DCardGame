using UnityEngine;
using UnityEngine.UI;

public class PlayerEndTurn : MonoBehaviour
{
    public GameObject endTurnButtonObject;
    public Button endTurnButton;

    private PlayerState localPlayer;

    private void Start()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnClickEndTurn);
        }
    }

    private void Update()
    {
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        RefreshButtonVisible();
    }

    private void FindLocalPlayer()
    {
        PlayerState[] players = FindObjectsOfType<PlayerState>();
        foreach (PlayerState player in players)
        {
            if (player.isLocalPlayer)
            {
                localPlayer = player;
                break;
            }
        }
    }

    private void RefreshButtonVisible()
    {
        if (endTurnButtonObject == null) return;

        bool shouldShow = false;

        if (localPlayer != null && MatchManager.Instance != null && MatchManager.Instance.gameStarted)
        {
            PlayerState currentPlayer = MatchManager.Instance.GetCurrentPlayer();
            shouldShow = (currentPlayer == localPlayer);
        }

        endTurnButtonObject.SetActive(shouldShow);
    }

    private void OnClickEndTurn()
    {
        if (localPlayer == null) return;

        localPlayer.RequestEndTurn();
    }
}