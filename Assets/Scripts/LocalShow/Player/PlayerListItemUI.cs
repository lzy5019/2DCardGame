using TMPro;
using UnityEngine;

public class PlayerListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text handCardNumText;

    private float refreshInterval = 0.2f;
    private PlayerState targetPlayer;
    private float refreshTimer = 0f;

    public void Bind(PlayerState player)
    {
        targetPlayer = player;
        RefreshView();
    }

    private void Update()
    {
        if (targetPlayer == null) return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshInterval)
            return;

        refreshTimer = 0f;
        RefreshView();
    }

    public void RefreshView()
    {
        nameText.text = targetPlayer.playerName;
        manaText.text = targetPlayer.mana.ToString();
        attackText.text = targetPlayer.attack.ToString();
        scoreText.text = targetPlayer.score.ToString();
        handCardNumText.text = targetPlayer.handCount.ToString();
    }
}
