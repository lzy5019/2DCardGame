using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum PublicActionType
{
    PlayCard,
    DefeatCenterMonster,
    BuyCenterCard,
    DefeatBaseMonster,
    BuyBaseCard,
    EquipCard,
    EquipWeapon,
    UseEquipment,
    UseWeapon
}

public struct PublicActionEvent
{
    public int actorPlayerIndex;
    public string cardId;
    public PublicActionType actionType;

    public PublicActionEvent(int actorPlayerIndex, string cardId, PublicActionType actionType)
    {
        this.actorPlayerIndex = actorPlayerIndex;
        this.cardId = cardId;
        this.actionType = actionType;
    }
}

public class PublicActionQueueUI : MonoBehaviour
{
    public static PublicActionQueueUI Instance;

    #region 队列状态
    private readonly Queue<PublicActionEvent> actionQueue = new Queue<PublicActionEvent>();
    private bool isPlaying = false;
    private Coroutine waitForIdleCoroutine;

    public bool IsBusy
    {
        get { return isPlaying || actionQueue.Count > 0; }
    }
    #endregion

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    #endregion

    #region 队列播放
    public void Enqueue(PublicActionEvent actionEvent)
    {
        actionQueue.Enqueue(actionEvent);

        if (!isPlaying)
        {
            StartCoroutine(PlayQueueRoutine());
        }
    }

    private IEnumerator PlayQueueRoutine()
    {
        isPlaying = true;

        while (actionQueue.Count > 0)
        {
            PublicActionEvent actionEvent = actionQueue.Dequeue();

            Debug.Log(
                $"Play public action: playerIndex={actionEvent.actorPlayerIndex}, " +
                $"card={actionEvent.cardId}, action={actionEvent.actionType}"
            );

            yield return new WaitForSeconds(2f);
        }

        isPlaying = false;
    }
    #endregion

    #region 队列清空确认
    public void WaitUntilIdleThenAck(int waitId)
    {
        if (waitForIdleCoroutine != null)
        {
            StopCoroutine(waitForIdleCoroutine);
        }

        waitForIdleCoroutine = StartCoroutine(WaitUntilIdleThenAckRoutine(waitId));
    }

    private IEnumerator WaitUntilIdleThenAckRoutine(int waitId)
    {
        while (IsBusy)
        {
            yield return null;
        }

        waitForIdleCoroutine = null;

        if (NetworkClient.localPlayer == null)
            yield break;

        PlayerState localPlayer = NetworkClient.localPlayer.GetComponent<PlayerState>();
        if (localPlayer == null)
            yield break;

        localPlayer.RequestReportPublicActionQueueDrained(waitId);
    }
    #endregion
}



