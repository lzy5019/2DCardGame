using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;

public class FinalSceneController : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private FinalPlayerResultUI playerResultPrefab;
    [SerializeField] private Transform contentRoot;

    [Header("Reveal Timing")]
    [SerializeField] private float firstRevealDelay = 0.35f;
    [SerializeField] private float playerRevealIntervalSeconds = 0.3f;
    [SerializeField] private float keyCardRevealIntervalSeconds = 0.12f;

    [Header("Display")]
    [SerializeField] private string hiddenScoreText = "";
    [SerializeField] private TMP_Text debugMessageText;

    public event Action RevealSequenceCompleted;

    private readonly List<FinalPlayerResultUI> spawnedPlayerResults = new List<FinalPlayerResultUI>();
    private Coroutine revealCoroutine;
    private bool revealSequenceCompleted;

    public bool IsRevealSequenceCompleted => revealSequenceCompleted;

    private void Start()
    {
        BuildFromSnapshot();
    }

    public void BuildFromSnapshot()
    {
        revealSequenceCompleted = false;

        if (!TryGetSnapshot(out FinalMatchSnapshot snapshot))
        {
            ShowDebugMessage("No final snapshot found.");
            return;
        }

        if (playerResultPrefab == null || contentRoot == null)
        {
            ShowDebugMessage("Final scene references are incomplete.");
            return;
        }

        ShowDebugMessage(string.Empty);
        RebuildPlayerPanels(snapshot);

        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
        }

        revealCoroutine = StartCoroutine(PlayRevealSequence(snapshot));
    }

    private bool TryGetSnapshot(out FinalMatchSnapshot snapshot)
    {
        snapshot = null;
        FinalResultBridge bridge = FinalResultBridge.EnsureInstance();
        if (bridge == null)
            return false;

        if (!bridge.TryGetSnapshot(out snapshot))
            return false;

        return snapshot != null && snapshot.players != null && snapshot.players.Count > 0;
    }

    private void RebuildPlayerPanels(FinalMatchSnapshot snapshot)
    {
        ClearSpawnedPanels();

        List<FinalPlayerSnapshot> orderedPlayers = new List<FinalPlayerSnapshot>(snapshot.players);
        orderedPlayers.Sort((left, right) => left.playerIndex.CompareTo(right.playerIndex));

        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            FinalPlayerSnapshot playerSnapshot = orderedPlayers[i];
            FinalPlayerResultUI playerResultUi = Instantiate(playerResultPrefab, contentRoot);
            playerResultUi.name = $"FinalPlayer_{playerSnapshot.playerIndex + 1}";
            playerResultUi.SetPlayerOrder(playerSnapshot.playerIndex + 1);
            playerResultUi.SetPlayerName(playerSnapshot.playerName);
            playerResultUi.SetOwnedCardIds(playerSnapshot.ownedCardIds);
            playerResultUi.HideAllKeyCards();
            playerResultUi.SetGainScoreText(hiddenScoreText);
            playerResultUi.SetCardValueScoreText(hiddenScoreText);
            playerResultUi.SetTotalScoreText(hiddenScoreText);
            spawnedPlayerResults.Add(playerResultUi);
        }
    }

    private void ClearSpawnedPanels()
    {
        for (int i = 0; i < spawnedPlayerResults.Count; i++)
        {
            FinalPlayerResultUI spawnedUi = spawnedPlayerResults[i];
            if (spawnedUi != null)
            {
                Destroy(spawnedUi.gameObject);
            }
        }

        spawnedPlayerResults.Clear();
    }

    private IEnumerator PlayRevealSequence(FinalMatchSnapshot snapshot)
    {
        List<FinalPlayerSnapshot> orderedPlayers = new List<FinalPlayerSnapshot>(snapshot.players);
        orderedPlayers.Sort((left, right) => left.playerIndex.CompareTo(right.playerIndex));

        if (firstRevealDelay > 0f)
        {
            yield return new WaitForSeconds(firstRevealDelay);
        }

        yield return RevealKeyCards(orderedPlayers);
        yield return RevealGainScores(orderedPlayers);
        yield return RevealCardValueScores(orderedPlayers);
        yield return RevealTotalScores(orderedPlayers);
        revealSequenceCompleted = true;
        RevealSequenceCompleted?.Invoke();
        revealCoroutine = null;
    }

    private IEnumerator RevealKeyCards(List<FinalPlayerSnapshot> orderedPlayers)
    {
        for (int i = 0; i < orderedPlayers.Count && i < spawnedPlayerResults.Count; i++)
        {
            FinalPlayerSnapshot playerSnapshot = orderedPlayers[i];
            FinalPlayerResultUI playerResultUi = spawnedPlayerResults[i];

            int cardCount = playerSnapshot.keyCardIds != null ? playerSnapshot.keyCardIds.Count : 0;
            for (int cardIndex = 0; cardIndex < cardCount && cardIndex < 3; cardIndex++)
            {
                playerResultUi.RevealKeyCardAt(cardIndex, playerSnapshot.keyCardIds[cardIndex]);
                if (cardIndex < cardCount - 1 && keyCardRevealIntervalSeconds > 0f)
                {
                    yield return new WaitForSeconds(keyCardRevealIntervalSeconds);
                }
            }

            if (playerRevealIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(playerRevealIntervalSeconds);
            }
        }
    }

    private IEnumerator RevealGainScores(List<FinalPlayerSnapshot> orderedPlayers)
    {
        for (int i = 0; i < orderedPlayers.Count && i < spawnedPlayerResults.Count; i++)
        {
            spawnedPlayerResults[i].SetGainScore(orderedPlayers[i].gainScore);
            if (playerRevealIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(playerRevealIntervalSeconds);
            }
        }
    }

    private IEnumerator RevealCardValueScores(List<FinalPlayerSnapshot> orderedPlayers)
    {
        for (int i = 0; i < orderedPlayers.Count && i < spawnedPlayerResults.Count; i++)
        {
            spawnedPlayerResults[i].SetCardValueScore(orderedPlayers[i].cardValueScore);
            if (playerRevealIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(playerRevealIntervalSeconds);
            }
        }
    }

    private IEnumerator RevealTotalScores(List<FinalPlayerSnapshot> orderedPlayers)
    {
        for (int i = 0; i < orderedPlayers.Count && i < spawnedPlayerResults.Count; i++)
        {
            spawnedPlayerResults[i].SetTotalScore(orderedPlayers[i].totalScore);
            if (playerRevealIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(playerRevealIntervalSeconds);
            }
        }
    }

    private void ShowDebugMessage(string message)
    {
        if (debugMessageText != null)
        {
            debugMessageText.text = message;
        }
    }
}
