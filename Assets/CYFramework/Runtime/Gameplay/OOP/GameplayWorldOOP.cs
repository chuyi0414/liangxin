// ============================================================================
// CYFramework - 玩法世界 OOP 实现
// 单线程、全平台可用的轻量实现
// 
// 设计要点：
// - 数据集中存储，方便批量更新
// - 控制 GC 和虚调用，保证单线程下也有良好性能
// - 避免 LINQ、反射、多层虚函数
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;
using CYFramework.Runtime.Gameplay.OOP.Systems;

namespace CYFramework.Runtime.Gameplay.OOP
{
    /// <summary>
    /// 玩法世界 OOP 实现
    /// 全平台可用（包括 WebGL、微信小游戏）
    /// </summary>
    public class GameplayWorldOOP : IGameplayWorld
    {
        // ====================================================================
        // 属性
        // ====================================================================

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 是否正在运行（未暂停）
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 当前实体数量
        /// </summary>
        public int EntityCount => _entities.Count;

        // ====================================================================
        // 私有字段
        // ====================================================================

        // 玩法配置
        private GameplayConfig _config;

        // 当前游戏时间（受 TimeScale 影响）
        private float _gameTime;

        // 时间缩放
        private float _timeScale = 1.0f;

        // 下一个实体 ID
        private int _nextEntityId = 1;

        // 实体列表
        private List<EntityData> _entities;

        // 实体 ID 到索引的映射
        private Dictionary<int, int> _entityIdToIndex;

        // 子系统列表
        private List<IGameSystem> _systems;

        // 战斗结果
        private BattleResult _battleResult;
        private int _killCount;
        private int _deathCount;

        // ====================================================================
        // 生命周期
        // ====================================================================

        /// <summary>
        /// 初始化玩法世界
        /// </summary>
        public void Initialize(GameplayConfig config)
        {
            if (IsInitialized)
            {
                Log.W("GameplayWorld", "玩法世界已经初始化过了");
                return;
            }

            _config = config ?? new GameplayConfig();
            _timeScale = _config.TimeScale;
            _gameTime = 0f;
            _nextEntityId = 1;

            // 初始化实体容器
            int initialCapacity = Mathf.Min(_config.MaxEntityCount, 256);
            _entities = new List<EntityData>(initialCapacity);
            _entityIdToIndex = new Dictionary<int, int>(initialCapacity);

            // 初始化子系统
            _systems = new List<IGameSystem>();
            RegisterSystems();

            // 按优先级排序并初始化
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Initialize(this);
            }

            IsInitialized = true;
            IsRunning = true;

            Log.I("GameplayWorld", "玩法世界初始化完成");
        }

        /// <summary>
        /// 注册子系统
        /// 可以重写此方法来自定义子系统
        /// </summary>
        protected virtual void RegisterSystems()
        {
            // 按优先级顺序：数值越小越先执行
            _systems.Add(new LifeTimeSystem());    // 50: 生命周期
            _systems.Add(new AISystem());          // 80: AI 决策
            _systems.Add(new MovementSystem());    // 100: 移动
            _systems.Add(new BuffSystem());        // 150: Buff 效果
            _systems.Add(new CombatSystem());      // 200: 战斗
            _systems.Add(new CleanupSystem());     // 9999: 清理（最后执行）
        }

        // ====================================================================
        // 子系统访问（供外部调用）
        // ====================================================================

