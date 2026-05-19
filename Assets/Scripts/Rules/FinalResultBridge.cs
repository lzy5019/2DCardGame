using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FinalPlayerSnapshot
{
    public int playerIndex;
    public string playerName = "";
    public int gainScore;
    public int cardValueScore;
    public int totalScore;
    public List<string> ownedCardIds = new List<string>();
    public List<string> keyCardIds = new List<string>();
}

[System.Serializable]
public class FinalMatchSnapshot
{
    public int playerCount;
    public int initialScorePool;
    public int remainingScorePool;
    public List<FinalPlayerSnapshot> players = new List<FinalPlayerSnapshot>();
}

public class FinalResultBridge : MonoBehaviour
{
    private const string RuntimeObjectName = "[FinalResultBridge]";

    public static FinalResultBridge Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string finalSceneName = "FinalEstimate";

    [Header("Runtime Snapshot Debug")]
    [SerializeField] private FinalMatchSnapshot currentSnapshot = new FinalMatchSnapshot();

    public string FinalSceneName => finalSceneName;
    public FinalMatchSnapshot CurrentSnapshot => currentSnapshot;
    public bool HasSnapshot => currentSnapshot != null && currentSnapshot.players != null && currentSnapshot.players.Count > 0;

    public static FinalResultBridge EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        Instance = runtimeObject.AddComponent<FinalResultBridge>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void StoreSnapshot(FinalMatchSnapshot snapshot)
    {
        currentSnapshot = snapshot ?? new FinalMatchSnapshot();
    }

    public bool TryGetSnapshot(out FinalMatchSnapshot snapshot)
    {
        snapshot = currentSnapshot;
        return snapshot != null;
    }

    public void ClearSnapshot()
    {
        currentSnapshot = new FinalMatchSnapshot();
    }
}
