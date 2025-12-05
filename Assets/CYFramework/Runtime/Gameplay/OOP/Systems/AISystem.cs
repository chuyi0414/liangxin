// ============================================================================
// CYFramework - AI 系统
// 处理 NPC/敌人的 AI 行为决策
// 支持 OOP 和 DOTS 两种玩法世界实现
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.OOP.Systems
{
    /// <summary>
    /// AI 状态
    /// </summary>
    public enum AIState
    {
        /// <summary>
        /// 空闲
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 巡逻
        /// </summary>
        Patrol = 1,

        /// <summary>
        /// 追击
        /// </summary>
        Chase = 2,

        /// <summary>
        /// 攻击
        /// </summary>
        Attack = 3,

        /// <summary>
        /// 逃跑
        /// </summary>
        Flee = 4,

        /// <summary>
        /// 返回
        /// </summary>
        Return = 5
    }

    /// <summary>
    /// AI 数据
    /// </summary>
    public struct AIData
    {
        public int EntityId;
        public AIState State;
        public int TargetId;
        public Vector3 HomePosition;
        public float DetectRange;
        public float AttackRange;
        public float ChaseRange;
        public float StateTimer;
        public float ThinkInterval;
        public float LastThinkTime;
    }

    /// <summary>
    /// AI 系统
    /// 负责 NPC 和敌人的行为决策
    /// </summary>
    public class AISystem : IGameSystem
    {
        public int Priority => 80; // 在移动系统之前

        private IGameplayWorld _world;
        
        // AI 数据列表
        private List<AIData> _aiEntities;
        
        // 实体 ID 到 AI 索引的映射
        private Dictionary<int, int> _entityToAI;

        // 默认参数
        private const float DEFAULT_DETECT_RANGE = 10f;
        private const float DEFAULT_ATTACK_RANGE = 2f;
        private const float DEFAULT_CHASE_RANGE = 15f;
        private const float DEFAULT_THINK_INTERVAL = 0.2f;

        public void Initialize(IGameplayWorld world)
        {
            _world = world;
            _aiEntities = new List<AIData>(64);
            _entityToAI = new Dictionary<int, int>(64);
            Log.I("AISystem", "初始化完成");
        }

        public void Update(float deltaTime)
        {
            float gameTime = _world.GetGameTime();

            for (int i = 0; i < _aiEntities.Count; i++)
            {
                AIData ai = _aiEntities[i];

                // 检查实体是否还活着
                if (!_world.TryGetEntity(ai.EntityId, out EntityData entity))
                {
                    ai.State = AIState.Idle;
                    _aiEntities[i] = ai;
                    continue;
                }

                if (entity.State != EntityState.Active)
                {
                    continue;
                }

                // 更新状态计时器
                ai.StateTimer += deltaTime;

                // 按间隔进行思考
                if (gameTime - ai.LastThinkTime >= ai.ThinkInterval)
                {
                    ai.LastThinkTime = gameTime;
                    Think(ref ai, entity);
                }

                // 执行当前状态的行为
                Execute(ref ai, entity, deltaTime);

                _aiEntities[i] = ai;
            }
        }

        public void Shutdown()
        {
            _aiEntities?.Clear();
            _entityToAI?.Clear();
            _world = null;
            Log.I("AISystem", "已关闭");
        }

        public void Reset()
        {
            _aiEntities?.Clear();
            _entityToAI?.Clear();
        }

        // ====================================================================
        // 公共接口
        // ====================================================================

        /// <summary>
        /// 为实体添加 AI
        /// </summary>
        public void AddAI(int entityId)
        {
            if (_entityToAI.ContainsKey(entityId))
                return;

            if (!_world.TryGetEntity(entityId, out EntityData entity))
                return;

            AIData ai = new AIData
            {
                EntityId = entityId,
                State = AIState.Idle,
                TargetId = -1,
                HomePosition = entity.Position,
                DetectRange = DEFAULT_DETECT_RANGE,
                AttackRange = DEFAULT_ATTACK_RANGE,
                ChaseRange = DEFAULT_CHASE_RANGE,
                StateTimer = 0f,
                ThinkInterval = DEFAULT_THINK_INTERVAL,
                LastThinkTime = 0f
            };

            int index = _aiEntities.Count;
            _aiEntities.Add(ai);
            _entityToAI[entityId] = index;
        }

        /// <summary>
        /// 移除实体的 AI
        /// </summary>
        public void RemoveAI(int entityId)
        {
            if (!_entityToAI.TryGetValue(entityId, out int index))
                return;

            // 用最后一个替换
            int lastIndex = _aiEntities.Count - 1;
            if (index != lastIndex)
            {
                AIData last = _aiEntities[lastIndex];
                _aiEntities[index] = last;
                _entityToAI[last.EntityId] = index;
            }

            _aiEntities.RemoveAt(lastIndex);
            _entityToAI.Remove(entityId);
        }

        /// <summary>
        /// 设置 AI 参数
        /// </summary>
        public void SetAIParams(int entityId, float detectRange, float attackRange, float chaseRange)
        {
            if (!_entityToAI.TryGetValue(entityId, out int index))
                return;

            AIData ai = _aiEntities[index];
            ai.DetectRange = detectRange;
            ai.AttackRange = attackRange;
            ai.ChaseRange = chaseRange;
            _aiEntities[index] = ai;
        }

        // ====================================================================
        // AI 决策
        // ====================================================================

        private void Think(ref AIData ai, EntityData self)
        {
            switch (ai.State)
            {
                case AIState.Idle:
                case AIState.Patrol:
                    // 寻找目标
                    int target = FindTarget(self, ai.DetectRange);
                    if (target >= 0)
                    {
                        ai.TargetId = target;
                        ai.State = AIState.Chase;
                        ai.StateTimer = 0f;
                    }
                    break;

                case AIState.Chase:
                    // 检查目标是否还有效
                    if (!IsTargetValid(ai.TargetId, self))
                    {
                        ai.TargetId = -1;
                        ai.State = AIState.Return;
                        ai.StateTimer = 0f;
                        break;
                    }

                    // 检查是否超出追击范围
                    float distToHome = Vector3.Distance(self.Position, ai.HomePosition);
                    if (distToHome > ai.ChaseRange)
                    {
                        ai.TargetId = -1;
                        ai.State = AIState.Return;
                        ai.StateTimer = 0f;
                        break;
                    }

                    // 检查是否进入攻击范围
                    if (_world.TryGetEntity(ai.TargetId, out EntityData targetEntity))
                    {
                        float distToTarget = Vector3.Distance(self.Position, targetEntity.Position);
                        if (distToTarget <= ai.AttackRange)
                        {
                            ai.State = AIState.Attack;
                            ai.StateTimer = 0f;
                        }
                    }
                    break;

                case AIState.Attack:
                    // 检查目标是否还有效
                    if (!IsTargetValid(ai.TargetId, self))
                    {
                        ai.TargetId = -1;
                        ai.State = AIState.Idle;
                        ai.StateTimer = 0f;
                        break;
                    }

                    // 检查是否超出攻击范围
                    if (_world.TryGetEntity(ai.TargetId, out EntityData attackTarget))
                    {
                        float dist = Vector3.Distance(self.Position, attackTarget.Position);
                        if (dist > ai.AttackRange * 1.2f) // 稍微加一点容差
                        {
                            ai.State = AIState.Chase;
                            ai.StateTimer = 0f;
                        }
                    }
                    break;

                case AIState.Return:
                    // 检查是否回到原点
                    float distHome = Vector3.Distance(self.Position, ai.HomePosition);
                    if (distHome < 0.5f)
                    {
                        ai.State = AIState.Idle;
                        ai.StateTimer = 0f;
                    }
                    break;
            }
        }

        private void Execute(ref AIData ai, EntityData self, float deltaTime)
        {
            switch (ai.State)
            {
                case AIState.Chase:
                    // 追击目标
                    if (_world.TryGetEntity(ai.TargetId, out EntityData chaseTarget))
                    {
                        _world.MoveEntityTo(self.Id, chaseTarget.Position);
                    }
                    break;

                case AIState.Attack:
                    // 请求攻击（通过战斗系统）
                    // 这里简化处理，直接调用 DamageEntity
                    // 实际应该调用 CombatSystem.RequestAttack
                    if (ai.StateTimer >= 1.0f) // 攻击间隔
                    {
                        ai.StateTimer = 0f;
                        _world.DamageEntity(ai.TargetId, self.Attack);
                    }
                    break;

                case AIState.Return:
                    // 返回原点
                    _world.MoveEntityTo(self.Id, ai.HomePosition);
                    break;
            }
        }

        private int FindTarget(EntityData self, float range)
        {
            var entities = _world.GetAllEntities();
            float minDist = float.MaxValue;
            int bestTarget = -1;

            for (int i = 0; i < entities.Count; i++)
            {
                EntityData other = entities[i];
                
                // 跳过自己和非活跃实体
                if (other.Id == self.Id || other.State != EntityState.Active)
                    continue;

                // 跳过同阵营
                if (other.CampId == self.CampId)
                    continue;

                // 检查距离
                float dist = Vector3.Distance(self.Position, other.Position);
                if (dist <= range && dist < minDist)
                {
                    minDist = dist;
                    bestTarget = other.Id;
                }
            }

            return bestTarget;
        }

        private bool IsTargetValid(int targetId, EntityData self)
        {
            if (targetId < 0) return false;
            if (!_world.TryGetEntity(targetId, out EntityData target)) return false;
            if (target.State != EntityState.Active) return false;
            if (target.CampId == self.CampId) return false;
            return true;
        }
    }
}
