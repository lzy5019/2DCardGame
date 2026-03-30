/// <summary>
/// 用于实现出牌后的效果
/// </summary>

using UnityEngine;

public class CardEffectManager : MonoBehaviour
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

    public void ResolveCardEffect(int playerindex, string cardId)
    {
        PlayerState player = MatchManager.Instance.playerList[playerindex];
        switch (cardId)
        {
            case "00001":   // 学徒
                player.AddMana(1);break;
            case "00002":   // 民兵
                player.AddAttack(1);break;
            case "00003":   // 战士
                player.AddAttack(2); break;
            default: break;
        }
    }
}
