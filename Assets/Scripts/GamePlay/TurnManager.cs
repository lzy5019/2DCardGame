using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public int currentPlayerId = 0;
    public PlayerDeckManager playerDeckManager;

    public void EndTurn()
    {
        Debug.Log("»ØºÏ½áÊø");

        PlayerDataManager.Instance.players[currentPlayerId].attack = 0;
        PlayerDataManager.Instance.players[currentPlayerId].mana = 0;
        PlayerDataManager.Instance.players[currentPlayerId].isWizard = false;
        PlayerDataManager.Instance.playerDataDisplay.RefreshDisplay();

        playerDeckManager.EndShuffle();
        playerDeckManager.handDisplayManager.ClearHand();
        CardEffect.Instance.DrawCards(5);

    }
}