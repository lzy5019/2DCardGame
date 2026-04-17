using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    #region 标识信息
    [Header("标识信息")]
    public string cardId;
    #endregion

    #region 数值信息
    [Header("数值信息")]
    public int cost;
    public int scoreValue;
    #endregion

    #region 分类信息
    [Header("分类信息")]
    public CardType cardType;
    public CardCategory cardCategory;
    #endregion

    #region 卡组设置
    [Header("卡组设置")]
    public int cardNum;
    #endregion

    #region 表现信息
    [Header("表现信息")]
    public Sprite cardSprite;
    public AudioClip playWav;
    public AudioClip buyWav;

    [TextArea(2, 5)]
    public string description;

    [Header("衍生卡")]
    public List<CardData> derivedCards = new List<CardData>();
    #endregion

    #region 效果信息
    [Header("效果信息")]
    public List<int> effectId;
    #endregion
}

