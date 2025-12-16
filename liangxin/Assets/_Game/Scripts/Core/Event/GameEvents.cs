using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 血条样式（用于支持不同单位/不同外观/多条血条）。
/// 注意：使用 enum 避免 string 带来的 GC 与资源路径耦合。
/// </summary>
public enum HPBarStyle : byte
{
    /// <summary>默认敌人血条（保持与历史逻辑兼容：不填时默认为 0）。</summary>
    Enemy = 0,

    /// <summary>大本营血条（BaseCamp）。</summary>
    BaseCamp = 1,

    /// <summary>Boss/精英等（如需扩展可继续追加，不要随意改动已有值）。</summary>
    Boss = 2,
}

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

    /// <summary>血条样式：用于让 HPBarManager 选择不同预制体/偏移等配置。</summary>
    public HPBarStyle BarStyle;

    /// <summary>
    /// 血条槽位：同一个 UnitID 可同时拥有多条血条（例如 BaseCamp 两条血条）。
    /// 0 表示主血条；1/2... 表示第二/第三条。
    /// </summary>
    public byte BarSlot;
}

// 定义单位死亡事件（用于回收血条）
[StructLayout(LayoutKind.Auto)]
public struct UnitDeadEvent
{
    public int UnitID;
}

// 定义部门资源变化事件（用于刷新战斗 HUD 的资源显示）
// 注意：
// 1) 事件必须是 struct，且发布必须使用 CY.Event.Post(ref evt)（零 GC）。
// 2) 本事件携带快照值，避免 UI 侧再次访问服务并产生隐式依赖。
[StructLayout(LayoutKind.Auto)]
public struct DepartmentResourceChangedEvent
{
    public int Gold;
    public int ConscienceResource;
    public int DarkHeart;
    public int CompanyConscience;
    public int CompanyCorruption;
}
