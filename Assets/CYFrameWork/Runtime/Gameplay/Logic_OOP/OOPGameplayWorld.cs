// ============================================================================
// CYFramework 2.2 - OOP Lite 玩法世界实现
// 文档位置：3.2.2 实现 A：OOP Lite (微信/低端机基线)
// 架构：SOA (Structure of Arrays) 风格，纯 C# 数组 + for 循环
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Gameplay.Abstraction;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Gameplay.Logic_OOP
{
    /// <summary>
    /// 单位状态
    /// </summary>
    public enum UnitState
    {
        Idle = 0,
        Moving = 1,
        Attacking = 2,
        Dead = 3
    }
    
    /// <summary>
    /// 单位数据（SOA 风格）
    /// </summary>
    public class UnitDataArrays
    {
        public int Capacity;
        public int Count;
        
        // 核心数据
        public int[] IDs;
        public int[] ConfigIDs;
        public Vector3[] Positions;
        public Quaternion[] Rotations;
        public float[] HPs;
        public float[] MaxHPs;
        public UnitState[] States;
        
        // 运动数据
        public Vector3[] Velocities;
        public Vector3[] TargetPositions;
        public float[] Speeds;
        
        // 回收标记
        public bool[] Alive;
        
        public UnitDataArrays(int capacity)
        {
            Capacity = capacity;
            Count = 0;
            
            IDs = new int[capacity];
            ConfigIDs = new int[capacity];
            Positions = new Vector3[capacity];
            Rotations = new Quaternion[capacity];
            HPs = new float[capacity];
            MaxHPs = new float[capacity];
            States = new UnitState[capacity];
            Velocities = new Vector3[capacity];
            TargetPositions = new Vector3[capacity];
            Speeds = new float[capacity];
            Alive = new bool[capacity];
        }
    }
    
    /// <summary>
    /// OOP Lite 玩法世界
    /// 微信/低端机基线实现
    /// </summary>
    public class OOPGameplayWorld : IGameplayWorld, IQuery, ICommand
    {
        // 配置
        private const int MAX_UNITS = 1000;
        private const int SNAPSHOT_BUFFER_COUNT = 3;
        
        // 单位数据
        private readonly UnitDataArrays _units;
        private int _nextUnitId = 1;
        private readonly Dictionary<int, int> _idToIndex = new();
        private readonly Stack<int> _freeIndices = new();
        
        // 输入缓冲
        private readonly InputBuffer _inputBuffer;
        
        // 三缓冲快照
        private readonly RenderSnapshot[] _snapshots;
        private int _frontIdx = 0;  // 渲染读
        private int _backIdx = 1;   // 逻辑写
        private int _idleIdx = 2;   // 插值用上一帧
        
        // 逻辑系统
        private readonly List<IOOPSystem> _systems = new();
        
        // 时间
        private float _logicTime;
        private bool _needResetDelta;
        
        public OOPGameplayWorld()
        {
            _units = new UnitDataArrays(MAX_UNITS);
            _inputBuffer = new InputBuffer();
            
            // 初始化三缓冲快照
            _snapshots = new RenderSnapshot[SNAPSHOT_BUFFER_COUNT];
            for (int i = 0; i < SNAPSHOT_BUFFER_COUNT; i++)
            {
                _snapshots[i] = RenderSnapshot.Create(MAX_UNITS);
            }
        }
        
        #region IGameplayWorld 实现
        
        public void Initialize()
        {
            // 注册默认系统
            AddSystem(new MovementSystem(this));
            AddSystem(new CombatSystem(this));
            
            CYLog.Info("[OOPGameplayWorld] 初始化完成");
        }
        
        public void Dispose()
        {
            _systems.Clear();
            _idToIndex.Clear();
            _freeIndices.Clear();
            
            CYLog.Info("[OOPGameplayWorld] 已销毁");
        }
        
        public void FixedTick(float fixedDt)
        {
            if (_needResetDelta)
            {
                _needResetDelta = false;
                // 不累加时间，防止瞬移
            }
            else
            {
                _logicTime += fixedDt;
            }
            
            // 1. 消费输入缓冲
            while (_inputBuffer.TryDequeue(out var cmd))
            {
                ProcessInputCommand(cmd);
            }
            
            // 2. 执行所有系统
            foreach (var system in _systems)
            {
                system.Tick(_units, fixedDt);
            }
            
            // 3. 清理死亡单位
            CleanupDeadUnits();
            
            // 4. 交换快照缓冲
            SwapSnapshotBuffers();
            
            // 5. 填充新快照
            FillSnapshot(ref _snapshots[_backIdx]);
        }
        
        public void HandleInput(in InputCommand command)
        {
            _inputBuffer.Enqueue(command);
        }
        
        public ref readonly RenderSnapshot GetRenderSnapshot()
        {
            return ref _snapshots[_frontIdx];
        }
        
        public ref readonly RenderSnapshot GetPrevSnapshot()
        {
            return ref _snapshots[_idleIdx];
        }
        
        public void ResetDeltaTime()
        {
            _needResetDelta = true;
            CYLog.Debug("[OOPGameplayWorld] 重置 DeltaTime");
        }
        
        #endregion
        
        #region IQuery 实现
        
        public Vector3 GetPosition(int unitId)
        {
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                return _units.Positions[idx];
            }
            return Vector3.zero;
        }
        
        public float GetHP(int unitId)
        {
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                return _units.HPs[idx];
            }
            return 0;
        }
        
        public bool IsAlive(int unitId)
        {
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                return _units.Alive[idx];
            }
            return false;
        }
        
        public int GetUnitsInRange(Vector3 center, float radius, int[] resultBuffer)
        {
            int count = 0;
            float radiusSqr = radius * radius;
            
            for (int i = 0; i < _units.Count && count < resultBuffer.Length; i++)
            {
                if (!_units.Alive[i]) continue;
                
                float distSqr = (center - _units.Positions[i]).sqrMagnitude;
                if (distSqr <= radiusSqr)
                {
                    resultBuffer[count++] = _units.IDs[i];
                }
            }
            
            return count;
        }
        
        #endregion
        
        #region ICommand 实现
        
        public int SpawnUnit(int configId, Vector3 position, Quaternion rotation)
        {
            int idx;
            
            if (_freeIndices.Count > 0)
            {
                idx = _freeIndices.Pop();
            }
            else if (_units.Count < _units.Capacity)
            {
                idx = _units.Count++;
            }
            else
            {
                CYLog.Error("[OOPGameplayWorld] 单位数量已达上限");
                return -1;
            }
            
            int id = _nextUnitId++;
            
            _units.IDs[idx] = id;
            _units.ConfigIDs[idx] = configId;
            _units.Positions[idx] = position;
            _units.Rotations[idx] = rotation;
            _units.HPs[idx] = 100f; // TODO: 从配置读取
            _units.MaxHPs[idx] = 100f;
            _units.States[idx] = UnitState.Idle;
            _units.Velocities[idx] = Vector3.zero;
            _units.TargetPositions[idx] = position;
            _units.Speeds[idx] = 5f;
            _units.Alive[idx] = true;
            
            _idToIndex[id] = idx;
            
            CYLog.Debug($"[OOPGameplayWorld] 生成单位 ID={id} 位置={position}");
            return id;
        }
        
        public void DestroyUnit(int unitId)
        {
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                _units.Alive[idx] = false;
                _units.States[idx] = UnitState.Dead;
            }
        }
        
        public void MoveUnit(int unitId, Vector3 targetPosition)
        {
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                _units.TargetPositions[idx] = targetPosition;
                _units.States[idx] = UnitState.Moving;
            }
        }
        
        public void DamageUnit(int unitId, float damage)
        {
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                _units.HPs[idx] = Mathf.Max(0, _units.HPs[idx] - damage);
                
                if (_units.HPs[idx] <= 0)
                {
                    _units.States[idx] = UnitState.Dead;
                    _units.Alive[idx] = false;
                }
            }
        }
        
        public void HealUnit(int unitId, float amount)
        {
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                _units.HPs[idx] = Mathf.Min(_units.MaxHPs[idx], _units.HPs[idx] + amount);
            }
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 添加系统
        /// </summary>
        public void AddSystem(IOOPSystem system)
        {
            _systems.Add(system);
        }
        
        /// <summary>
        /// 处理输入命令
        /// </summary>
        private void ProcessInputCommand(InputCommand cmd)
        {
            // TODO: 根据输入类型执行对应逻辑
            switch (cmd.Type)
            {
                case InputType.Move:
                    // 移动玩家控制的单位
                    break;
                case InputType.Attack:
                    // 执行攻击
                    break;
            }
        }
        
        /// <summary>
        /// 清理死亡单位
        /// </summary>
        private void CleanupDeadUnits()
        {
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                if (!_units.Alive[i] && _units.States[i] == UnitState.Dead)
                {
                    int id = _units.IDs[i];
                    _idToIndex.Remove(id);
                    _freeIndices.Push(i);
                }
            }
        }
        
        /// <summary>
        /// 交换快照缓冲
        /// 文档位置：3.3 三缓冲环形队列
        /// </summary>
        private void SwapSnapshotBuffers()
        {
            int temp = _frontIdx;
            _frontIdx = _backIdx;
            _backIdx = _idleIdx;
            _idleIdx = temp;
        }
        
        /// <summary>
        /// 填充快照
        /// </summary>
        private void FillSnapshot(ref RenderSnapshot snapshot)
        {
            snapshot.Clear();
            snapshot.Timestamp = _logicTime;
            
            int count = 0;
            for (int i = 0; i < _units.Count && count < MAX_UNITS; i++)
            {
                if (!_units.Alive[i]) continue;
                
                snapshot.IDs[count] = _units.IDs[i];
                snapshot.Positions[count] = _units.Positions[i];
                snapshot.Rotations[count] = _units.Rotations[i];
                snapshot.HPs[count] = _units.HPs[i];
                snapshot.StateIDs[count] = (int)_units.States[i];
                count++;
            }
            
            snapshot.Count = count;
        }
        
        #endregion
    }
    
    #region OOP 系统接口
    
    /// <summary>
    /// OOP 系统接口
    /// </summary>
    public interface IOOPSystem
    {
        void Tick(UnitDataArrays units, float deltaTime);
    }
    
    /// <summary>
    /// 移动系统
    /// </summary>
    public class MovementSystem : IOOPSystem
    {
        private readonly OOPGameplayWorld _world;
        
        public MovementSystem(OOPGameplayWorld world)
        {
            _world = world;
        }
        
        public void Tick(UnitDataArrays units, float deltaTime)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (!units.Alive[i]) continue;
                if (units.States[i] != UnitState.Moving) continue;
                
                Vector3 current = units.Positions[i];
                Vector3 target = units.TargetPositions[i];
                float speed = units.Speeds[i];
                
                Vector3 direction = (target - current).normalized;
                float distance = Vector3.Distance(current, target);
                float moveDistance = speed * deltaTime;
                
                if (moveDistance >= distance)
                {
                    // 到达目标
                    units.Positions[i] = target;
                    units.States[i] = UnitState.Idle;
                    units.Velocities[i] = Vector3.zero;
                }
                else
                {
                    // 继续移动
                    units.Positions[i] += direction * moveDistance;
                    units.Velocities[i] = direction * speed;
                    
                    // 更新朝向
                    if (direction != Vector3.zero)
                    {
                        units.Rotations[i] = Quaternion.LookRotation(direction);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 战斗系统
    /// </summary>
    public class CombatSystem : IOOPSystem
    {
        private readonly OOPGameplayWorld _world;
        
        public CombatSystem(OOPGameplayWorld world)
        {
            _world = world;
        }
        
        public void Tick(UnitDataArrays units, float deltaTime)
        {
            // TODO: 实现战斗逻辑
            // 如自动攻击、技能 CD 等
        }
    }
    
    #endregion
}
