using System;

[Serializable]
public class PlayerData
{
    // 玩家信息
    public int playerId;
    public string playerName;

    public int mana;    // 费用
    public int score;    // 分数
    public int attack;  // 攻击力
    public int drawNum; // 每个回合结束的抽牌数
    public bool isWizard;

    public PlayerData(int id, string name)
    {
        playerId = id;
        playerName = name;
        mana = 0;
        score = 0;
        attack = 0;
        drawNum = 5;
        isWizard = false;
    }
}