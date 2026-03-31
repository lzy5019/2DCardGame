using Mirror;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEndTurn : MonoBehaviour
{
    public static PlayerEndTurn Instance;

    public GameObject endTurnButtonObject;
    public Button endTurnButton;

    public PlayerState localPlayer = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnClickEndTurn);
        }

        RefreshButtonVisible();
    }

    private void OnDestroy()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(OnClickEndTurn);
        }
    }

    public void RegisterLocalPlayer(PlayerState playerState)
    {
        localPlayer = playerState;
        RefreshButtonVisible();
    }

    private void Update()
    {
        RefreshButtonVisible();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryEndTurnByHotkey();
        }
    }

    private void RefreshButtonVisible()
    {
        if (endTurnButtonObject == null) return;

        bool shouldShow = false;

        if (localPlayer != null && MatchManager.Instance != null && MatchManager.Instance.gameStarted)
        {
            shouldShow = localPlayer.isMyTurn;
        }

        endTurnButtonObject.SetActive(shouldShow);
    }

    private void TryEndTurnByHotkey()
    {
        if (localPlayer == null) return;
        if (MatchManager.Instance == null) return;
        if (!MatchManager.Instance.gameStarted) return;
        if (!localPlayer.isMyTurn) return;

        OnClickEndTurn();
    }

    private void OnClickEndTurn()
    {
        if (localPlayer == null) return;

        localPlayer.RequestEndTurn();
    }
}