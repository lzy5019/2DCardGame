/// <summary>
/// 负责手牌UI
/// 包括排列旋转、放大、拖动、判断是否打出卡牌
/// </summary>

using UnityEngine;
using UnityEngine.EventSystems;

public class HandCardUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private RectTransform rect;
    private Canvas parentCanvas;
    private RectTransform parentRect;

    [Header("目标状态")]
    public Vector2 targetPosition;
    public float targetRotation;

    [Header("移动参数")]
    public float moveSpeed = 12f;
    public float rotateSpeed = 12f;
    public float scaleSpeed = 12f;

    [Header("悬停效果")]
    public float hoverHeight = 120f;
    public float hoverScale = 1.2f;

    [Header("拖拽效果")]
    public float dragScale = 1.2f;
    public bool resetRotationWhenDragging = true;
    public float playThresholdY = 300f;

    public bool isHovering = false;     // 鼠标悬停
    public bool isDragging = false;     // 鼠标左键按住
    public HandDisplayManager handDisplayManager;

    public string cardId;
    public int handIndex;

    private Vector2 dragOffset;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }
    public void Initialized()
    {
        parentRect = transform.parent as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
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

        if (IsInPlayArea())
        {
            handDisplayManager.playerDeckManager.PlayCard(cardId,handIndex);
        }
    }

    private void UpdateDragOffset(PointerEventData eventData)   // 保存拖拽偏移量
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
}