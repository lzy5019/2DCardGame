using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class FinalReturnToRoomButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button returnButton;
    [SerializeField] private FinalSceneController finalSceneController;
    [SerializeField] private GameObject visibilityRoot;
    [SerializeField] private CanvasGroup visibilityCanvasGroup;

    [Header("Behavior")]
    [SerializeField] private bool hideUntilRevealComplete = true;
    [SerializeField] private bool onlyHostCanReturnToRoom = true;

    private void Awake()
    {
        if (returnButton == null)
        {
            returnButton = GetComponent<Button>();
        }

        if (finalSceneController == null)
        {
            finalSceneController = FindFirstObjectByType<FinalSceneController>();
        }

        if (visibilityRoot == null)
        {
            visibilityRoot = gameObject;
        }

        if (visibilityCanvasGroup == null && visibilityRoot != null)
        {
            visibilityCanvasGroup = visibilityRoot.GetComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(HandleReturnClicked);
        }

        if (finalSceneController != null)
        {
            finalSceneController.RevealSequenceCompleted += HandleRevealSequenceCompleted;
        }

        RefreshVisibilityAndInteractable();
    }

    private void OnDisable()
    {
        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(HandleReturnClicked);
        }

        if (finalSceneController != null)
        {
            finalSceneController.RevealSequenceCompleted -= HandleRevealSequenceCompleted;
        }
    }

    private void HandleRevealSequenceCompleted()
    {
        RefreshVisibilityAndInteractable();
    }

    private void RefreshVisibilityAndInteractable()
    {
        bool revealCompleted = finalSceneController == null || finalSceneController.IsRevealSequenceCompleted;
        bool shouldShow = !hideUntilRevealComplete || revealCompleted;

        if (visibilityRoot != null && visibilityRoot != gameObject)
        {
            visibilityRoot.SetActive(shouldShow);
        }

        if (visibilityCanvasGroup != null)
        {
            visibilityCanvasGroup.alpha = shouldShow ? 1f : 0f;
            visibilityCanvasGroup.interactable = shouldShow;
            visibilityCanvasGroup.blocksRaycasts = shouldShow;
        }

        if (returnButton != null)
        {
            bool canReturn = !onlyHostCanReturnToRoom || NetworkServer.active;
            returnButton.interactable = shouldShow && canReturn;
        }
    }

    private void HandleReturnClicked()
    {
        if (onlyHostCanReturnToRoom && !NetworkServer.active)
            return;

        MyNetworkRoomManager roomManager = MyNetworkRoomManager.Instance;
        if (roomManager == null)
            return;

        FinalResultBridge bridge = FinalResultBridge.EnsureInstance();
        if (bridge != null)
        {
            bridge.ClearSnapshot();
        }

        roomManager.ServerChangeScene(roomManager.RoomScene);
    }
}
