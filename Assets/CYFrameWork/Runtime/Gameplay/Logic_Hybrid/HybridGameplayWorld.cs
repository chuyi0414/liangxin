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
        private readonly BrainData[] _brainData = new BrainData[MAX_UNITS];
        private int _brainCount;
        
        // ========== Muscle (DOTS) ==========
        // 计算密集型：位置更新、物理碰撞、AOE 判定
        private NativeArray<MuscleData> _muscleData;
        private NativeQueue<MoveCommand> _commandQueue;
        
        // 双缓冲：读写分离
        private NativeArray<Vector3> _positionsA;
        private NativeArray<Vector3> _positionsB;
        private bool _useBufferA = true;
        
        // Job Handle
        private JobHandle _jobHandle;
        
        // 输入缓冲
        private readonly InputBuffer _inputBuffer = new();
        
        // 三缓冲快照
        private readonly RenderSnapshot[] _snapshots = new RenderSnapshot[3];
        private int _frontIdx = 0;
        private int _backIdx = 1;
        private int _idleIdx = 2;
        
        public int InitOrder => 100;
        public int DisposeOrder => 100;
        
        #region 生命周期
        
        public void Initialize()
        {
            // 分配 Native 容器
            _muscleData = new NativeArray<MuscleData>(MAX_UNITS, Allocator.Persistent);
            _commandQueue = new NativeQueue<MoveCommand>(Allocator.Persistent);
            _positionsA = new NativeArray<Vector3>(MAX_UNITS, Allocator.Persistent);
            _positionsB = new NativeArray<Vector3>(MAX_UNITS, Allocator.Persistent);
            
            // 初始化快照
            for (int i = 0; i < 3; i++)
            {
                _snapshots[i] = RenderSnapshot.Create(MAX_UNITS);
            }
            
            CYLog.Info("[HybridGameplayWorld] 初始化完成 (DOTS 模式)");
        }
        
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
        
        public void FixedTick(float fixedDt)
        {
            // 1. 确保上一帧 Job 完成
            _jobHandle.Complete();
            
            // 2. Brain: 处理输入，生成命令
            while (_inputBuffer.TryDequeue(out var cmd))
            {
                ProcessBrainCommand(cmd);
            }
            
            // 3. Brain: 状态机/AI 决策
            for (int i = 0; i < _brainCount; i++)
            {
                UpdateBrain(ref _brainData[i], fixedDt);
            }
            
            // 4. Brain -> Muscle: 将移动命令写入队列
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
        
        public void HandleInput(InputCommand cmd)
        {
            _inputBuffer.Enqueue(cmd);
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
            // 清空命令队列，防止切后台回来后堆积命令
            while (_commandQueue.TryDequeue(out _)) { }
            _inputBuffer.Clear();
        }
        
        #endregion
        
        #region Brain 逻辑
        
        private void ProcessBrainCommand(InputCommand cmd)
        {
            // 简化示例：处理移动命令
            if (cmd.Type == InputType.Move && cmd.TargetId >= 0 && cmd.TargetId < _brainCount)
            {
                ref var brain = ref _brainData[cmd.TargetId];
                brain.HasMoveIntent = true;
                brain.MoveDirection = new Vector3(cmd.Direction.x, 0, cmd.Direction.y);
            }
        }
        
        private void UpdateBrain(ref BrainData brain, float dt)
        {
            // 状态机更新、AI 决策等
            // 这里是 OOP 代码，可以任意复杂
        }
        
        #endregion
        
        #region 快照
        
        private void WriteSnapshot()
        {
            ref var snapshot = ref _snapshots[_backIdx];
            var positions = _useBufferA ? _positionsB : _positionsA; // 读取上一帧完成的缓冲区
            
            snapshot.Count = _brainCount;
            
            for (int i = 0; i < _brainCount; i++)
            {
                snapshot.IDs[i] = _brainData[i].Id;
                snapshot.Positions[i] = positions[i];
                snapshot.Rotations[i] = Quaternion.identity;
                snapshot.HPs[i] = _brainData[i].HP;
                snapshot.StateIDs[i] = (int)_brainData[i].State;
            }
        }
        
        private void SwapSnapshots()
        {
            int temp = _frontIdx;
            _frontIdx = _backIdx;
            _backIdx = _idleIdx;
            _idleIdx = temp;
        }
        
        #endregion
        
        #region IQuery 实现
        
        public Vector3 GetPosition(int unitId)
        {
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    var positions = _useBufferA ? _positionsB : _positionsA;
                    return positions[i];
                }
            }
            return Vector3.zero;
        }
        
        public float GetHP(int unitId)
        {
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    return _brainData[i].HP;
                }
            }
            return 0f;
        }
        
        public bool IsAlive(int unitId)
        {
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    return _brainData[i].State != UnitState.Dead;
                }
            }
            return false;
        }
        
        public int GetUnitsInRange(Vector3 center, float radius, int[] resultBuffer)
        {
            int count = 0;
            var positions = _useBufferA ? _positionsB : _positionsA;
            
            for (int i = 0; i < _brainCount && count < resultBuffer.Length; i++)
            {
                if (_brainData[i].State == UnitState.Dead) continue;
                
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
        
        private int _nextUnitId = 1;
        
        public int SpawnUnit(int configId, Vector3 position, Quaternion rotation)
        {
            if (_brainCount >= MAX_UNITS)
            {
                CYLog.Warning("[HybridGameplayWorld] 单位数量已达上限");
                return -1;
            }
            
            int id = _nextUnitId++;
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
        
        public void DestroyUnit(int unitId)
        {
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    _brainData[i].State = UnitState.Dead;
                    break;
                }
            }
        }
        
        public void MoveUnit(int unitId, Vector3 targetPosition)
        {
            for (int i = 0; i < _brainCount; i++)
            {
                if (_brainData[i].Id == unitId)
                {
                    var currentPos = _useBufferA ? _positionsB[i] : _positionsA[i];
                    var direction = (targetPosition - currentPos).normalized;
                    
                    _brainData[i].HasMoveIntent = true;
                    _brainData[i].MoveDirection = direction;
                    _brainData[i].State = UnitState.Moving;
                    break;
                }
            }
        }
        
        public void DamageUnit(int unitId, float damage)
        {
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
        
        public void HealUnit(int unitId, float amount)
        {
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
        public int Id;
        public float HP;
        public UnitState State;
        
        // 移动意图
        public bool HasMoveIntent;
        public Vector3 MoveDirection;
        public float MoveSpeed;
    }
    
    public enum UnitState
    {
        Idle,
        Moving,
        Attacking,
        Dead
    }
    
    /// <summary>
    /// Muscle 数据（DOTS 侧）
    /// </summary>
    public struct MuscleData
    {
        public Vector3 Velocity;
        public float Mass;
    }
    
    /// <summary>
    /// 移动命令
    /// </summary>
    public struct MoveCommand
    {
        public int UnitIndex;
        public Vector3 Direction;
        public float Speed;
    }
    
    /// <summary>
    /// 移动 Job（Burst 编译）
    /// 文档：位置更新、物理碰撞、大规模 AOE 判定下放到 Job
    /// </summary>
    [BurstCompile]
    public struct MovementJob : IJob
    {
        public NativeQueue<MoveCommand> Commands;
        public float DeltaTime;
        public NativeArray<Vector3> Positions;
        public NativeArray<MuscleData> MuscleData;
        
        public void Execute()
        {
            // 消费命令队列
            while (Commands.TryDequeue(out var cmd))
            {
                if (cmd.UnitIndex >= 0 && cmd.UnitIndex < Positions.Length)
                {
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
        public HybridGameplayWorld()
        {
            throw new System.PlatformNotSupportedException(
                "HybridGameplayWorld 不支持 WebGL/微信平台，请使用 OOPGameplayWorld");
        }
    }
}

#endif
