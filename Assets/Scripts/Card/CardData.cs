using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("基础信息")]
    public string cardId;

    [Header("数值信息")]
    public int cost;
    public int scoreValue;

    [Header("分类信息")]
    public CardType cardType;
    public CardCategory cardCategory;

    [Header("卡牌数量")]
    public int cardNum;

    [Header("显示信息")]
    public Sprite cardSprite;

    [Header("扩展预留")]
    [TextArea(2, 5)]
    public string description;

    public string effectId;
}