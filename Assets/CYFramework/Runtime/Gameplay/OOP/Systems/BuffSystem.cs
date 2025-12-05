// ============================================================================
// CYFramework - Buff 系统
// 处理所有 Buff/状态效果的添加、更新、移除
// 支持 OOP 和 DOTS 两种玩法世界实现
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.OOP.Systems
{
    /// <summary>
    /// Buff 系统
    /// 管理所有实体的 Buff 效果
    /// 通过 IGameplayWorld 接口访问实体，同时支持 OOP 和 DOTS
    /// </summary>
    public class BuffSystem : IGameSystem
    {
        public int Priority => 150; // 在战斗系统之前更新

        private IGameplayWorld _world;
        
        // 所有活跃的 Buff
        private List<BuffData> _buffs;
        
        // 实体 ID 到其 Buff 索引列表的映射
        private Dictionary<int, List<int>> _entityBuffs;
        
        // 下一个 Buff ID
        private int _nextBuffId = 1;

        public void Initialize(IGameplayWorld world)
        {
            _world = world;
            _buffs = new List<BuffData>(128);
            _entityBuffs = new Dictionary<int, List<int>>(64);
            Log.I("BuffSystem", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            float gameTime = _world.GetGameTime();

            // 更新所有 Buff
            for (int i = 0; i < _buffs.Count; i++)
            {
                BuffData buff = _buffs[i];
                if (buff.IsExpired) continue;

                // 更新时间
                buff.ElapsedTime += deltaTime;

                // 检查是否过期
                if (buff.Duration > 0 && buff.ElapsedTime >= buff.Duration)
                {
                    buff.IsExpired = true;
                    OnBuffExpired(buff);
                }
                else
                {
                    // 处理周期性效果
                    ProcessTickEffect(ref buff, gameTime);
                }

                _buffs[i] = buff;
            }

            // 清理过期的 Buff
            CleanupExpiredBuffs();
        }

        public void Shutdown()
        {
            _buffs?.Clear();
            _entityBuffs?.Clear();
            _world = null;
            Log.I("BuffSystem", "已关闭");
        }

        public void Reset()
        {
            _buffs?.Clear();
            _entityBuffs?.Clear();
            _nextBuffId = 1;
        }

        // ====================================================================
        // 公共接口
        // ====================================================================

        /// <summary>
        /// 添加 Buff
        /// </summary>
        /// <returns>Buff ID，失败返回 -1</returns>
        public int AddBuff(AddBuffParams param)
        {
            // 验证目标
            if (!_world.TryGetEntity(param.TargetId, out EntityData target))
                return -1;
            if (target.State != EntityState.Active)
                return -1;

            // 检查是否已有相同 Buff（叠层或刷新）
            int existingIndex = FindBuff(param.TargetId, param.ConfigId);
            if (existingIndex >= 0)
            {
                BuffData existing = _buffs[existingIndex];
                if (existing.StackCount < existing.MaxStackCount)
                {
                    // 叠层
                    existing.StackCount++;
                    existing.ElapsedTime = 0f; // 刷新时间
                    _buffs[existingIndex] = existing;
                    return existing.Id;
                }
                else
                {
                    // 刷新持续时间
                    existing.ElapsedTime = 0f;
                    _buffs[existingIndex] = existing;
                    return existing.Id;
                }
            }

            // 创建新 Buff
            int id = _nextBuffId++;
            BuffData buff = new BuffData
            {
                Id = id,
                ConfigId = param.ConfigId,
                OwnerId = param.TargetId,
                CasterId = param.CasterId,
                Type = param.Type,
                EffectType = param.EffectType,
                EffectValue = param.EffectValue,
                ElapsedTime = 0f,
                Duration = param.Duration,
                TickInterval = param.TickInterval,
                LastTickTime = 0f,
                StackCount = 1,
                MaxStackCount = Mathf.Max(1, param.MaxStackCount),
                IsExpired = false
            };

            int buffIndex = _buffs.Count;
            _buffs.Add(buff);

            // 记录实体的 Buff
            if (!_entityBuffs.TryGetValue(param.TargetId, out List<int> buffList))
            {
                buffList = new List<int>(8);
                _entityBuffs[param.TargetId] = buffList;
            }
            buffList.Add(buffIndex);

            // 应用即时效果
            ApplyBuffEffect(buff, true);

            return id;
        }

        /// <summary>
        /// 移除 Buff
        /// </summary>
        public void RemoveBuff(int buffId)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Id == buffId && !_buffs[i].IsExpired)
                {
                    BuffData buff = _buffs[i];
                    buff.IsExpired = true;
                    _buffs[i] = buff;
                    OnBuffExpired(buff);
                    break;
                }
            }
        }

        /// <summary>
        /// 移除实体的所有 Buff
        /// </summary>
        public void RemoveAllBuffs(int entityId)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].OwnerId == entityId && !_buffs[i].IsExpired)
                {
                    BuffData buff = _buffs[i];
                    buff.IsExpired = true;
                    _buffs[i] = buff;
                    OnBuffExpired(buff);
                }
            }
        }

        /// <summary>
        /// 检查实体是否有指定类型的 Buff
        /// </summary>
        public bool HasBuff(int entityId, BuffEffectType effectType)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].OwnerId == entityId && 
                    _buffs[i].EffectType == effectType && 
                    !_buffs[i].IsExpired)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查实体是否被控制（眩晕等）
        /// </summary>
        public bool IsControlled(int entityId)
        {
            return HasBuff(entityId, BuffEffectType.Stun);
        }

        /// <summary>
        /// 获取实体的属性修正值
        /// </summary>
        public int GetAttributeModifier(int entityId, BuffEffectType effectType)
        {
            int total = 0;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].OwnerId == entityId && 
                    _buffs[i].EffectType == effectType && 
                    !_buffs[i].IsExpired)
                {
                    total += _buffs[i].EffectValue * _buffs[i].StackCount;
                }
            }
            return total;
        }

        // ====================================================================
        // 内部方法
        // ====================================================================

        private int FindBuff(int ownerId, int configId)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].OwnerId == ownerId && 
                    _buffs[i].ConfigId == configId && 
                    !_buffs[i].IsExpired)
                {
                    return i;
                }
            }
            return -1;
        }

        private void ProcessTickEffect(ref BuffData buff, float gameTime)
        {
            if (buff.TickInterval <= 0) return;
            if (buff.ElapsedTime - buff.LastTickTime < buff.TickInterval) return;

            buff.LastTickTime = buff.ElapsedTime;

            switch (buff.EffectType)
            {
                case BuffEffectType.HealOverTime:
                    // 回血
                    if (_world.TryGetEntity(buff.OwnerId, out EntityData entity))
                    {
                        int healAmount = buff.EffectValue * buff.StackCount;
                        entity.Hp = Mathf.Min(entity.MaxHp, entity.Hp + healAmount);
                        _world.UpdateEntity(_world.GetEntityIndex(buff.OwnerId), entity);
                    }
                    break;

                case BuffEffectType.DamageOverTime:
                    // 持续伤害
                    _world.DamageEntity(buff.OwnerId, buff.EffectValue * buff.StackCount);
                    break;
            }
        }

        private void ApplyBuffEffect(BuffData buff, bool isAdd)
        {
            // 某些效果需要在添加/移除时立即生效
            // 目前属性修改类效果在查询时动态计算，无需特殊处理
        }

        private void OnBuffExpired(BuffData buff)
        {
            ApplyBuffEffect(buff, false);
        }

        private void CleanupExpiredBuffs()
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].IsExpired)
                {
                    int ownerId = _buffs[i].OwnerId;
                    
                    // 从实体的 Buff 列表中移除
                    if (_entityBuffs.TryGetValue(ownerId, out List<int> buffList))
                    {
                        buffList.Remove(i);
                    }

                    // 用最后一个替换
                    int lastIndex = _buffs.Count - 1;
                    if (i != lastIndex)
                    {
                        _buffs[i] = _buffs[lastIndex];
                        // 更新索引映射
                        int movedOwnerId = _buffs[i].OwnerId;
                        if (_entityBuffs.TryGetValue(movedOwnerId, out List<int> movedList))
                        {
                            int idx = movedList.IndexOf(lastIndex);
                            if (idx >= 0) movedList[idx] = i;
                        }
                    }
                    _buffs.RemoveAt(lastIndex);
                }
            }
        }
    }
}
