using TMPro;
using UnityEngine;

public class PlayerResourceUI : MonoBehaviour
{
    [Header("UIÒýÓÃ")]
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text scoreText;
    private PlayerState localPlayer;

    private void Update()
    {
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (localPlayer != null)
        {
            SetData(localPlayer.attack, localPlayer.mana, localPlayer.score);
        }
    }
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
    public void SetData(int attack, int mana, int score)
    {
        if (attackText != null)
            attackText.text = attack.ToString();

        if (manaText != null)
            manaText.text = mana.ToString();

        if (scoreText != null)
            scoreText.text = score.ToString();
    }

}