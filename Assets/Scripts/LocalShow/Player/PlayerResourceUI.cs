using TMPro;
using UnityEngine;

public class PlayerResourceUI : MonoBehaviour
{
    #region 界面引用
    [Header("界面引用")]
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text scoreText;

    private PlayerState localPlayer;
    #endregion

    #region 生命周期
    private void Update()
    {
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (localPlayer != null)
        {
            SetData(localPlayer.attack, localPlayer.mana, localPlayer.GetDisplayedScore());
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
    #endregion
}

