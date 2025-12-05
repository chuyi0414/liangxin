// ============================================================================
// CYFramework - 战斗系统
// 处理攻击判定、伤害计算等战斗相关逻辑
// 支持 OOP 和 DOTS 两种玩法世界实现
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.OOP.Systems
{
    /// <summary>
    /// 战斗请求（用于缓存待处理的攻击）
    /// </summary>
    public struct CombatRequest
    {
        public int AttackerId;
        public int TargetId;
        public int SkillId;
        public float RequestTime;
    }

    /// <summary>
    /// 战斗系统
    /// 负责处理攻击判定和伤害计算
    /// </summary>
    public class CombatSystem : IGameSystem
    {
        public int Priority => 200;

        private IGameplayWorld _world;
        
        // 待处理的战斗请求队列
        private List<CombatRequest> _pendingRequests;
        
        // 攻击冷却记录（实体ID -> 下次可攻击时间）
        private Dictionary<int, float> _attackCooldowns;
        
        // 默认攻击冷却时间
        private const float DEFAULT_ATTACK_COOLDOWN = 1.0f;
        
        // 默认攻击范围
        private const float DEFAULT_ATTACK_RANGE = 2.0f;

        public void Initialize(IGameplayWorld world)
        {
            _world = world;
            _pendingRequests = new List<CombatRequest>(32);
            _attackCooldowns = new Dictionary<int, float>(64);
            Log.I("CombatSystem", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            // 处理所有待处理的战斗请求
            ProcessCombatRequests();
        }

        public void Shutdown()
        {
            _pendingRequests?.Clear();
            _attackCooldowns?.Clear();
            _world = null;
            Log.I("CombatSystem", "已关闭");
        }

        public void Reset()
        {
            _pendingRequests?.Clear();
            _attackCooldowns?.Clear();
        }

        // ====================================================================
        // 公共接口
        // ====================================================================

        /// <summary>
        /// 请求攻击
        /// </summary>
        /// <param name="attackerId">攻击者 ID</param>
        /// <param name="targetId">目标 ID</param>
        /// <param name="skillId">技能 ID（0 表示普通攻击）</param>
        public void RequestAttack(int attackerId, int targetId, int skillId = 0)
        {
            // 检查冷却
            float gameTime = _world.GetGameTime();
            if (_attackCooldowns.TryGetValue(attackerId, out float nextAttackTime))
            {
                if (gameTime < nextAttackTime)
                {
                    return; // 还在冷却中
                }
            }

            _pendingRequests.Add(new CombatRequest
            {
                AttackerId = attackerId,
                TargetId = targetId,
                SkillId = skillId,
                RequestTime = gameTime
            });
        }

        /// <summary>
        /// 检查两个实体是否为敌对关系
        /// </summary>
        public bool IsHostile(int entityIdA, int entityIdB)
        {
            if (!_world.TryGetEntity(entityIdA, out EntityData a)) return false;
            if (!_world.TryGetEntity(entityIdB, out EntityData b)) return false;
            
            // 不同阵营视为敌对
            return a.CampId != b.CampId;
        }

        /// <summary>
        /// 检查是否在攻击范围内
        /// </summary>
        public bool IsInAttackRange(int attackerId, int targetId, float range = DEFAULT_ATTACK_RANGE)
        {
            if (!_world.TryGetEntity(attackerId, out EntityData attacker)) return false;
            if (!_world.TryGetEntity(targetId, out EntityData target)) return false;

            float distance = Vector3.Distance(attacker.Position, target.Position);
            return distance <= range;
        }

        // ====================================================================
        // 内部方法
        // ====================================================================

        private void ProcessCombatRequests()
        {
            float gameTime = _world.GetGameTime();

            for (int i = 0; i < _pendingRequests.Count; i++)
            {
                CombatRequest request = _pendingRequests[i];
                ProcessSingleAttack(request, gameTime);
            }

            _pendingRequests.Clear();
        }

        private void ProcessSingleAttack(CombatRequest request, float gameTime)
        {
            // 验证攻击者
            if (!_world.TryGetEntity(request.AttackerId, out EntityData attacker))
                return;
            if (attacker.State != EntityState.Active)
                return;

            // 验证目标
            if (!_world.TryGetEntity(request.TargetId, out EntityData target))
                return;
            if (target.State != EntityState.Active)
                return;

            // 检查是否敌对
            if (!IsHostile(request.AttackerId, request.TargetId))
                return;

            // 检查距离
            if (!IsInAttackRange(request.AttackerId, request.TargetId))
                return;

            // 计算伤害
            int damage = CalculateDamage(attacker, target, request.SkillId, out bool isCritical);

            // 应用伤害
            _world.DamageEntity(request.TargetId, damage);

            // 设置攻击冷却
            _attackCooldowns[request.AttackerId] = gameTime + DEFAULT_ATTACK_COOLDOWN;

            // 发布伤害事件
            var eventModule = CYFrameworkEntry.Instance?.Event;
            if (eventModule != null)
            {
                eventModule.Publish(new DamageEvent
                {
                    AttackerId = request.AttackerId,
                    TargetId = request.TargetId,
                    Damage = damage,
                    IsCritical = isCritical
                });
            }
        }

        /// <summary>
        /// 计算伤害
        /// </summary>
        private int CalculateDamage(EntityData attacker, EntityData target, int skillId, out bool isCritical)
        {
            // 基础伤害 = 攻击力 - 防御力
            int baseDamage = Mathf.Max(1, attacker.Attack - target.Defense);
            
            // 技能加成（简化：技能 ID 作为额外伤害倍率百分比）
            if (skillId > 0)
            {
                float skillMultiplier = 1f + (skillId * 0.1f); // 技能1=110%, 技能2=120%...
                baseDamage = Mathf.RoundToInt(baseDamage * skillMultiplier);
            }
            
            // 暴击判定（10% 暴击率，200% 暴击伤害）
            isCritical = Random.value < 0.1f;
            if (isCritical)
            {
                baseDamage *= 2;
            }
            
            return baseDamage;
        }
    }
}
