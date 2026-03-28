/// <summary>   
/// 存储所有卡牌的效果
/// 卡牌打出函数引用此处
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardEffect : MonoBehaviour
{
    public static CardEffect Instance;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================
    // 基础效果
    // =========================
    public void DrawCards(int amount)
    {

    }

    




}
