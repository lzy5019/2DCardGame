using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PresentationPanelUI : MonoBehaviour
{
    public static PresentationPanelUI Instance;

    #region 界面引用
    [Header("根节点")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private GameObject panelRoot;

    [Header("显示控件")]
    [SerializeField] private Image targetCardImage;
    [SerializeField] private TMP_Text actorText;
    [SerializeField] private TMP_Text descriptionText;
    #endregion

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        AutoBindIfNeeded();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region 播放接口
    // 第一版卡牌播报：显示卡牌与玩家名，可按需显示简短描述，持续指定时长后关闭。
    public IEnumerator PlayCardAnnouncement(Sprite cardSprite, string actorName, string description, float duration)
    {
        AutoBindIfNeeded();

        if (panelRoot == null)
        {
            Debug.LogWarning("PresentationPanelUI: panelRoot is missing.");
            yield return new WaitForSeconds(duration);
            yield break;
        }

        if (canvasRoot != null)
        {
            canvasRoot.SetActive(true);
        }

        panelRoot.SetActive(true);

        if (targetCardImage != null)
        {
            targetCardImage.sprite = cardSprite;
            targetCardImage.enabled = cardSprite != null;
        }

        if (actorText != null)
        {
            actorText.text = actorName;
            actorText.gameObject.SetActive(true);
        }

        if (descriptionText != null)
        {
            bool hasDescription = !string.IsNullOrEmpty(description);
            descriptionText.text = hasDescription ? description : string.Empty;
            descriptionText.gameObject.SetActive(hasDescription);
        }

        yield return new WaitForSeconds(duration);
        HideImmediate();
    }

    public void HideImmediate()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
    #endregion

    #region 自动绑定
    public void AutoBindIfNeeded()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasRoot == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                canvasRoot = canvas.gameObject;
            }
        }

        if (targetCardImage == null)
        {
            Transform imageTransform = FindChildByNames(
                panelRoot.transform,
                "Target Card Image",
                "Image");

            if (imageTransform != null)
            {
                targetCardImage = imageTransform.GetComponent<Image>();
            }
        }

        if (actorText == null)
        {
            Transform actorTransform = FindChildByNames(
                panelRoot.transform,
                "Actor Text",
                "Creator (TMP)");

            if (actorTransform != null)
            {
                actorText = actorTransform.GetComponent<TMP_Text>();
            }
        }

        if (descriptionText == null)
        {
            Transform descriptionTransform = FindChildByNames(
                panelRoot.transform,
                "Description Text",
                "Description (TMP)");

            if (descriptionTransform != null)
            {
                descriptionText = descriptionTransform.GetComponent<TMP_Text>();
            }
        }
    }

    private Transform FindChildByNames(Transform root, params string[] candidateNames)
    {
        if (root == null || candidateNames == null || candidateNames.Length == 0)
            return null;

        for (int i = 0; i < candidateNames.Length; i++)
        {
            Transform result = FindChildRecursive(root, candidateNames[i]);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private Transform FindChildRecursive(Transform current, string targetName)
    {
        if (current == null)
            return null;
        if (current.name == targetName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform result = FindChildRecursive(current.GetChild(i), targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
    #endregion
}
