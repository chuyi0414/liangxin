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
        public InputType Type;
        public float Timestamp;
        public Vector2 Direction;
        public int TargetId;
    }
    
    /// <summary>
    /// 输入类型
    /// </summary>
    public enum InputType
    {
        None,
        Move,
        Jump,
        Attack,
        Skill,
        Interact
    }
    
    /// <summary>
    /// 状态机接口
    /// 文档：OOP与Hybrid共用的纯逻辑(状态机/AI)
    /// </summary>
    public interface IStateMachine<TState> where TState : System.Enum
    {
        TState CurrentState { get; }
        void ChangeState(TState newState);
        void Update(float dt);
    }
    
    /// <summary>
    /// 简单有限状态机实现
    /// </summary>
    public class SimpleFSM<TState> : IStateMachine<TState> where TState : System.Enum
    {
        public TState CurrentState { get; private set; }
        
        private readonly System.Collections.Generic.Dictionary<TState, System.Action<float>> _updateActions = new();
        private readonly System.Collections.Generic.Dictionary<TState, System.Action> _enterActions = new();
        private readonly System.Collections.Generic.Dictionary<TState, System.Action> _exitActions = new();
        
        public SimpleFSM(TState initialState)
        {
            CurrentState = initialState;
        }
        
        public void RegisterState(TState state, System.Action onEnter = null, System.Action<float> onUpdate = null, System.Action onExit = null)
        {
            if (onEnter != null) _enterActions[state] = onEnter;
            if (onUpdate != null) _updateActions[state] = onUpdate;
            if (onExit != null) _exitActions[state] = onExit;
        }
        
        public void ChangeState(TState newState)
        {
            if (CurrentState.Equals(newState)) return;
            
            // Exit
            if (_exitActions.TryGetValue(CurrentState, out var exit))
            {
                exit();
            }
            
            var oldState = CurrentState;
            CurrentState = newState;
            
            // Enter
            if (_enterActions.TryGetValue(CurrentState, out var enter))
            {
                enter();
            }
        }
        
        public void Update(float dt)
        {
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
        public int SelfId;
        public Vector3 SelfPosition;
        public int TargetId;
        public Vector3 TargetPosition;
        public float DistanceToTarget;
        public float HP;
        public float MaxHP;
    }
    
    /// <summary>
    /// 简单 AI 控制器
    /// 优先级驱动的行为选择
    /// </summary>
    public class SimpleAIController
    {
        private readonly System.Collections.Generic.List<IAIBehavior> _behaviors = new();
        private IAIBehavior _currentBehavior;
        
        public void AddBehavior(IAIBehavior behavior)
        {
            _behaviors.Add(behavior);
        }
        
        public void Update(ref AIContext context, float dt)
        {
            // 选择最高优先级行为
            float maxPriority = float.MinValue;
            IAIBehavior bestBehavior = null;
            
            foreach (var behavior in _behaviors)
            {
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
