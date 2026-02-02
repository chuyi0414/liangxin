// ============================================================================
// CYFramework 2.2 - 玩法核心层抽象接口
// 文档位置：3.2.1 抽象接口 (IGameplayWorld)
// 功能：统一 API，屏蔽 OOP/DOTS 实现差异
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Gameplay.Abstraction
{
    #region 输入系统
    
    /// <summary>
    /// 输入类型
    /// </summary>
    public enum InputType
    {
        /// <summary>
        /// 无输入
        /// </summary>
        None,
        /// <summary>
        /// 移动
        /// </summary>
        Move,
        /// <summary>
        /// 跳跃
        /// </summary>
        Jump,
        /// <summary>
        /// 攻击
        /// </summary>
        Attack,
        /// <summary>
        /// 技能
        /// </summary>
        Skill,
        /// <summary>
        /// 交互
        /// </summary>
        Interact,
        /// <summary>
        /// 自定义
        /// </summary>
        Custom
    }
    
    /// <summary>
    /// 输入指令
    /// </summary>
    public struct InputCommand
    {
        /// <summary>
        /// 输入类型
        /// </summary>
        public InputType Type;
        /// <summary>
        /// 输入方向
        /// </summary>
        public Vector2 Direction;
        /// <summary>
        /// 技能 ID
        /// </summary>
        public int SkillId;
        /// <summary>
        /// 时间戳
        /// </summary>
        public float Timestamp;
        /// <summary>
        /// 自定义 ID
        /// </summary>
        public int CustomId;
    }
    
    #endregion
    
    #region 渲染快照
    
    /// <summary>
    /// 渲染快照
    /// 文档位置：3.3 数据桥接层 - 三缓冲环形队列
    /// </summary>
    public struct RenderSnapshot
    {
        /// <summary>
        /// 有效单位数量
        /// </summary>
        public int Count;
        
        /// <summary>
        /// 单位 ID 数组（预分配）
        /// </summary>
        public int[] IDs;
        
        /// <summary>
        /// 位置数组（预分配）
        /// </summary>
        public Vector3[] Positions;
        
        /// <summary>
        /// 旋转数组（预分配）
        /// </summary>
        public Quaternion[] Rotations;
        
        /// <summary>
        /// 生命值数组（预分配）
        /// </summary>
        public float[] HPs;
        
        /// <summary>
        /// 状态 ID 数组（预分配）
        /// </summary>
        public int[] StateIDs;
        
        /// <summary>
        /// 逻辑帧时间戳
        /// </summary>
        public float Timestamp;
        
        /// <summary>
        /// 预分配快照
        /// </summary>
        public static RenderSnapshot Create(int maxUnits)
        {
            return new RenderSnapshot
            {
                Count = 0,
                IDs = new int[maxUnits],
                Positions = new Vector3[maxUnits],
                Rotations = new Quaternion[maxUnits],
                HPs = new float[maxUnits],
                StateIDs = new int[maxUnits],
                Timestamp = 0
            };
        }
        
        /// <summary>
        /// 清空快照
        /// </summary>
        public void Clear()
        {
            Count = 0;
            Timestamp = 0;
        }
        
        /// <summary>
        /// 复制快照数据
        /// </summary>
        public void CopyFrom(in RenderSnapshot other)
        {
            Count = other.Count;
            Timestamp = other.Timestamp;
            
            Array.Copy(other.IDs, IDs, other.Count);
            Array.Copy(other.Positions, Positions, other.Count);
            Array.Copy(other.Rotations, Rotations, other.Count);
            Array.Copy(other.HPs, HPs, other.Count);
            Array.Copy(other.StateIDs, StateIDs, other.Count);
        }
    }
    
    #endregion
    
    #region 玩法世界接口
    
    /// <summary>
    /// 玩法世界接口
    /// 对外暴露统一 API，对内屏蔽实现差异
    /// </summary>
    public interface IGameplayWorld
    {
        /// <summary>
        /// 固定逻辑帧更新
        /// </summary>
        /// <param name="fixedDt">固定时间步长</param>
        void FixedTick(float fixedDt);
        
        /// <summary>
        /// 处理输入
        /// </summary>
        /// <param name="command">输入指令</param>
        void HandleInput(in InputCommand command);
        
        /// <summary>
        /// 获取渲染快照（当前帧）
        /// </summary>
        ref readonly RenderSnapshot GetRenderSnapshot();
        
        /// <summary>
        /// 获取上一帧快照（用于插值）
        /// </summary>
        ref readonly RenderSnapshot GetPrevSnapshot();
        
        /// <summary>
        /// 重置 DeltaTime（切后台恢复时调用）
        /// </summary>
        void ResetDeltaTime();
        
        /// <summary>
        /// 初始化世界
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// 销毁世界
        /// </summary>
        void Dispose();
    }
    
    /// <summary>
    /// 查询接口
    /// </summary>
    public interface IQuery
    {
        /// <summary>
        /// 根据 ID 获取单位位置
        /// </summary>
        Vector3 GetPosition(int unitId);
        
        /// <summary>
        /// 根据 ID 获取单位生命值
        /// </summary>
        float GetHP(int unitId);
        
        /// <summary>
        /// 检查单位是否存活
        /// </summary>
        bool IsAlive(int unitId);
        
        /// <summary>
        /// 获取指定范围内的单位
        /// </summary>
        int GetUnitsInRange(Vector3 center, float radius, int[] resultBuffer);
    }
    
    /// <summary>
    /// 命令接口
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 生成单位
        /// </summary>
        int SpawnUnit(int configId, Vector3 position, Quaternion rotation);
        
        /// <summary>
        /// 销毁单位
        /// </summary>
        void DestroyUnit(int unitId);
        
        /// <summary>
        /// 移动单位
        /// </summary>
        void MoveUnit(int unitId, Vector3 targetPosition);
        
        /// <summary>
        /// 对单位造成伤害
        /// </summary>
        void DamageUnit(int unitId, float damage);
        
        /// <summary>
        /// 治疗单位
        /// </summary>
        void HealUnit(int unitId, float amount);
    }
    
    #endregion
}
