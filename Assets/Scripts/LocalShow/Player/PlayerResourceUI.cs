using TMPro;
using UnityEngine;

public class PlayerResourceUI : MonoBehaviour
{
    public static PlayerResourceUI Instance { get; private set; }

    #region 界面引用
    [Header("界面引用")]
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text scoreText;

    private PlayerState localPlayer;
    #endregion

    #region 生命周期
    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (localPlayer != null)
        {
            SetData(localPlayer.GetDisplayedAttack(), localPlayer.GetDisplayedMana(), localPlayer.GetDisplayedScore());
        }
    }
    #endregion

    #region 玩家查找
    private void FindLocalPlayer()
    {
        PlayerState[] players = FindObjectsOfType<PlayerState>();

        foreach (PlayerState player in players)
        {
            if (player.isLocalPlayer)
            {
                localPlayer = player;
                break;
            }
        }
    }
    #endregion

    #region 渲染
    public void SetData(int attack, int mana, int score)
    {
        if (attackText != null)
            attackText.text = attack.ToString();

        if (manaText != null)
            manaText.text = mana.ToString();

        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    public RectTransform GetAttackTargetRect()
    {
        return attackText != null ? attackText.rectTransform : null;
    }

    public RectTransform GetManaTargetRect()
    {
        return manaText != null ? manaText.rectTransform : null;
    }

    public RectTransform GetScoreTargetRect()
    {
        return scoreText != null ? scoreText.rectTransform : null;
    }
    #endregion
}

