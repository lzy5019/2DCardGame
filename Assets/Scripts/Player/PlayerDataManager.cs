/// <summary>
/// 管理玩家数据，费用攻击分数
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    [Header("所有玩家数据")]
    public List<PlayerData> players = new List<PlayerData>();

    public PlayerDataDisplay playerDataDisplay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void InitializePlayers()
    {
        players.Clear();
    }

    public void AddPlayer(int id, string name)
    {
        if (GetPlayerById(id) != null)
        {
            Debug.LogWarning("玩家ID已存在: " + id);
            return;
        }

        PlayerData newPlayer = new PlayerData(id, name);

        players.Add(newPlayer);
    }

    public PlayerData GetPlayerById(int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerId == id)
                return players[i];
        }

        return null;
    }

    public void AddMana(int id, int amount)
    {
        PlayerData player = GetPlayerById(id);
        if (player == null)
            return;

        player.mana += amount;
        playerDataDisplay.RefreshDisplay();
    }

    public bool SpendMana(int id, int amount)
    {
        PlayerData player = GetPlayerById(id);
        if (player == null)
            return false;

        if (player.mana < amount)
            return false;

        player.mana -= amount;
        return true;
    }

    public void AddScore(int id, int amount)
    {
        PlayerData player = GetPlayerById(id);
        if (player == null)
            return;

        player.score += amount;
        playerDataDisplay.RefreshDisplay();
    }

    public bool SpendScore(int id, int amount)
    {
        PlayerData player = GetPlayerById(id);
        if (player == null)
            return false;

        if (player.score < amount)
            return false;

        player.score -= amount;
        return true;
    }

    public void AddAttack(int id, int amount)
    {
        PlayerData player = GetPlayerById(id);
        if (player == null)
            return;

        player.attack += amount;
        playerDataDisplay.RefreshDisplay();
    }

    public bool SpendAttack(int id, int amount)
    {
        PlayerData player = GetPlayerById(id);
        if (player == null)
            return false;

        if (player.attack < amount)
            return false;

        player.attack -= amount;
        return true;
    }

}