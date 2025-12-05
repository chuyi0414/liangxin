// ============================================================================
// CYFramework - 玩法世界 DOTS 实现
// 仅在 PC/原生平台启用，使用 DOTS/ECS 高性能实现
// ============================================================================

#if !UNITY_WEBGL && !WECHAT_MINIGAME
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using CYFramework.Runtime.Core;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Gameplay.DOTS
{
    // ========================================================================
    // ECS 组件定义
    // ========================================================================

    /// <summary>
    /// 实体核心数据组件
    /// </summary>
    public struct EntityDataComponent : IComponentData
    {
        public int Id;
        public int ConfigId;
        public int CampId;
        public int EntityType;  // 0=Unknown, 1=Player, 2=Enemy, 3=Npc, 4=Projectile
        public int State;       // 0=Invalid, 1=Active, 2=Dead, 3=PendingDestroy
    }

    /// <summary>
    /// 位置组件
    /// </summary>
    public struct PositionComponent : IComponentData
    {
        public float3 Position;
        public float Rotation;
    }

    /// <summary>
    /// 移动组件
    /// </summary>
    public struct MovementComponent : IComponentData
    {
        public float3 TargetPosition;
        public float MoveSpeed;
        public bool IsMoving;
    }

    /// <summary>
    /// 战斗属性组件
    /// </summary>
    public struct CombatStatsComponent : IComponentData
    {
        public int Hp;
        public int MaxHp;
        public int Attack;
        public int Defense;
    }

    /// <summary>
    /// 生命周期组件
    /// </summary>
    public struct LifeTimeComponent : IComponentData
    {
        public float LifeTime;
        public float MaxLifeTime;
        public float CreateTime;
    }

    // ========================================================================
    // ECS 系统定义
    // ========================================================================

    /// <summary>
    /// 移动系统
    /// </summary>
    [BurstCompile]
    public partial struct DotsMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (pos, movement, entityData) in 
                SystemAPI.Query<RefRW<PositionComponent>, RefRW<MovementComponent>, RefRO<EntityDataComponent>>())
            {
                if (entityData.ValueRO.State != 1 || !movement.ValueRO.IsMoving)
                    continue;

                float3 direction = math.normalizesafe(movement.ValueRO.TargetPosition - pos.ValueRO.Position);
                float distance = math.distance(pos.ValueRO.Position, movement.ValueRO.TargetPosition);
                float moveDistance = movement.ValueRO.MoveSpeed * deltaTime;

                if (moveDistance >= distance)
                {
                    pos.ValueRW.Position = movement.ValueRO.TargetPosition;
                    movement.ValueRW.IsMoving = false;
                }
                else
                {
                    pos.ValueRW.Position += direction * moveDistance;
                    if (math.lengthsq(direction) > 0.001f)
                    {
                        pos.ValueRW.Rotation = math.atan2(direction.x, direction.z);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 生命周期系统
    /// </summary>
    [BurstCompile]
    public partial struct DotsLifeTimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (lifeTime, entityData) in 
                SystemAPI.Query<RefRW<LifeTimeComponent>, RefRW<EntityDataComponent>>())
            {
                if (entityData.ValueRO.State != 1 || lifeTime.ValueRO.MaxLifeTime <= 0)
                    continue;

                lifeTime.ValueRW.LifeTime += deltaTime;

                if (lifeTime.ValueRO.LifeTime >= lifeTime.ValueRO.MaxLifeTime)
                {
                    entityData.ValueRW.State = 3; // PendingDestroy
                }
            }
        }
    }

    // ========================================================================
    // 玩法世界 DOTS 实现
    // ========================================================================

    /// <summary>
    /// 玩法世界 DOTS 实现
    /// </summary>
    public class GameplayWorldDots : IGameplayWorld
    {
        public bool IsInitialized { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsBattleEnded => _battleResult.ResultType != BattleResultType.None;

        private World _world;
        private EntityManager _entityManager;
        private GameplayConfig _config;
        private float _gameTime;
        private int _nextEntityId = 1;
        private BattleResult _battleResult;
        private int _killCount;
        private int _deathCount;

        // Entity ID 到 ECS Entity 的映射
        private Dictionary<int, Entity> _entityMap;

        public void Initialize(GameplayConfig config)
        {
            if (IsInitialized)
            {
                Log.W("GameplayWorldDots", "已经初始化过了");
                return;
            }

            _config = config ?? new GameplayConfig();
            _gameTime = 0f;
            _nextEntityId = 1;
            _battleResult = default;
            _killCount = 0;
            _deathCount = 0;
            _entityMap = new Dictionary<int, Entity>(256);

            // 创建 ECS World
            _world = new World("GameplayWorld");
            _entityManager = _world.EntityManager;

            // 添加系统
            var simulationGroup = _world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            _world.GetOrCreateSystem<DotsMovementSystem>();
            _world.GetOrCreateSystem<DotsLifeTimeSystem>();

            IsInitialized = true;
            IsRunning = true;

            Log.I("GameplayWorldDots", "DOTS 玩法世界初始化完成");
        }

        public void Tick(float deltaTime)
        {
            if (!IsInitialized || !IsRunning) return;

            _gameTime += deltaTime * _config.TimeScale;

            // 更新 ECS World
            _world.Update();

            // 清理待销毁的实体
            CleanupPendingDestroy();
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;

            Log.I("GameplayWorldDots", "正在关闭 DOTS 玩法世界...");

            _entityMap?.Clear();

            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
                _world = null;
            }

            IsRunning = false;
            IsInitialized = false;

            Log.I("GameplayWorldDots", "DOTS 玩法世界已关闭");
        }

        public void Pause()
        {
            if (!IsInitialized) return;
            IsRunning = false;
        }

        public void Resume()
        {
            if (!IsInitialized) return;
            IsRunning = true;
        }

        public void Reset()
        {
            if (!IsInitialized) return;

            // 销毁所有实体
            foreach (var kvp in _entityMap)
            {
                if (_entityManager.Exists(kvp.Value))
                {
                    _entityManager.DestroyEntity(kvp.Value);
                }
            }
            _entityMap.Clear();

            _nextEntityId = 1;
            _gameTime = 0f;
            _battleResult = default;
            _killCount = 0;
            _deathCount = 0;

            IsRunning = true;
            Log.I("GameplayWorldDots", "DOTS 玩法世界已重置");
        }

        public void HandleInput(PlayerInput input)
        {
            if (!IsInitialized || !IsRunning) return;

            switch (input.Type)
            {
                case InputType.Move:
                    MoveEntityTo(input.PlayerId, input.TargetPosition);
                    break;
                case InputType.Attack:
                    // 简化处理：直接造成伤害
                    DamageEntity(input.TargetEntityId, 10);
                    break;
            }
        }

        public void ExecuteCommand(GameplayCommand command)
        {
            if (!IsInitialized) return;

            switch (command.Type)
            {
                case CommandType.DamageEntity:
                    DamageEntity(command.EntityId, command.IntParam);
                    break;
                case CommandType.MoveEntity:
                    MoveEntityTo(command.EntityId, command.VectorParam);
                    break;
                case CommandType.EndBattle:
                    EndBattle((BattleResultType)command.IntParam);
                    break;
            }
        }

        public BattleResult GetBattleResult()
        {
            _battleResult.Duration = _gameTime;
            _battleResult.KillCount = _killCount;
            _battleResult.DeathCount = _deathCount;
            return _battleResult;
        }

        // ====================================================================
        // 子系统（DOTS 版本暂时使用空实现）
        // ====================================================================

        private List<IGameSystem> _systems = new List<IGameSystem>();

        public T GetSystem<T>() where T : class, IGameSystem
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                if (_systems[i] is T system)
                    return system;
            }
            return null;
        }

        public void RegisterSystem<T>(T system) where T : class, IGameSystem
        {
            if (system == null) return;
            _systems.Add(system);
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            if (IsInitialized)
            {
                system.Initialize(this);
            }
        }

        public void SetTimeScale(float scale)
        {
            if (_config != null)
                _config.TimeScale = scale;
        }

        // ====================================================================
        // 实体管理
        // ====================================================================

        /// <summary>
        /// 生成实体（接口版本）
        /// </summary>
        public int SpawnEntity(EntitySpawnInfo spawnInfo)
        {
            return SpawnEntity((int)spawnInfo.Type, spawnInfo.ConfigId, spawnInfo.CampId, 
                spawnInfo.Position, spawnInfo.Rotation, spawnInfo.MaxLifeTime);
        }

        /// <summary>
        /// 生成实体（内部版本）
        /// </summary>
        private int SpawnEntity(int entityType, int configId, int campId, Vector3 position, float rotation, float maxLifeTime = 0)
        {
            if (!IsInitialized) return -1;

            int id = _nextEntityId++;

            // 创建 ECS 实体
            Entity entity = _entityManager.CreateEntity(
                typeof(EntityDataComponent),
                typeof(PositionComponent),
                typeof(MovementComponent),
                typeof(CombatStatsComponent),
                typeof(LifeTimeComponent)
            );

            // 设置组件数据
            _entityManager.SetComponentData(entity, new EntityDataComponent
            {
                Id = id,
                ConfigId = configId,
                CampId = campId,
                EntityType = entityType,
                State = 1 // Active
            });

            _entityManager.SetComponentData(entity, new PositionComponent
            {
                Position = new float3(position.x, position.y, position.z),
                Rotation = rotation
            });

            _entityManager.SetComponentData(entity, new MovementComponent
            {
                TargetPosition = new float3(position.x, position.y, position.z),
                MoveSpeed = 5f,
                IsMoving = false
            });

            _entityManager.SetComponentData(entity, new CombatStatsComponent
            {
                Hp = 100,
                MaxHp = 100,
                Attack = 10,
                Defense = 5
            });

            _entityManager.SetComponentData(entity, new LifeTimeComponent
            {
                LifeTime = 0f,
                MaxLifeTime = maxLifeTime,
                CreateTime = _gameTime
            });

            _entityMap[id] = entity;
            return id;
        }

        /// <summary>
        /// 移动实体到目标位置
        /// </summary>
        public void MoveEntityTo(int entityId, Vector3 targetPosition)
        {
            if (!_entityMap.TryGetValue(entityId, out Entity entity)) return;
            if (!_entityManager.Exists(entity)) return;

            var movement = _entityManager.GetComponentData<MovementComponent>(entity);
            movement.TargetPosition = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
            movement.IsMoving = true;
            _entityManager.SetComponentData(entity, movement);
        }

        /// <summary>
        /// 销毁实体
        /// </summary>
        public void DestroyEntity(int entityId)
        {
            if (!_entityMap.TryGetValue(entityId, out Entity entity)) return;
            if (!_entityManager.Exists(entity)) return;

            var entityData = _entityManager.GetComponentData<EntityDataComponent>(entity);
            entityData.State = 3; // PendingDestroy
            _entityManager.SetComponentData(entity, entityData);
        }

        /// <summary>
        /// 获取实体数据
        /// </summary>
        public bool TryGetEntity(int entityId, out EntityData data)
        {
            data = default;
            if (!_entityMap.TryGetValue(entityId, out Entity entity)) return false;
            if (!_entityManager.Exists(entity)) return false;

            var ecsData = _entityManager.GetComponentData<EntityDataComponent>(entity);
            var pos = _entityManager.GetComponentData<PositionComponent>(entity);
            var movement = _entityManager.GetComponentData<MovementComponent>(entity);
            var stats = _entityManager.GetComponentData<CombatStatsComponent>(entity);
            var lifeTime = _entityManager.GetComponentData<LifeTimeComponent>(entity);

            data = new EntityData
            {
                Id = ecsData.Id,
                Type = (EntityType)ecsData.EntityType,
                State = (EntityState)ecsData.State,
                ConfigId = ecsData.ConfigId,
                CampId = ecsData.CampId,
                Position = new Vector3(pos.Position.x, pos.Position.y, pos.Position.z),
                Rotation = pos.Rotation,
                TargetPosition = new Vector3(movement.TargetPosition.x, movement.TargetPosition.y, movement.TargetPosition.z),
                IsMoving = movement.IsMoving,
                MoveSpeed = movement.MoveSpeed,
                Hp = stats.Hp,
                MaxHp = stats.MaxHp,
                Attack = stats.Attack,
                Defense = stats.Defense,
                CreateTime = lifeTime.CreateTime,
                LifeTime = lifeTime.LifeTime,
                MaxLifeTime = lifeTime.MaxLifeTime
            };
            return true;
        }

        /// <summary>
        /// 判断实体是否存活
        /// </summary>
        public bool IsEntityAlive(int entityId)
        {
            if (!_entityMap.TryGetValue(entityId, out Entity entity)) return false;
            if (!_entityManager.Exists(entity)) return false;
            var entityData = _entityManager.GetComponentData<EntityDataComponent>(entity);
            return entityData.State == 1; // Active
        }

        /// <summary>
        /// 造成伤害
        /// </summary>
        public void DamageEntity(int entityId, int damage)
        {
            if (!_entityMap.TryGetValue(entityId, out Entity entity)) return;
            if (!_entityManager.Exists(entity)) return;

            var stats = _entityManager.GetComponentData<CombatStatsComponent>(entity);
            var entityData = _entityManager.GetComponentData<EntityDataComponent>(entity);

            if (entityData.State != 1) return; // 只对活跃实体有效

            stats.Hp = math.max(0, stats.Hp - damage);
            _entityManager.SetComponentData(entity, stats);

            if (stats.Hp <= 0)
            {
                entityData.State = 2; // Dead
                _entityManager.SetComponentData(entity, entityData);

                if (entityData.CampId == 1)
                    _deathCount++;
                else
                    _killCount++;
            }
        }

        /// <summary>
        /// 治疗实体
        /// </summary>
        public void HealEntity(int entityId, int amount)
        {
            if (!_entityMap.TryGetValue(entityId, out Entity entity)) return;
            if (!_entityManager.Exists(entity)) return;

            var stats = _entityManager.GetComponentData<CombatStatsComponent>(entity);
            var entityData = _entityManager.GetComponentData<EntityDataComponent>(entity);

            if (entityData.State != 1) return;

            stats.Hp = math.min(stats.MaxHp, stats.Hp + amount);
            _entityManager.SetComponentData(entity, stats);
        }

        /// <summary>
        /// 获取所有实体（DOTS 版本返回空列表，因为数据在 ECS 中）
        /// </summary>
        public List<EntityData> GetAllEntities()
        {
            var result = new List<EntityData>();
            foreach (var kvp in _entityMap)
            {
                if (TryGetEntity(kvp.Key, out EntityData data))
                {
                    result.Add(data);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取实体索引（DOTS 中无意义，返回 ID）
        /// </summary>
        public int GetEntityIndex(int entityId)
        {
            return _entityMap.ContainsKey(entityId) ? entityId : -1;
        }

        /// <summary>
        /// 更新实体数据（DOTS 版本需要写回各组件）
        /// </summary>
        public void UpdateEntity(int index, EntityData data)
        {
            int entityId = index; // DOTS 中 index 就是 entityId
            if (!_entityMap.TryGetValue(entityId, out Entity entity)) return;
            if (!_entityManager.Exists(entity)) return;

            _entityManager.SetComponentData(entity, new EntityDataComponent
            {
                Id = data.Id,
                ConfigId = data.ConfigId,
                CampId = data.CampId,
                EntityType = (int)data.Type,
                State = (int)data.State
            });

            _entityManager.SetComponentData(entity, new PositionComponent
            {
                Position = new float3(data.Position.x, data.Position.y, data.Position.z),
                Rotation = data.Rotation
            });

            _entityManager.SetComponentData(entity, new MovementComponent
            {
                TargetPosition = new float3(data.TargetPosition.x, data.TargetPosition.y, data.TargetPosition.z),
                MoveSpeed = data.MoveSpeed,
                IsMoving = data.IsMoving
            });

            _entityManager.SetComponentData(entity, new CombatStatsComponent
            {
                Hp = data.Hp,
                MaxHp = data.MaxHp,
                Attack = data.Attack,
                Defense = data.Defense
            });

            _entityManager.SetComponentData(entity, new LifeTimeComponent
            {
                LifeTime = data.LifeTime,
                MaxLifeTime = data.MaxLifeTime,
                CreateTime = data.CreateTime
            });
        }

        /// <summary>
        /// 清理待销毁的实体（接口版本）
        /// </summary>
        public void CleanupPendingDestroyEntities()
        {
            CleanupPendingDestroy();
        }

        /// <summary>
        /// 结束战斗
        /// </summary>
        public void EndBattle(BattleResultType resultType, int score = 0)
        {
            _battleResult.ResultType = resultType;
            _battleResult.Score = score;
            IsRunning = false;
            Log.I("GameplayWorldDots", $"战斗结束: {resultType}");
        }

        /// <summary>
        /// 清理待销毁的实体
        /// </summary>
        private void CleanupPendingDestroy()
        {
            var toRemove = new List<int>();

            foreach (var kvp in _entityMap)
            {
                if (!_entityManager.Exists(kvp.Value))
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                var entityData = _entityManager.GetComponentData<EntityDataComponent>(kvp.Value);
                if (entityData.State == 2 || entityData.State == 3) // Dead or PendingDestroy
                {
                    _entityManager.DestroyEntity(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (int id in toRemove)
            {
                _entityMap.Remove(id);
            }
        }

        /// <summary>
        /// 获取实体数量
        /// </summary>
        public int EntityCount => _entityMap.Count;

        /// <summary>
        /// 获取游戏时间
        /// </summary>
        public float GetGameTime() => _gameTime;
    }
}
#endif
