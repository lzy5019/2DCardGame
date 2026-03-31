using Mirror;
using UnityEngine;

public class CardEffectManager : NetworkBehaviour
{
    public static CardEffectManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [Server]
    public void ResolveCardEffect(int playerindex, string cardId)
    {
        if (playerindex < 0)
            return;
        PlayerState player = MatchManager.Instance.playerList[playerindex];
        switch (cardId)
        {
            case "00001":   // 学徒
                player.AddMana(1);break;

            case "00002":   // 民兵
                player.AddAttack(1);break;

            case "00003":   // 战士
                player.AddAttack(2); break;

            case "00004":   // 法师
                player.AddMana(2); break;

            case "00005":   // 术士
                if (!player.isWizard)
                {
                    player.isWizard = true;
                    player.DrawCards(1);
                }
                break;

            case "01002":   // 陈计神
                player.AddAttack(6);break;

            case "01003":   // 答卷活页纸
                player.DrawCards(1);
                break;






            default: break;
        }
    }
}
