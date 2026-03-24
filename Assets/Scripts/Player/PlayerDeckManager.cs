/// <summary>
/// 管理玩家牌组
/// 抽牌、弃牌、洗牌、打牌、获得卡牌
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDeckManager : MonoBehaviour
{
    public HandDisplayManager handDisplayManager;

    [Header("玩家牌组信息")]
    public List<string> drawPile = new List<string>();      // 抽牌堆
    public List<string> playerPile = new List<string>();    // 全部牌
    public List<string> discardPile = new List<string>();   // 洗牌堆
    public List<string> handCards = new List<string>();     // 手牌
    public List<string> playedCards = new List<string>();   // 本回合打出的牌

    [Header("初始手牌")]
    public List<string> startCards = new List<string>();    // 初始卡牌

    public void Initialized()
    {
        InitializeDeck();
        ShuffleDrawPile();
        Debug.Log("**玩家牌组初始化完成");
    }

    public void InitializeDeck()        // 初始化函数
    {
        drawPile.Clear();
        playerPile.Clear();
        handCards.Clear();
        playedCards.Clear();
        discardPile.Clear();

        for (int i = 0; i < startCards.Count; i++)  // 加入初始牌组
        {
            playerPile.Add(startCards[i]);
            drawPile.Add(startCards[i]);
        }

    }

    public void ShuffleDrawPile()       // 洗牌
    {
        if (discardPile.Count + drawPile.Count == 0)
        {
            Debug.Log("无牌可抽");
            return;
        }
        
        for (int i = 0; i < discardPile.Count; i++)
        {
            drawPile.Add(discardPile[i]);
        }
        discardPile.Clear();

        for (int i = 0; i < drawPile.Count; i++)
        {
            int randomIndex = Random.Range(i, drawPile.Count);

            string temp = drawPile[i];
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = temp;
        }
        Debug.Log("玩家牌堆洗牌完成");
    }

    public void DrawCard()      // 抽牌
    {
        if (drawPile.Count == 0)
        {
            ShuffleDrawPile();
        }
        if (drawPile.Count != 0)
        {
            string cardID = drawPile[0];
            handCards.Add(cardID);
            drawPile.RemoveAt(0);
            handDisplayManager.RearrangeAfterDraw();
        }
    }

    public void PlayCard(string cardId, int handIndex)  // 打牌
    {
        playedCards.Add(handCards[handIndex]);
        handCards.RemoveAt(handIndex);
        handDisplayManager.RearrangeAfterPlay(handIndex);
        Debug.Log("打出了" + cardId);
        CardEffectManager.Instance.ResolveCardEffect(cardId, 0);

    }

    public void GainCard(string cardId)     // 获得卡牌
    {
        playerPile.Add(cardId);
        discardPile.Add(cardId);

        Debug.Log("玩家获得卡牌：" + cardId);
    }

    public void EndShuffle()        // 回合结束
    {
        for (int i = 0; i < playedCards.Count; i++)
        {
            discardPile.Add(playedCards[i]);
        }

        for (int i = 0; i < handCards.Count; i++)
        {
            discardPile.Add(handCards[i]);
        }

        playedCards.Clear();
        handCards.Clear();

        Debug.Log("回合结束：已将打出牌和剩余手牌按顺序放入弃牌堆");
    }
}
