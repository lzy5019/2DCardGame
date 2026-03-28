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

    public void ResolveCardEffect(string cardId, int id)
    { 
        switch(cardId)
        {

            default: break;
        }
    }
}
