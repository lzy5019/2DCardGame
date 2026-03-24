/// <summary>
/// 用于实现出牌后的效果
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
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

    public void ResolveCardEffect(string cardId, int id)
    { 
        switch(cardId)
        {
            case "00000":// 平民
                break;

            case "00001":// 学徒
                PlayerDataManager.Instance.AddMana(id, 1);
                break;

            case "00002":// 民兵
                PlayerDataManager.Instance.AddAttack(id, 1);
                break;

            case "00003":// 战士
                PlayerDataManager.Instance.AddAttack(id, 2);
                break;

            case "00004":// 法师
                PlayerDataManager.Instance.AddMana(id, 2);
                break;

            case "00005":// 术士
                //放逐中场一张牌
                if (!PlayerDataManager.Instance.players[id].isWizard)
                {
                    CardEffect.Instance.DrawCards(1);
                    PlayerDataManager.Instance.players[id].isWizard = true;
                }
                break;

            case "00007":// 野怪
                PlayerDataManager.Instance.AddScore(id, 1);
                break;

            default: break;
        }
    }
}
