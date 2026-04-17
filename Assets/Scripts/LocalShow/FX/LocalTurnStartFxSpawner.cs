using Mirror;
using UnityEngine;

public class LocalTurnStartFxSpawner : MonoBehaviour
{
    public static LocalTurnStartFxSpawner Instance { get; private set; }

    [Header("Turn Start FX")]
    [SerializeField] private GameObject startTurnPrefab;
    [SerializeField] private float destroyDelaySeconds = 5f;

    private PlayerState localPlayer;
    private bool wasMyTurn;
    private GameObject activeFxInstance;

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
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterLocalPlayer(PlayerState player)
    {
        localPlayer = player;
        wasMyTurn = player != null && player.isMyTurn;
    }

    private void Update()
    {
        if (localPlayer == null && NetworkClient.localPlayer != null)
        {
            PlayerState fallbackPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
            if (fallbackPlayer != null)
            {
                RegisterLocalPlayer(fallbackPlayer);
            }
        }

        if (localPlayer == null)
            return;

        bool isMyTurnNow = localPlayer.isMyTurn;
        if (!wasMyTurn && isMyTurnNow)
        {
            SpawnStartTurnFx();
        }

        wasMyTurn = isMyTurnNow;
    }

    private void SpawnStartTurnFx()
    {
        if (startTurnPrefab == null)
        {
            Debug.LogWarning("LocalTurnStartFxSpawner is missing the Start Turn prefab reference.");
            return;
        }

        if (activeFxInstance != null)
        {
            Destroy(activeFxInstance);
        }

        activeFxInstance = Instantiate(startTurnPrefab, transform.position, transform.rotation);
        Destroy(activeFxInstance, destroyDelaySeconds);
    }
}
