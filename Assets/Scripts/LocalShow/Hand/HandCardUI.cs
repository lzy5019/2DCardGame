using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 控制单张手牌的悬停、拖拽以及与出牌区域的交互。
/// </summary>
public class HandCardUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    #region 缓存引用
    private RectTransform rect;
    private Canvas parentCanvas;
    private RectTransform parentRect;
    #endregion

    #region 目标状态
    [Header("目标状态")]
    public Vector2 targetPosition;
    public float targetRotation;
    #endregion

    #region 动画设置
    [Header("动画设置")]
    public float moveSpeed = 12f;
    public float rotateSpeed = 12f;
    public float scaleSpeed = 12f;
    #endregion

    #region 悬停状态
    [Header("悬停设置")]
    public float hoverHeight = 180f;
    public float hoverScale = 1.2f;
    #endregion

    #region 拖拽状态
    [Header("拖拽设置")]
    public float dragScale = 1.2f;
    public bool resetRotationWhenDragging = true;
    public float playThresholdY = 300f;
    #endregion

    #region 运行时状态
    public bool isHovering = false;
    public bool isDragging = false;
    public HandDisplayManager handDisplayManager;
    public string cardId;
    public int handIndex;

    private Vector2 dragOffset;
    #endregion

    #region 生命周期
    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (isDragging)
        {
            Vector3 draggingScale = Vector3.one * dragScale;
            rect.localScale = Vector3.Lerp(
                rect.localScale,
                draggingScale,
                Time.deltaTime * scaleSpeed
            );

            if (resetRotationWhenDragging)
            {
                rect.localRotation = Quaternion.Lerp(
                    rect.localRotation,
                    Quaternion.Euler(0, 0, 0f),
                    Time.deltaTime * rotateSpeed
                );
            }

            return;
        }

        Vector2 finalPosition = targetPosition;
        float finalRotation = targetRotation;
        Vector3 finalScale = Vector3.one;

        if (isHovering)
        {
            finalPosition += new Vector2(0, hoverHeight);
            finalRotation = 0f;
            finalScale = Vector3.one * hoverScale;
        }

        rect.anchoredPosition = Vector2.Lerp(
            rect.anchoredPosition,
            finalPosition,
            Time.deltaTime * moveSpeed
        );

        rect.localRotation = Quaternion.Lerp(
            rect.localRotation,
            Quaternion.Euler(0, 0, finalRotation),
            Time.deltaTime * rotateSpeed
        );

        rect.localScale = Vector3.Lerp(
            rect.localScale,
            finalScale,
            Time.deltaTime * scaleSpeed
        );
    }
    #endregion

    #region 初始化
    public void Initialized()
    {
        parentRect = transform.parent as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
    }
    #endregion

    #region 指针事件
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging)
            return;

        isHovering = true;
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging)
            return;

        isHovering = false;
        handDisplayManager.UpdateHandLayout();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        isDragging = true;
        isHovering = false;
        transform.SetAsLastSibling();

        UpdateDragOffset(eventData);
        handDisplayManager.UpdateHandLayout();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        transform.SetAsLastSibling();
        Vector2 localPoint;
        Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? eventData.pressEventCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventCamera,
            out localPoint))
        {
            rect.anchoredPosition = localPoint + dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        if (handDisplayManager == null || handDisplayManager.playerState == null)
            return;

        if (IsInPlayArea())
        {
            int playFxRequestId = handDisplayManager.BeginLocalPlayCardFx(cardId, handIndex, eventData.position);
            handDisplayManager.playerState.RequestPlayCard(handIndex, playFxRequestId);
        }
    }
    #endregion

    #region 辅助方法
    private void UpdateDragOffset(PointerEventData eventData)
    {
        Vector2 localPoint;
        Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? eventData.pressEventCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventCamera,
            out localPoint))
        {
            dragOffset = rect.anchoredPosition - localPoint;
        }
        else
        {
            dragOffset = Vector2.zero;
        }
    }

    public bool IsInPlayArea()
    {
        return rect.anchoredPosition.y >= playThresholdY;
    }
    #endregion
}

