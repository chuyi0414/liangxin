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
        /// <summary>
        /// 待机
        /// </summary>
        Idle = 0,
        /// <summary>
        /// 移动中
        /// </summary>
        Moving = 1,
        /// <summary>
        /// 攻击中
        /// </summary>
        Attacking = 2,
        /// <summary>
        /// 死亡
        /// </summary>
        Dead = 3
    }
    
    /// <summary>
    /// 单位数据（SOA 风格）
    /// </summary>
    public class UnitDataArrays
    {
        /// <summary>
        /// 容量
        /// </summary>
        public int Capacity;
        /// <summary>
        /// 当前数量
        /// </summary>
        public int Count;
        
        // 核心数据
        /// <summary>
        /// 单位 ID 数组
        /// </summary>
        public int[] IDs;
        /// <summary>
        /// 配置 ID 数组
        /// </summary>
        public int[] ConfigIDs;
        /// <summary>
        /// 位置数组
        /// </summary>
        public Vector3[] Positions;
        /// <summary>
        /// 旋转数组
        /// </summary>
        public Quaternion[] Rotations;
        /// <summary>
        /// 当前生命值数组
        /// </summary>
        public float[] HPs;
        /// <summary>
        /// 最大生命值数组
        /// </summary>
        public float[] MaxHPs;
        /// <summary>
        /// 状态数组
        /// </summary>
        public UnitState[] States;
        
        // 运动数据
        /// <summary>
        /// 速度数组
        /// </summary>
        public Vector3[] Velocities;
        /// <summary>
        /// 目标位置数组
        /// </summary>
        public Vector3[] TargetPositions;
        /// <summary>
        /// 速度标量数组
        /// </summary>
        public float[] Speeds;
        
        // 回收标记
        /// <summary>
        /// 存活标记数组
        /// </summary>
        public bool[] Alive;
        
        /// <summary>
        /// 构造单位数据数组
        /// </summary>
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
        /// <summary>
        /// 快照缓冲数量
        /// </summary>
        private const int SNAPSHOT_BUFFER_COUNT = 3;
        
        // 单位数据
        /// <summary>
        /// 单位数据数组
        /// </summary>
        private readonly UnitDataArrays _units;
        /// <summary>
        /// 下一个单位 ID
        /// </summary>
        private int _nextUnitId = 1;
        /// <summary>
        /// ID 到索引映射
        /// </summary>
        private readonly Dictionary<int, int> _idToIndex = new();
        /// <summary>
        /// 空闲索引栈
        /// </summary>
        private readonly Stack<int> _freeIndices = new();
        
        // 输入缓冲
        /// <summary>
        /// 输入缓冲
        /// </summary>
        private readonly InputBuffer _inputBuffer;
        
        // 三缓冲快照
        /// <summary>
        /// 快照缓冲数组
        /// </summary>
        private readonly RenderSnapshot[] _snapshots;
        /// <summary>
        /// 前缓冲索引（渲染读）
        /// </summary>
        private int _frontIdx = 0;  // 渲染读
        /// <summary>
        /// 后缓冲索引（逻辑写）
        /// </summary>
        private int _backIdx = 1;   // 逻辑写
        /// <summary>
        /// 空闲缓冲索引（插值用上一帧）
        /// </summary>
        private int _idleIdx = 2;   // 插值用上一帧
        
        // 逻辑系统
        /// <summary>
        /// 逻辑系统列表
        /// </summary>
        private readonly List<IOOPSystem> _systems = new();
        
        // 时间
        /// <summary>
        /// 逻辑时间
        /// </summary>
        private float _logicTime;
        /// <summary>
        /// 是否需要重置 DeltaTime
        /// </summary>
        private bool _needResetDelta;
        
        /// <summary>
        /// 构造玩法世界
        /// </summary>
        public OOPGameplayWorld()
        {
            _units = new UnitDataArrays(MAX_UNITS);
            _inputBuffer = new InputBuffer();
            
            // 初始化三缓冲快照
            _snapshots = new RenderSnapshot[SNAPSHOT_BUFFER_COUNT];
            // i 为索引
            for (int i = 0; i < SNAPSHOT_BUFFER_COUNT; i++)
            {
                _snapshots[i] = RenderSnapshot.Create(MAX_UNITS);
            }
        }
        
        #region IGameplayWorld 实现
        
        /// <summary>
        /// 初始化玩法世界
        /// </summary>
        public void Initialize()
        {
            // 注册默认系统
            AddSystem(new MovementSystem(this));
            AddSystem(new CombatSystem(this));
            
            CYLog.Info("[OOPGameplayWorld] 初始化完成");
        }
        
        /// <summary>
        /// 释放玩法世界
        /// </summary>
        public void Dispose()
        {
            _systems.Clear();
            _idToIndex.Clear();
            _freeIndices.Clear();
            
            CYLog.Info("[OOPGameplayWorld] 已销毁");
        }
        
        /// <summary>
        /// 固定逻辑帧更新
        /// </summary>
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
                // 输入命令
                ProcessInputCommand(cmd);
            }
            
            // 2. 执行所有系统
            foreach (var system in _systems)
            {
                // 当前系统
                system.Tick(_units, fixedDt);
            }
            
            // 3. 清理死亡单位
            CleanupDeadUnits();
            
            // 4. 交换快照缓冲
            SwapSnapshotBuffers();
            
            // 5. 填充新快照
            FillSnapshot(ref _snapshots[_backIdx]);
        }
        
        /// <summary>
        /// 接收输入命令
        /// </summary>
        public void HandleInput(in InputCommand command)
        {
            _inputBuffer.Enqueue(command);
        }
        
        /// <summary>
        /// 获取渲染快照（前缓冲）
        /// </summary>
        public ref readonly RenderSnapshot GetRenderSnapshot()
        {
            return ref _snapshots[_frontIdx];
        }
        
        /// <summary>
        /// 获取上一帧快照（空闲缓冲）
        /// </summary>
        public ref readonly RenderSnapshot GetPrevSnapshot()
        {
            return ref _snapshots[_idleIdx];
        }
        
        /// <summary>
        /// 请求重置 DeltaTime
        /// </summary>
        public void ResetDeltaTime()
        {
            _needResetDelta = true;
            CYLog.Debug("[OOPGameplayWorld] 重置 DeltaTime");
        }
        
        #endregion
        
        #region IQuery 实现
        
        /// <summary>
        /// 获取单位位置
        /// </summary>
        public Vector3 GetPosition(int unitId)
        {
            // 单位索引
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                return _units.Positions[idx];
            }
            return Vector3.zero;
        }
        
        /// <summary>
        /// 获取单位生命值
        /// </summary>
        public float GetHP(int unitId)
        {
            // 单位索引
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                return _units.HPs[idx];
            }
            return 0;
        }
        
        /// <summary>
        /// 单位是否存活
        /// </summary>
        public bool IsAlive(int unitId)
        {
            // 单位索引
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                return _units.Alive[idx];
            }
            return false;
        }
        
        /// <summary>
        /// 获取范围内单位
        /// </summary>
        public int GetUnitsInRange(Vector3 center, float radius, int[] resultBuffer)
        {
            // 命中数量
            int count = 0;
            // 半径平方
            float radiusSqr = radius * radius;
            
            // i 为索引
            for (int i = 0; i < _units.Count && count < resultBuffer.Length; i++)
            {
                if (!_units.Alive[i]) continue;
                
                // 距离平方
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
        
        /// <summary>
        /// 生成单位
        /// </summary>
        public int SpawnUnit(int configId, Vector3 position, Quaternion rotation)
        {
            // 单位索引
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
            
            // 单位 ID
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
        
        /// <summary>
        /// 销毁单位
        /// </summary>
        public void DestroyUnit(int unitId)
        {
            // 单位索引
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                _units.Alive[idx] = false;
                _units.States[idx] = UnitState.Dead;
            }
        }
        
        /// <summary>
        /// 移动单位
        /// </summary>
        public void MoveUnit(int unitId, Vector3 targetPosition)
        {
            // 单位索引
            if (_idToIndex.TryGetValue(unitId, out int idx))
            {
                _units.TargetPositions[idx] = targetPosition;
                _units.States[idx] = UnitState.Moving;
            }
        }
        
        /// <summary>
        /// 伤害单位
        /// </summary>
        public void DamageUnit(int unitId, float damage)
        {
            // 单位索引
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
        
        /// <summary>
        /// 治疗单位
        /// </summary>
        public void HealUnit(int unitId, float amount)
        {
            // 单位索引
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
            switch (cmd.Type)
            {
                case InputType.Move:
                    HandleMoveInput(cmd);
                    break;
                case InputType.Attack:
                    HandleAttackInput(cmd);
                    break;
                case InputType.Skill:
                    HandleSkillInput(cmd);
                    break;
                case InputType.Jump:
                    HandleJumpInput(cmd);
                    break;
                case InputType.Interact:
                    HandleInteractInput(cmd);
                    break;
            }
        }
        
        /// <summary>
        /// 处理移动输入
        /// </summary>
        private void HandleMoveInput(InputCommand cmd)
        {
            // 假设 TargetId 是玩家控制的单位 ID
            // 或者使用第一个活着的单位
            // 目标单位 ID
            int unitId = cmd.SkillId > 0 ? cmd.SkillId : GetFirstAliveUnitId();
            if (unitId <= 0) return;
            
            // 单位索引
            if (!_idToIndex.TryGetValue(unitId, out int idx)) return;
            if (!_units.Alive[idx]) return;
            
            // 设置移动目标
            // 移动方向
            Vector3 moveDir = new Vector3(cmd.Direction.x, 0, cmd.Direction.y).normalized;
            // 移动距离
            float moveDistance = 10f; // 移动距离
            _units.TargetPositions[idx] = _units.Positions[idx] + moveDir * moveDistance;
            _units.States[idx] = UnitState.Moving;
        }
        
        /// <summary>
        /// 处理攻击输入
        /// </summary>
        private void HandleAttackInput(InputCommand cmd)
        {
            // 目标单位 ID
            int unitId = cmd.SkillId > 0 ? cmd.SkillId : GetFirstAliveUnitId();
            if (unitId <= 0) return;
            
            // 单位索引
            if (!_idToIndex.TryGetValue(unitId, out int idx)) return;
            if (!_units.Alive[idx]) return;
            
            // 设置攻击状态
            _units.States[idx] = UnitState.Attacking;
        }
        
        /// <summary>
        /// 处理技能输入
        /// </summary>
        private void HandleSkillInput(InputCommand cmd)
        {
            // 技能输入处理
            // cmd.SkillId 包含技能 ID
        }
        
        /// <summary>
        /// 处理跳跃输入
        /// </summary>
        private void HandleJumpInput(InputCommand cmd)
        {
            // 跳跃输入处理
        }
        
        /// <summary>
        /// 处理交互输入
        /// </summary>
        private void HandleInteractInput(InputCommand cmd)
        {
            // 交互输入处理
        }
        
        /// <summary>
        /// 获取第一个存活单位 ID
        /// </summary>
        private int GetFirstAliveUnitId()
        {
            // i 为索引
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units.Alive[i])
                {
                    return _units.IDs[i];
                }
            }
            return -1;
        }
        
        /// <summary>
        /// 清理死亡单位
        /// </summary>
        private void CleanupDeadUnits()
        {
            // i 为索引
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                if (!_units.Alive[i] && _units.States[i] == UnitState.Dead)
                {
                    // 单位 ID
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
            // 临时索引
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
            
            // 写入数量
            int count = 0;
            // i 为索引
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
        /// <summary>
        /// 系统更新
        /// </summary>
        void Tick(UnitDataArrays units, float deltaTime);
    }
    
    /// <summary>
    /// 移动系统
    /// </summary>
    public class MovementSystem : IOOPSystem
    {
        /// <summary>
        /// 世界引用
        /// </summary>
        private readonly OOPGameplayWorld _world;
        
        /// <summary>
        /// 构造移动系统
        /// </summary>
        public MovementSystem(OOPGameplayWorld world)
        {
            _world = world;
        }
        
        /// <summary>
        /// 系统更新
        /// </summary>
        public void Tick(UnitDataArrays units, float deltaTime)
        {
            // i 为索引
            for (int i = 0; i < units.Count; i++)
            {
                if (!units.Alive[i]) continue;
                if (units.States[i] != UnitState.Moving) continue;
                
                // 当前坐标
                Vector3 current = units.Positions[i];
                // 目标坐标
                Vector3 target = units.TargetPositions[i];
                // 移动速度
                float speed = units.Speeds[i];
                
                // 移动方向
                Vector3 direction = (target - current).normalized;
                // 距离
                float distance = Vector3.Distance(current, target);
                // 本帧移动距离
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
        /// <summary>
        /// 世界引用
        /// </summary>
        private readonly OOPGameplayWorld _world;
        
        // 攻击配置
        private const float ATTACK_RANGE = 2f;
        /// <summary>
        /// 攻击伤害
        /// </summary>
        private const float ATTACK_DAMAGE = 10f;
        /// <summary>
        /// 攻击冷却
        /// </summary>
        private const float ATTACK_COOLDOWN = 1f;
        
        // 攻击冷却计时器
        /// <summary>
        /// 攻击冷却数组
        /// </summary>
        private readonly float[] _attackCooldowns;
        
        /// <summary>
        /// 构造战斗系统
        /// </summary>
        public CombatSystem(OOPGameplayWorld world)
        {
            _world = world;
            _attackCooldowns = new float[1000]; // 与 MAX_UNITS 保持一致
        }
        
        /// <summary>
        /// 系统更新
        /// </summary>
        public void Tick(UnitDataArrays units, float deltaTime)
        {
            // i 为索引
            for (int i = 0; i < units.Count; i++)
            {
                if (!units.Alive[i]) continue;
                
                // 更新攻击冷却
                if (_attackCooldowns[i] > 0)
                {
                    _attackCooldowns[i] -= deltaTime;
                }
                
                // 处理攻击状态
                if (units.States[i] == UnitState.Attacking)
                {
                    ProcessAttack(units, i, deltaTime);
                }
            }
        }
        
        /// <summary>
        /// 处理攻击逻辑
        /// </summary>
        private void ProcessAttack(UnitDataArrays units, int attackerIdx, float deltaTime)
        {
            // 检查冷却
            if (_attackCooldowns[attackerIdx] > 0)
            {
                // 冷却中，返回空闲状态
                units.States[attackerIdx] = UnitState.Idle;
                return;
            }
            
            // 攻击者位置
            Vector3 attackerPos = units.Positions[attackerIdx];
            // 攻击者 ID
            int attackerId = units.IDs[attackerIdx];
            
            // 寻找范围内的敌人（简化实现：攻击最近的单位）
            // 目标索引
            int targetIdx = -1;
            // 最小距离
            float minDistance = float.MaxValue;
            
            for (int i = 0; i < units.Count; i++)
            {
                if (i == attackerIdx) continue;
                if (!units.Alive[i]) continue;
                
                // 当前距离
                float distance = Vector3.Distance(attackerPos, units.Positions[i]);
                if (distance <= ATTACK_RANGE && distance < minDistance)
                {
                    minDistance = distance;
                    targetIdx = i;
                }
            }
            
            if (targetIdx >= 0)
            {
                // 造成伤害
                units.HPs[targetIdx] -= ATTACK_DAMAGE;
                
                // 检查死亡
                if (units.HPs[targetIdx] <= 0)
                {
                    units.HPs[targetIdx] = 0;
                    units.States[targetIdx] = UnitState.Dead;
                    units.Alive[targetIdx] = false;
                }
                
                // 设置攻击冷却
                _attackCooldowns[attackerIdx] = ATTACK_COOLDOWN;
            }
            
            // 攻击完成，返回空闲
            units.States[attackerIdx] = UnitState.Idle;
        }
    }
    
    #endregion
}
