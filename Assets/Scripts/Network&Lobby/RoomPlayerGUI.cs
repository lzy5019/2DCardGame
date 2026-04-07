using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomPlayerGUI : MonoBehaviour
{
    [SerializeField] private GameObject playerPanelPrefab;

    #region 界面引用
    private Button readyBtn;
    private Button cancelBtn;
    private Button removeBtn;
    private TextMeshProUGUI playerName;
    private TextMeshProUGUI readyState;
    private GameObject playerlist;
    private GameObject playerPanel;
    private MyNetworkRoomPlayer player;
    #endregion

    #region 生命周期
    private void Start()
    {
        InitializeUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (playerName != null)
        {
            playerName.text = player.roomPlayerName;
        }

        if (readyState != null)
        {
            readyState.text = player.readyToBegin ? "Ready" : "Not Ready";
        }

        if (NetworkClient.active && player.isLocalPlayer)
        {
            if (readyBtn != null && cancelBtn != null)
            {
                readyBtn.gameObject.SetActive(!player.readyToBegin);
                cancelBtn.gameObject.SetActive(player.readyToBegin);
            }
        }
    }

    private void OnDestroy()
    {
        if (readyBtn != null)
            readyBtn.onClick.RemoveAllListeners();

        if (cancelBtn != null)
            cancelBtn.onClick.RemoveAllListeners();

        if (removeBtn != null)
            removeBtn.onClick.RemoveAllListeners();
    }
    #endregion

    #region 场景初始化
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "LobbyScene")
        {
            InitializeUI();
        }
    }

    private void InitializeUI()
    {
        if (playerPanel != null) return;

        player = GetComponent<MyNetworkRoomPlayer>();
        playerlist = GameObject.FindWithTag("PlayerList");
        playerPanel = Instantiate(playerPanelPrefab, playerlist.transform);

        readyBtn = playerPanel.transform.Find("Ready Button").GetComponent<Button>();
        cancelBtn = playerPanel.transform.Find("Cancel Button").GetComponent<Button>();
        removeBtn = playerPanel.transform.Find("Remove Button").GetComponent<Button>();
        playerName = playerPanel.transform.Find("Player Name").GetComponent<TextMeshProUGUI>();
        readyState = playerPanel.transform.Find("Ready State").GetComponent<TextMeshProUGUI>();

        readyBtn.gameObject.SetActive(false);
        cancelBtn.gameObject.SetActive(false);
        removeBtn.gameObject.SetActive(false);

        if (NetworkClient.active && player.isLocalPlayer)
        {
            readyBtn.onClick.AddListener(OnReadyButtonClicked);
            cancelBtn.onClick.AddListener(OnCancelButtonClicked);
        }

        if (player.isServer && !player.isLocalPlayer)
        {
            removeBtn.gameObject.SetActive(true);
            removeBtn.onClick.AddListener(OnRemoveButtonClicked);
        }
    }
    #endregion

    #region 按钮事件
    private void OnReadyButtonClicked()
    {
        readyBtn.gameObject.SetActive(false);
        cancelBtn.gameObject.SetActive(true);
        player.CmdChangeReadyState(true);
    }

    private void OnCancelButtonClicked()
    {
        readyBtn.gameObject.SetActive(true);
        cancelBtn.gameObject.SetActive(false);
        player.CmdChangeReadyState(false);
    }

    private void OnRemoveButtonClicked()
    {
        GetComponent<NetworkIdentity>().connectionToClient.Disconnect();

        if (playerPanel != null)
        {
            Destroy(playerPanel.gameObject);
        }
    }
    #endregion
}

