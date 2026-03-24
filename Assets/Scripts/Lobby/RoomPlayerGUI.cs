using Mirror;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomPlayerGUI : MonoBehaviour
{
    [SerializeField] GameObject playerPanelPrefab;

    Button readyBtn;
    Button cancelBtn;
    Button removeBtn;

    TextMeshProUGUI playerName;
    TextMeshProUGUI readyState;

    GameObject playerlist;
    GameObject playerPanel;
    NetworkRoomPlayer player;

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.name == "LobbyScene")
        {
            InitializeUI();
        }
    }

    private void Update()
    {
        if (playerName != null)
        {
            playerName.text = $"Player [{player.index + 1}]";
        }
        if (readyState != null)
        {
            readyState.text = player.readyToBegin ? "准备" : "未准备";
        }
        if(NetworkClient.active && player.isLocalPlayer)
        {
            if(readyBtn != null && cancelBtn != null)
            {
                readyBtn.gameObject.SetActive(!player.readyToBegin);
                cancelBtn.gameObject.SetActive(player.readyToBegin);
            }
        }
    }

    private void InitializeUI()
    {
        player = GetComponent<NetworkRoomPlayer>();
        playerlist = GameObject.FindWithTag("PlayerList");
        playerPanel = Instantiate(playerPanelPrefab, playerlist.transform) as GameObject;
        readyBtn = playerPanel.transform.Find("Ready Button").GetComponent<Button>();
        cancelBtn = playerPanel.transform.Find("Cancel Button").GetComponent<Button>();
        removeBtn = playerPanel.transform.Find("Remove Button").GetComponent<Button>();
        playerName = playerPanel.transform.Find("Player Name").GetComponent<TextMeshProUGUI>();
        readyState = playerPanel.transform.Find("Ready State").GetComponent<TextMeshProUGUI>();

        readyBtn.gameObject.SetActive(false);
        cancelBtn.gameObject.SetActive(false);
        removeBtn.gameObject.SetActive(false);

        if(NetworkClient.active && player.isLocalPlayer)
        {
            readyBtn.onClick.AddListener(OnReadyButtonClicked);
            cancelBtn.onClick.AddListener(OnCancelButtonClicked);
        }

        if(player.isServer && !player.isLocalPlayer)
        {
            removeBtn.gameObject.SetActive(true);
            removeBtn.onClick.AddListener(OnRemoveButtonClicked);
        }
    }

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
        if(playerPanel != null)
        {
            Destroy(playerPanel.gameObject);
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
}
