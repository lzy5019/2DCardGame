using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;

public class PileCountUI : MonoBehaviour
{
    public static PileCountUI Instance;

    [Header("Pile Count Text")]
    public TMP_Text discardPileText;
    public TMP_Text drawPileText;
    public TMP_Text playedPileText;

    public PlayerState localPlayer;
    private Coroutine delayedRefreshCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        UnbindCallbacks();

        if (delayedRefreshCoroutine != null)
        {
            StopCoroutine(delayedRefreshCoroutine);
            delayedRefreshCoroutine = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterLocalPlayer(PlayerState player)
    {
        UnbindCallbacks();

        localPlayer = player;

        BindCallbacks();
        RefreshCounts();
    }

    private void BindCallbacks()
    {
        if (localPlayer == null) return;

        localPlayer.drawPile.Callback += OnPileListChanged;
        localPlayer.discardPile.Callback += OnPileListChanged;
        localPlayer.playedCardIds.Callback += OnPileListChanged;
    }

    private void UnbindCallbacks()
    {
        if (localPlayer == null) return;

        localPlayer.drawPile.Callback -= OnPileListChanged;
        localPlayer.discardPile.Callback -= OnPileListChanged;
        localPlayer.playedCardIds.Callback -= OnPileListChanged;
    }

    private void OnPileListChanged(SyncList<string>.Operation op, int itemIndex, string oldItem, string newItem)
    {
        if (op == SyncList<string>.Operation.OP_CLEAR)
        {
            QueueRefreshNextFrame();
            return;
        }

        RefreshCounts();
    }

    private void QueueRefreshNextFrame()
    {
        if (delayedRefreshCoroutine != null)
        {
            StopCoroutine(delayedRefreshCoroutine);
        }

        delayedRefreshCoroutine = StartCoroutine(RefreshCountsNextFrame());
    }

    private IEnumerator RefreshCountsNextFrame()
    {
        yield return null;
        delayedRefreshCoroutine = null;
        RefreshCounts();
    }

    public void RefreshCounts()
    {
        int discardCount = 0;
        int drawCount = 0;
        int playedCount = 0;

        if (localPlayer != null)
        {
            discardCount = localPlayer.discardPile.Count;
            drawCount = localPlayer.drawPile.Count;
            playedCount = localPlayer.playedCardIds.Count;
        }

        if (discardPileText != null)
        {
            discardPileText.text = discardCount.ToString();
        }

        if (drawPileText != null)
        {
            drawPileText.text = drawCount.ToString();
        }

        if (playedPileText != null)
        {
            playedPileText.text = playedCount.ToString();
        }
    }
}
