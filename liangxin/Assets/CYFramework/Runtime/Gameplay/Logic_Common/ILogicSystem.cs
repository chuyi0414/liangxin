// ============================================================================
// CYFramework 2.2 - 共用逻辑接口
// 文档位置：4. 目录结构 - Logic_Common
// 功能：OOP 与 Hybrid 共用的纯逻辑（状态机/AI）
// ============================================================================

using UnityEngine;

namespace CYFramework.Gameplay.Common
{
    /// <summary>
    /// 逻辑系统接口
    /// OOP 和 Hybrid 都可以复用
    /// </summary>
    public interface ILogicSystem
    {
        /// <summary>
        /// 处理输入命令
        /// </summary>
        void ProcessCommand(InputCommand cmd);
        
        /// <summary>
        /// 逻辑步进
        /// </summary>
        void Step(float dt);
    }
    
    /// <summary>
    /// 输入命令
    /// </summary>
    public struct InputCommand
    {
        /// <summary>
        /// 输入类型
        /// </summary>
        public InputType Type;
        /// <summary>
        /// 时间戳
        /// </summary>
        public float Timestamp;
        /// <summary>
        /// 方向向量
        /// </summary>
        public Vector2 Direction;
        /// <summary>
        /// 目标 ID
        /// </summary>
        public int TargetId;
    }
    
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
        Interact
    }
    
    /// <summary>
    /// 状态机接口
    /// 文档：OOP与Hybrid共用的纯逻辑(状态机/AI)
    /// </summary>
    public interface IStateMachine<TState> where TState : System.Enum
    {
        /// <summary>
        /// 当前状态
        /// </summary>
        TState CurrentState { get; }
        /// <summary>
        /// 切换状态
        /// </summary>
        void ChangeState(TState newState);
        /// <summary>
        /// 状态更新
        /// </summary>
        void Update(float dt);
    }
    
    /// <summary>
    /// 简单有限状态机实现
    /// </summary>
    public class SimpleFSM<TState> : IStateMachine<TState> where TState : System.Enum
    {
        /// <summary>
        /// 当前状态
        /// </summary>
        public TState CurrentState { get; private set; }
        
        /// <summary>
        /// 状态更新回调表
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<TState, System.Action<float>> _updateActions = new();
        /// <summary>
        /// 状态进入回调表
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<TState, System.Action> _enterActions = new();
        /// <summary>
        /// 状态退出回调表
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<TState, System.Action> _exitActions = new();
        
        /// <summary>
        /// 构造状态机
        /// </summary>
        public SimpleFSM(TState initialState)
        {
            CurrentState = initialState;
        }
        
        /// <summary>
        /// 注册状态回调
        /// </summary>
        public void RegisterState(TState state, System.Action onEnter = null, System.Action<float> onUpdate = null, System.Action onExit = null)
        {
            if (onEnter != null) _enterActions[state] = onEnter;
            if (onUpdate != null) _updateActions[state] = onUpdate;
            if (onExit != null) _exitActions[state] = onExit;
        }
        
        /// <summary>
        /// 切换状态
        /// </summary>
        public void ChangeState(TState newState)
        {
            if (CurrentState.Equals(newState)) return;
            
            // Exit
            // 退出回调
            if (_exitActions.TryGetValue(CurrentState, out var exit))
            {
                exit();
            }
            
            // 旧状态
            var oldState = CurrentState;
            CurrentState = newState;
            
            // Enter
            // 进入回调
            if (_enterActions.TryGetValue(CurrentState, out var enter))
            {
                enter();
            }
        }
        
        /// <summary>
        /// 状态更新
        /// </summary>
        public void Update(float dt)
        {
            // 更新回调
            if (_updateActions.TryGetValue(CurrentState, out var update))
            {
                update(dt);
            }
        }
    }
    
    /// <summary>
    /// AI 决策节点接口
    /// </summary>
    public interface IAIBehavior
    {
        /// <summary>
        /// 评估优先级（越高越优先执行）
        /// </summary>
        float Evaluate(in AIContext context);
        
        /// <summary>
        /// 执行行为
        /// </summary>
        void Execute(ref AIContext context, float dt);
    }
    
    /// <summary>
    /// AI 上下文
    /// </summary>
    public struct AIContext
    {
        /// <summary>
        /// 自身 ID
        /// </summary>
        public int SelfId;
        /// <summary>
        /// 自身位置
        /// </summary>
        public Vector3 SelfPosition;
        /// <summary>
        /// 目标 ID
        /// </summary>
        public int TargetId;
        /// <summary>
        /// 目标位置
        /// </summary>
        public Vector3 TargetPosition;
        /// <summary>
        /// 目标距离
        /// </summary>
        public float DistanceToTarget;
        /// <summary>
        /// 当前血量
        /// </summary>
        public float HP;
        /// <summary>
        /// 最大血量
        /// </summary>
        public float MaxHP;
    }
    
    /// <summary>
    /// 简单 AI 控制器
    /// 优先级驱动的行为选择
    /// </summary>
    public class SimpleAIController
    {
        /// <summary>
        /// 行为列表
        /// </summary>
        private readonly System.Collections.Generic.List<IAIBehavior> _behaviors = new();
        /// <summary>
        /// 当前行为
        /// </summary>
        private IAIBehavior _currentBehavior;
        
        /// <summary>
        /// 添加行为
        /// </summary>
        public void AddBehavior(IAIBehavior behavior)
        {
            _behaviors.Add(behavior);
        }
        
        /// <summary>
        /// 更新 AI 决策
        /// </summary>
        public void Update(ref AIContext context, float dt)
        {
            // 选择最高优先级行为
            // 最大优先级
            float maxPriority = float.MinValue;
            // 最佳行为
            IAIBehavior bestBehavior = null;
            
            foreach (var behavior in _behaviors)
            {
                // 当前行为
                // 行为优先级
                float priority = behavior.Evaluate(in context);
                if (priority > maxPriority)
                {
                    maxPriority = priority;
                    bestBehavior = behavior;
                }
            }
            
            if (bestBehavior != null)
            {
                _currentBehavior = bestBehavior;
                _currentBehavior.Execute(ref context, dt);
            }
        }
    }
}
