// ============================================================================
// CYFramework 2.2 - Hybrid DOTS 玩法世界
// 文档位置：3.2.3 实现 B：Hybrid DOTS (PC/高端机增强)
// 功能：大脑 (Brain - OOP) + 肌肉 (Muscle - DOTS)
// ❗ 仅 PC/Mobile Native 端使用，WebGL/微信不支持
// ============================================================================

// 仅在支持 DOTS 的平台编译
#if !UNITY_WEBGL && !CY_WECHAT && ENABLE_DOTS

using System;
using CYFramework.Gameplay.Abstraction;
using CYFramework.Gameplay.Common;
using CYFramework.Infrastructure;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace CYFramework.Gameplay.Hybrid
{
    /// <summary>
    /// Hybrid DOTS 玩法世界
    /// 文档：大脑用 C# 写，肌肉用 Job System + Burst
    /// </summary>
    public class HybridGameplayWorld : IGameplayWorld, IQuery, ICommand, IInitializable, IDisposableEx
    {
        // 最大单位数
        private const int MAX_UNITS = 2000;
        
        // ========== Brain (OOP) ==========
        // 复杂逻辑：技能判定、状态机、AI 决策树
        /// <summary>
        /// Brain 数据数组
        /// </summary>
        private readonly BrainData[] _brainData = new BrainData[MAX_UNITS];
        /// <summary>
        /// 当前 Brain 数量
        /// </summary>
        private int _brainCount;
        
        // ========== Muscle (DOTS) ==========
        // 计算密集型：位置更新、物理碰撞、AOE 判定
        /// <summary>
        /// Muscle 数据数组
        /// </summary>
        private NativeArray<MuscleData> _muscleData;
        /// <summary>
        /// 移动命令队列
        /// </summary>
        private NativeQueue<MoveCommand> _commandQueue;
        
        // 双缓冲：读写分离
        /// <summary>
        /// 位置缓冲 A
        /// </summary>
        private NativeArray<Vector3> _positionsA;
        /// <summary>
        /// 位置缓冲 B
        /// </summary>
        private NativeArray<Vector3> _positionsB;
        /// <summary>
        /// 是否使用缓冲 A
        /// </summary>
        private bool _useBufferA = true;
        
        // Job Handle
        /// <summary>
        /// Job 句柄
        /// </summary>
        private JobHandle _jobHandle;
        
        // 输入缓冲
        /// <summary>
        /// 输入缓冲器
        /// </summary>
        private readonly InputBuffer _inputBuffer = new();
        
        // 三缓冲快照
        /// <summary>
        /// 快照缓冲数组
        /// </summary>
        private readonly RenderSnapshot[] _snapshots = new RenderSnapshot[3];
        /// <summary>
        /// 前缓冲索引
        /// </summary>
        private int _frontIdx = 0;
        /// <summary>
        /// 后缓冲索引
        /// </summary>
        private int _backIdx = 1;
        /// <summary>
        /// 空闲缓冲索引
        /// </summary>
        private int _idleIdx = 2;
        
        /// <summary>
        /// 初始化顺序
        /// </summary>
        public int InitOrder => 100;
        /// <summary>
        /// 释放顺序
        /// </summary>
        public int DisposeOrder => 100;
        
        #region 生命周期
        
        /// <summary>
        /// 初始化玩法世界
        /// </summary>
        public void Initialize()
        {
            // 分配 Native 容器
            _muscleData = new NativeArray<MuscleData>(MAX_UNITS, Allocator.Persistent);
            _commandQueue = new NativeQueue<MoveCommand>(Allocator.Persistent);
            _positionsA = new NativeArray<Vector3>(MAX_UNITS, Allocator.Persistent);
            _positionsB = new NativeArray<Vector3>(MAX_UNITS, Allocator.Persistent);
            
            // 初始化快照
            // i 为索引
            for (int i = 0; i < 3; i++)
            {
                _snapshots[i] = RenderSnapshot.Create(MAX_UNITS);
            }
            
            CYLog.Info("[HybridGameplayWorld] 初始化完成 (DOTS 模式)");
        }
        
        /// <summary>
        /// 释放玩法世界
        /// </summary>
        public void Dispose()
        {
            // 确保 Job 完成
            _jobHandle.Complete();
            
            // 释放 Native 容器
            if (_muscleData.IsCreated) _muscleData.Dispose();
            if (_commandQueue.IsCreated) _commandQueue.Dispose();
            if (_positionsA.IsCreated) _positionsA.Dispose();
            if (_positionsB.IsCreated) _positionsB.Dispose();
            
            CYLog.Info("[HybridGameplayWorld] 已销毁");
        }
        
        #endregion
        
        #region IGameplayWorld
        
        /// <summary>
        /// 固定逻辑帧更新
        /// </summary>
        public void FixedTick(float fixedDt)
        {
            // 1. 确保上一帧 Job 完成
            _jobHandle.Complete();
            
            // 2. Brain: 处理输入，生成命令
            while (_inputBuffer.TryDequeue(out var cmd))
            {
                // 输入命令
                ProcessBrainCommand(cmd);
            }
            
            // 3. Brain: 状态机/AI 决策
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                UpdateBrain(ref _brainData[i], fixedDt);
            }
            
            // 4. Brain -> Muscle: 将移动命令写入队列
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].HasMoveIntent)
                {
                    _commandQueue.Enqueue(new MoveCommand
                    {
                        UnitIndex = i,
                        Direction = _brainData[i].MoveDirection,
                        Speed = _brainData[i].MoveSpeed
                    });
                    _brainData[i].HasMoveIntent = false;
                }
            }
            
            // 5. Muscle: 调度 Job（位置更新）
            // 移动 Job
            var moveJob = new MovementJob
            {
                Commands = _commandQueue,
                DeltaTime = fixedDt,
                Positions = _useBufferA ? _positionsA : _positionsB,
                MuscleData = _muscleData
            };
            
            _jobHandle = moveJob.Schedule();
            
            // 6. 交换缓冲区
            _useBufferA = !_useBufferA;
            
            // 7. 写入快照（从已完成的缓冲区读取）
            WriteSnapshot();
            SwapSnapshots();
        }
        
        /// <summary>
        /// 接收输入命令
        /// </summary>
        public void HandleInput(InputCommand cmd)
        {
            _inputBuffer.Enqueue(cmd);
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
        /// 重置 DeltaTime（清空命令缓存）
        /// </summary>
        public void ResetDeltaTime()
        {
            // 清空命令队列，防止切后台回来后堆积命令
            while (_commandQueue.TryDequeue(out _)) { }
            _inputBuffer.Clear();
        }
        
        #endregion
        
        #region Brain 逻辑
        
        /// <summary>
        /// 处理 Brain 输入命令
        /// </summary>
        private void ProcessBrainCommand(InputCommand cmd)
        {
            // 简化示例：处理移动命令
            if (cmd.Type == InputType.Move && cmd.TargetId >= 0 && cmd.TargetId < _brainCount)
            {
                // Brain 数据引用
                ref var brain = ref _brainData[cmd.TargetId];
                brain.HasMoveIntent = true;
                brain.MoveDirection = new Vector3(cmd.Direction.x, 0, cmd.Direction.y);
            }
        }
        
        /// <summary>
        /// 更新 Brain 逻辑
        /// </summary>
        private void UpdateBrain(ref BrainData brain, float dt)
        {
            // 状态机更新、AI 决策等
            // 这里是 OOP 代码，可以任意复杂
        }
        
        #endregion
        
        #region 快照
        
        /// <summary>
        /// 写入快照
        /// </summary>
        private void WriteSnapshot()
        {
            // 写入目标快照
            ref var snapshot = ref _snapshots[_backIdx];
            // 读取上一帧完成的缓冲区
            var positions = _useBufferA ? _positionsB : _positionsA; // 读取上一帧完成的缓冲区
            
            snapshot.Count = _brainCount;
            
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                snapshot.IDs[i] = _brainData[i].Id;
                snapshot.Positions[i] = positions[i];
                snapshot.Rotations[i] = Quaternion.identity;
                snapshot.HPs[i] = _brainData[i].HP;
                snapshot.StateIDs[i] = (int)_brainData[i].State;
            }
        }
        
        /// <summary>
        /// 交换快照缓冲
        /// </summary>
        private void SwapSnapshots()
        {
            // 临时索引
            int temp = _frontIdx;
            _frontIdx = _backIdx;
            _backIdx = _idleIdx;
            _idleIdx = temp;
        }
        
        #endregion
        
        #region IQuery 实现
        
        /// <summary>
        /// 获取单位位置
        /// </summary>
        public Vector3 GetPosition(int unitId)
        {
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    // 当前位置缓冲
                    var positions = _useBufferA ? _positionsB : _positionsA;
                    return positions[i];
                }
            }
            return Vector3.zero;
        }
        
        /// <summary>
        /// 获取单位生命值
        /// </summary>
        public float GetHP(int unitId)
        {
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    return _brainData[i].HP;
                }
            }
            return 0f;
        }
        
        /// <summary>
        /// 单位是否存活
        /// </summary>
        public bool IsAlive(int unitId)
        {
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    return _brainData[i].State != UnitState.Dead;
                }
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
            // 当前位置缓冲
            var positions = _useBufferA ? _positionsB : _positionsA;
            
            for (int i = 0; i < _brainCount && count < resultBuffer.Length; i++)
            {
                if (_brainData[i].State == UnitState.Dead) continue;
                
                // 当前距离
                float distance = Vector3.Distance(center, positions[i]);
                if (distance <= radius)
                {
                    resultBuffer[count++] = _brainData[i].Id;
                }
            }
            return count;
        }
        
        #endregion
        
        #region ICommand 实现
        
        /// <summary>
        /// 下一个单位 ID
        /// </summary>
        private int _nextUnitId = 1;
        
        /// <summary>
        /// 生成单位
        /// </summary>
        public int SpawnUnit(int configId, Vector3 position, Quaternion rotation)
        {
            if (_brainCount >= MAX_UNITS)
            {
                CYLog.Warning("[HybridGameplayWorld] 单位数量已达上限");
                return -1;
            }
            
            // 分配的单位 ID
            int id = _nextUnitId++;
            // 单位索引
            int idx = _brainCount++;
            
            _brainData[idx] = new BrainData
            {
                Id = id,
                HP = 100f, // TODO: 从配置读取
                State = UnitState.Idle,
                MoveSpeed = 5f
            };
            
            // 设置初始位置
            if (_useBufferA)
            {
                _positionsA[idx] = position;
            }
            else
            {
                _positionsB[idx] = position;
            }
            
            return id;
        }
        
        /// <summary>
        /// 销毁单位
        /// </summary>
        public void DestroyUnit(int unitId)
        {
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    _brainData[i].State = UnitState.Dead;
                    break;
                }
            }
        }
        
        /// <summary>
        /// 移动单位
        /// </summary>
        public void MoveUnit(int unitId, Vector3 targetPosition)
        {
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    // 当前坐标
                    var currentPos = _useBufferA ? _positionsB[i] : _positionsA[i];
                    // 移动方向
                    var direction = (targetPosition - currentPos).normalized;
                    
                    _brainData[i].HasMoveIntent = true;
                    _brainData[i].MoveDirection = direction;
                    _brainData[i].State = UnitState.Moving;
                    break;
                }
            }
        }
        
        /// <summary>
        /// 伤害单位
        /// </summary>
        public void DamageUnit(int unitId, float damage)
        {
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    _brainData[i].HP -= damage;
                    if (_brainData[i].HP <= 0)
                    {
                        _brainData[i].HP = 0;
                        _brainData[i].State = UnitState.Dead;
                    }
                    break;
                }
            }
        }
        
        /// <summary>
        /// 治疗单位
        /// </summary>
        public void HealUnit(int unitId, float amount)
        {
            // i 为索引
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    _brainData[i].HP += amount;
                    // TODO: 限制最大生命值
                    break;
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Brain 数据（OOP 侧）
    /// </summary>
    public struct BrainData
    {
        /// <summary>
        /// 单位 ID
        /// </summary>
        public int Id;
        /// <summary>
        /// 当前生命值
        /// </summary>
        public float HP;
        /// <summary>
        /// 当前状态
        /// </summary>
        public UnitState State;
        
        // 移动意图
        /// <summary>
        /// 是否有移动意图
        /// </summary>
        public bool HasMoveIntent;
        /// <summary>
        /// 移动方向
        /// </summary>
        public Vector3 MoveDirection;
        /// <summary>
        /// 移动速度
        /// </summary>
        public float MoveSpeed;
    }
    
    /// <summary>
    /// 单位状态
    /// </summary>
    public enum UnitState
    {
        /// <summary>
        /// 待机
        /// </summary>
        Idle,
        /// <summary>
        /// 移动中
        /// </summary>
        Moving,
        /// <summary>
        /// 攻击中
        /// </summary>
        Attacking,
        /// <summary>
        /// 死亡
        /// </summary>
        Dead
    }
    
    /// <summary>
    /// Muscle 数据（DOTS 侧）
    /// </summary>
    public struct MuscleData
    {
        /// <summary>
        /// 速度
        /// </summary>
        public Vector3 Velocity;
        /// <summary>
        /// 质量
        /// </summary>
        public float Mass;
    }
    
    /// <summary>
    /// 移动命令
    /// </summary>
    public struct MoveCommand
    {
        /// <summary>
        /// 单位索引
        /// </summary>
        public int UnitIndex;
        /// <summary>
        /// 移动方向
        /// </summary>
        public Vector3 Direction;
        /// <summary>
        /// 移动速度
        /// </summary>
        public float Speed;
    }
    
    /// <summary>
    /// 移动 Job（Burst 编译）
    /// 文档：位置更新、物理碰撞、大规模 AOE 判定下放到 Job
    /// </summary>
    [BurstCompile]
    public struct MovementJob : IJob
    {
        /// <summary>
        /// 移动命令队列
        /// </summary>
        public NativeQueue<MoveCommand> Commands;
        /// <summary>
        /// 逻辑时间步长
        /// </summary>
        public float DeltaTime;
        /// <summary>
        /// 位置数组
        /// </summary>
        public NativeArray<Vector3> Positions;
        /// <summary>
        /// Muscle 数据数组
        /// </summary>
        public NativeArray<MuscleData> MuscleData;
        
        /// <summary>
        /// 执行移动更新
        /// </summary>
        public void Execute()
        {
            // 消费命令队列
            while (Commands.TryDequeue(out var cmd))
            {
                // 当前命令
                if (cmd.UnitIndex >= 0 && cmd.UnitIndex < Positions.Length)
                {
                    // 速度向量
                    var velocity = cmd.Direction.normalized * cmd.Speed;
                    Positions[cmd.UnitIndex] += velocity * DeltaTime;
                }
            }
        }
    }
}

#else

// WebGL/微信平台：提供空实现避免编译错误
namespace CYFramework.Gameplay.Hybrid
{
    /// <summary>
    /// Hybrid DOTS 玩法世界（占位符）
    /// WebGL/微信平台不支持，请使用 OOPGameplayWorld
    /// </summary>
    public class HybridGameplayWorld
    {
        /// <summary>
        /// 构造函数（占位符）
        /// </summary>
        public HybridGameplayWorld()
        {
            throw new System.PlatformNotSupportedException(
                "HybridGameplayWorld 不支持 WebGL/微信平台，请使用 OOPGameplayWorld");
        }
    }
}

#endif
