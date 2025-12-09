using CYFramework;
using UnityEngine;

/// <summary>
/// 游戏资源管理（金币、良心值、波次）
/// </summary>
public class GameResourceManager
{
    public int Gold { get; private set; } = 100;
    public int Conscience { get; private set; } = 50;
    public int CurrentWave { get; private set; } = 0;

    public void AddGold(int amount)
    {
        int oldAmount = Gold;
        Gold += amount;

        var evt = new GoldChangedEvent
        {
            OldAmount = oldAmount,
            NewAmount = Gold,
            Delta = amount
        };
        CY.Event.Post(ref evt);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        AddGold(-amount);
        return true;
    }

    public void NextWave()
    {
        CurrentWave++;
    }
}