using System.Runtime.InteropServices;
using UnityEngine;

// 定义单位 HP 变化事件
[StructLayout(LayoutKind.Auto)]
public struct UnitHPChangedEvent
{
    public int UnitID; // 实体 ID
    public float CurrentHP;
    public float MaxHP;
    public int Damage; // 本次受到的伤害（用于飘字）
    public bool IsCritical; // 是否暴击
    public Vector3 WorldPosition; // 发生位置（用于飘字定位）
    public bool IsDead; // 是否死亡
}

// 定义单位死亡事件（用于回收血条）
[StructLayout(LayoutKind.Auto)]
public struct UnitDeadEvent
{
    public int UnitID;
}
