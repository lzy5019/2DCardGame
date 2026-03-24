using TMPro;
using UnityEngine;

public class PlayerDataDisplay : MonoBehaviour
{
    [Header("显示哪个玩家")]
    public int playerId = 0;

    [Header("UI引用")]
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text scoreText;

    public void RefreshDisplay()
    {
        PlayerData player = PlayerDataManager.Instance.GetPlayerById(playerId);

        if (player == null)
        {
            Debug.LogWarning("没有找到玩家数据，playerId = " + playerId);
            return;
        }

        attackText.text = player.attack.ToString();
        manaText.text = player.mana.ToString();
        scoreText.text = player.score.ToString();
    }

}