        /// <summary>
        /// 获取子系统
        /// </summary>
        public T GetSystem<T>() where T : class, IGameSystem
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                if (_systems[i] is T system)
                    return system;
            }
            return null;
        }

        /// <summary>
        /// 注册子系统
        /// </summary>
        public void RegisterSystem<T>(T system) where T : class, IGameSystem
        {
            if (system == null) return;
            
            // 检查是否已存在相同类型的系统
            for (int i = 0; i < _systems.Count; i++)
            {
                if (_systems[i] is T)
                {
                    Log.W("GameplayWorld", $"子系统 {typeof(T).Name} 已存在，将被替换");
                    _systems[i] = system;
                    return;
                }
            }
            
            _systems.Add(system);
            
            // 按优先级排序
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            
            // 如果世界已初始化，立即初始化新系统
            if (IsInitialized)
            {
                system.Initialize(this);
            }
        }

        /// <summary>
        /// 帧更新
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsInitialized || !IsRunning) return;

            // 应用时间缩放
            float scaledDeltaTime = deltaTime * _timeScale;
            _gameTime += scaledDeltaTime;

            // 更新所有子系统
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Update(scaledDeltaTime);
            }
        }

        /// <summary>
        /// 关闭玩法世界
        /// </summary>
        public void Shutdown()
        {
            if (!IsInitialized) return;

            Log.I("GameplayWorld", "正在关闭玩法世界...");

            // 关闭所有子系统（逆序）
            if (_systems != null)
            {
                for (int i = _systems.Count - 1; i >= 0; i--)
                {
                    _systems[i].Shutdown();
                }
                _systems.Clear();
            }

            // 清理实体
            _entities?.Clear();
            _entityIdToIndex?.Clear();

            IsRunning = false;
            IsInitialized = false;
            _config = null;

            Log.I("GameplayWorld", "玩法世界已关闭");
        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            if (!IsInitialized) return;
            IsRunning = false;
            Log.I("GameplayWorld", "玩法世界已暂停");
        }

        /// <summary>
        /// 恢复
        /// </summary>
        public void Resume()
        {
            if (!IsInitialized) return;
            IsRunning = true;
            Log.I("GameplayWorld", "玩法世界已恢复");
        }

        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            if (!IsInitialized) return;

            Log.I("GameplayWorld", "正在重置玩法世界...");

            // 清理实体
            _entities.Clear();
            _entityIdToIndex.Clear();
            _nextEntityId = 1;
            _gameTime = 0f;

            // 重置战斗结果
            _battleResult = default;
            _killCount = 0;
            _deathCount = 0;

            // 重置所有子系统
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Reset();
            }

            IsRunning = true;

            Log.I("GameplayWorld", "玩法世界已重置");
        }

        // ====================================================================
        // 时间相关
        // ====================================================================

        /// <summary>
        /// 设置时间缩放
        /// </summary>
        public void SetTimeScale(float scale)
        {
            _timeScale = Mathf.Max(0f, scale);
        }

        /// <summary>
        /// 获取当前游戏时间
        /// </summary>
        public float GetGameTime()
        {
            return _gameTime;
        }

        // ====================================================================
        // 实体管理
        // ====================================================================

        /// <summary>
        /// 生成实体
        /// </summary>
        /// <param name="spawnInfo">生成信息</param>
        /// <returns>实体 ID</returns>
        public int SpawnEntity(EntitySpawnInfo spawnInfo)
        {
            if (_entities.Count >= _config.MaxEntityCount)
            {
                Log.W("GameplayWorld", "实体数量已达上限");
                return -1;
            }

            int id = _nextEntityId++;
            EntityData entity = new EntityData
            {
                Id = id,
                Type = spawnInfo.Type,
                State = EntityState.Active,
                ConfigId = spawnInfo.ConfigId,
                CampId = spawnInfo.CampId,
                Position = spawnInfo.Position,
                Rotation = spawnInfo.Rotation,
                MoveSpeed = 5f, // 默认移动速度，实际应从配置读取
                TargetPosition = spawnInfo.Position,
                IsMoving = false,
                Hp = 100,       // 默认血量，实际应从配置读取
                MaxHp = 100,
                Attack = 10,
                Defense = 5,
                CreateTime = _gameTime,
                LifeTime = 0f,
                MaxLifeTime = spawnInfo.MaxLifeTime
            };

            int index = _entities.Count;
            _entities.Add(entity);
            _entityIdToIndex[id] = index;

            return id;
        }

        /// <summary>
        /// 销毁实体
        /// </summary>
        /// <param name="entityId">实体 ID</param>
        public void DestroyEntity(int entityId)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index))
                return;

            EntityData entity = _entities[index];
            entity.State = EntityState.PendingDestroy;
            _entities[index] = entity;
        }

        /// <summary>
        /// 获取实体数据
        /// </summary>
        /// <param name="entityId">实体 ID</param>
        /// <param name="data">输出的实体数据</param>
        /// <returns>是否找到</returns>
        public bool TryGetEntity(int entityId, out EntityData data)
        {
            if (_entityIdToIndex.TryGetValue(entityId, out int index))
            {
                data = _entities[index];
                return true;
            }
            data = default;
            return false;
        }

        /// <summary>
        /// 获取实体索引
        /// </summary>
        public int GetEntityIndex(int entityId)
        {
            return _entityIdToIndex.TryGetValue(entityId, out int index) ? index : -1;
        }

        /// <summary>
        /// 获取实体快照（用于 UI 显示）
        /// </summary>
        public EntitySnapshot GetEntitySnapshot(int entityId)
        {
            if (TryGetEntity(entityId, out EntityData data))
            {
                return new EntitySnapshot
                {
                    Id = data.Id,
                    Type = data.Type,
                    State = data.State,
                    Position = data.Position,
                    Rotation = data.Rotation,
                    Hp = data.Hp,
                    MaxHp = data.MaxHp,
                    CampId = data.CampId
                };
            }
            return default;
        }

        /// <summary>
        /// 让实体移动到目标位置
        /// </summary>
        public void MoveEntityTo(int entityId, Vector3 targetPosition)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index))
                return;

            EntityData entity = _entities[index];
            entity.TargetPosition = targetPosition;
            entity.IsMoving = true;
            _entities[index] = entity;
        }

        /// <summary>
        /// 对实体造成伤害
        /// </summary>
        public void DamageEntity(int entityId, int damage)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index))
                return;

            EntityData entity = _entities[index];
            if (entity.State != EntityState.Active)
                return;

            entity.Hp = Mathf.Max(0, entity.Hp - damage);
            if (entity.Hp <= 0)
            {
                entity.State = EntityState.Dead;
            }
            _entities[index] = entity;
        }

        /// <summary>
        /// 治疗实体
        /// </summary>
        public void HealEntity(int entityId, int amount)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index))
                return;

            EntityData entity = _entities[index];
            if (entity.State != EntityState.Active)
                return;

            entity.Hp = Mathf.Min(entity.MaxHp, entity.Hp + amount);
            _entities[index] = entity;
        }

        /// <summary>
        /// 判断实体是否存活
        /// </summary>
        public bool IsEntityAlive(int entityId)
        {
            if (!_entityIdToIndex.TryGetValue(entityId, out int index))
                return false;
            return _entities[index].State == EntityState.Active;
        }

        // ====================================================================
        // 内部方法（供子系统调用）
        // ====================================================================

        /// <summary>
        /// 获取所有实体（只读访问）
        /// </summary>
        public List<EntityData> GetAllEntities()
        {
            return _entities;
        }

        /// <summary>
        /// 更新实体数据
        /// </summary>
        public void UpdateEntity(int index, EntityData data)
        {
            if (index >= 0 && index < _entities.Count)
            {
                _entities[index] = data;
            }
        }

        /// <summary>
        /// 清理待销毁的实体
        /// </summary>
        public void CleanupPendingDestroyEntities()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                if (_entities[i].State == EntityState.PendingDestroy || 
                    _entities[i].State == EntityState.Dead)
                {
                    int entityId = _entities[i].Id;
                    
                    // 统计死亡
                    if (_entities[i].State == EntityState.Dead)
                    {
                        if (_entities[i].CampId == 1) // 假设 1 是玩家阵营
                            _deathCount++;
                        else
                            _killCount++;
                    }
                    
                    // 用最后一个元素替换当前元素（避免移动大量数据）
                    int lastIndex = _entities.Count - 1;
                    if (i != lastIndex)
                    {
                        EntityData lastEntity = _entities[lastIndex];
                        _entities[i] = lastEntity;
                        _entityIdToIndex[lastEntity.Id] = i;
                    }
                    
                    _entities.RemoveAt(lastIndex);
                    _entityIdToIndex.Remove(entityId);
                }
            }
        }

        // ====================================================================
        // 输入与命令处理
        // ====================================================================

        /// <summary>
        /// 处理玩家输入
        /// </summary>
        public void HandleInput(PlayerInput input)
        {
            if (!IsInitialized || !IsRunning) return;

            switch (input.Type)
            {
                case InputType.Move:
                    // 移动玩家控制的实体
                    MoveEntityTo(input.PlayerId, input.TargetPosition);
                    break;

                case InputType.Attack:
                    // 攻击目标
                    var combatSystem = GetSystem<Systems.CombatSystem>();
                    combatSystem?.RequestAttack(input.PlayerId, input.TargetEntityId, 0);
                    break;

                case InputType.Skill:
                    // 释放技能
                    var combat = GetSystem<Systems.CombatSystem>();
                    combat?.RequestAttack(input.PlayerId, input.TargetEntityId, input.SkillId);
                    break;

                case InputType.Select:
                    // 选择目标（可以发送事件通知 UI）
                    break;

                case InputType.Cancel:
                    // 取消当前操作
                    break;
            }
        }

        /// <summary>
        /// 执行游戏命令
        /// </summary>
        public void ExecuteCommand(GameplayCommand command)
        {
            if (!IsInitialized) return;

            switch (command.Type)
            {
                case CommandType.SpawnEntity:
                    SpawnEntity(new EntitySpawnInfo
                    {
                        Type = (EntityType)command.IntParam,
                        ConfigId = command.TargetId,
                        Position = command.VectorParam
                    });
                    break;

                case CommandType.DestroyEntity:
                    DestroyEntity(command.EntityId);
                    break;

                case CommandType.DamageEntity:
                    DamageEntity(command.EntityId, command.IntParam);
                    break;

                case CommandType.HealEntity:
                    HealEntity(command.EntityId, command.IntParam);
                    break;

                case CommandType.MoveEntity:
                    MoveEntityTo(command.EntityId, command.VectorParam);
                    break;

                case CommandType.SetTimeScale:
                    SetTimeScale(command.FloatParam);
                    break;

                case CommandType.EndBattle:
                    EndBattle((BattleResultType)command.IntParam);
                    break;
            }
        }

        // ====================================================================
        // 战斗结果
        // ====================================================================

        /// <summary>
        /// 战斗是否已结束
        /// </summary>
        public bool IsBattleEnded => _battleResult.ResultType != BattleResultType.None;

        /// <summary>
        /// 获取战斗结果
        /// </summary>
        public BattleResult GetBattleResult()
        {
            _battleResult.Duration = _gameTime;
            _battleResult.KillCount = _killCount;
            _battleResult.DeathCount = _deathCount;
            return _battleResult;
        }

        /// <summary>
        /// 结束战斗
        /// </summary>
        public void EndBattle(BattleResultType resultType, int score = 0)
        {
            _battleResult.ResultType = resultType;
            _battleResult.Score = score;
            _battleResult.Duration = _gameTime;
            _battleResult.KillCount = _killCount;
            _battleResult.DeathCount = _deathCount;

            IsRunning = false;
            Log.I("GameplayWorld", $"战斗结束: {resultType}");
        }
    }
}
