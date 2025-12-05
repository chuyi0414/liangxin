// ============================================================================
// CYFramework - 实体数据定义
// 此文件重导出 Abstraction 中的共用类型，并定义 OOP 特有的类型
// ============================================================================

using UnityEngine;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.OOP
{
    // ========================================================================
    // 以下类型已移至 Abstraction 命名空间，这里使用 using 别名保持兼容
    // 新代码请直接使用：using CYFramework.Runtime.Gameplay.Abstraction;
    // ========================================================================
    
    // EntityType、EntityState、EntityData、EntitySpawnInfo 
    // 现在定义在 Abstraction.IGameplayWorld.cs 中

    /// <summary>
    /// 实体快照（用于 UI 显示等）
    /// OOP 特有类型，不在 Abstraction 中
    /// </summary>
    public struct EntitySnapshot
    {
        public int Id;              // 实体唯一 ID
        public Abstraction.EntityType Type;     // 实体类型（玩家/敌人等）
        public Abstraction.EntityState State;   // 当前状态（Active/Dead 等）
        public Vector3 Position;    // 世界空间位置
        public float Rotation;      // 朝向（Y 轴角度）
        public int Hp;              // 当前血量
        public int MaxHp;           // 最大血量
        public int CampId;          // 阵营 ID
    }
}
