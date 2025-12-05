// ============================================================================
// CYFramework - 有限状态机
// 通用的有限状态机实现
// 
// 设计要点：
// - 泛型设计，支持任意类型的所有者
// - 支持状态栈（可选）
// - 状态可复用
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Runtime.Core.FSM
{
    /// <summary>
    /// 有限状态机
    /// </summary>
    /// <typeparam name="T">所有者类型</typeparam>
    public class StateMachine<T>
    {
        // 所有者
        private readonly T _owner;
        
        // 状态字典
        private readonly Dictionary<Type, IState<T>> _states;
        
        // 当前状态
        private IState<T> _currentState;
        
        // 状态栈（用于支持状态回退）
        private readonly Stack<IState<T>> _stateStack;
        
        // 是否使用状态栈
        private readonly bool _useStateStack;

        /// <summary>
        /// 当前状态
        /// </summary>
        public IState<T> CurrentState => _currentState;

        /// <summary>
        /// 当前状态类型
        /// </summary>
        public Type CurrentStateType => _currentState?.GetType();

        /// <summary>
        /// 创建状态机
        /// </summary>
        /// <param name="owner">所有者</param>
        /// <param name="useStateStack">是否使用状态栈</param>
        public StateMachine(T owner, bool useStateStack = false)
        {
            _owner = owner;
            _states = new Dictionary<Type, IState<T>>();
            _useStateStack = useStateStack;
            
            if (_useStateStack)
            {
                _stateStack = new Stack<IState<T>>();
            }
        }

        /// <summary>
        /// 注册状态（泛型版本）
        /// </summary>
        public void RegisterState<TState>(TState state) where TState : IState<T>
        {
            Type type = typeof(TState);
            if (_states.ContainsKey(type))
            {
                Debug.LogWarning($"[StateMachine] 状态 {type.Name} 已存在，将被覆盖");
            }
            _states[type] = state;
        }

        /// <summary>
        /// 注册状态（非泛型版本，用于反射注册）
        /// </summary>
        /// <param name="stateType">状态类型</param>
        /// <param name="state">状态实例</param>
        public void RegisterState(Type stateType, IState<T> state)
        {
            if (stateType == null || state == null) return;
            
            if (_states.ContainsKey(stateType))
            {
                Debug.LogWarning($"[StateMachine] 状态 {stateType.Name} 已存在，将被覆盖");
            }
            _states[stateType] = state;
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        public void ChangeState<TState>() where TState : IState<T>
        {
            Type type = typeof(TState);
            if (!_states.TryGetValue(type, out IState<T> newState))
            {
                Debug.LogError($"[StateMachine] 状态 {type.Name} 未注册");
                return;
            }

            // 退出当前状态
            _currentState?.OnExit(_owner);

            // 如果使用状态栈，压入当前状态
            if (_useStateStack && _currentState != null)
            {
                _stateStack.Push(_currentState);
            }

            // 进入新状态
            _currentState = newState;
            _currentState.OnEnter(_owner);
        }

        /// <summary>
        /// 直接设置状态（不触发退出/进入）
        /// </summary>
        public void SetState<TState>() where TState : IState<T>
        {
            Type type = typeof(TState);
            if (_states.TryGetValue(type, out IState<T> state))
            {
                _currentState = state;
            }
        }

        /// <summary>
        /// 返回上一个状态（仅在使用状态栈时有效）
        /// </summary>
        public bool PopState()
        {
            if (!_useStateStack || _stateStack.Count == 0)
                return false;

            _currentState?.OnExit(_owner);
            _currentState = _stateStack.Pop();
            _currentState.OnEnter(_owner);
            return true;
        }

        /// <summary>
        /// 更新当前状态
        /// </summary>
        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(_owner, deltaTime);
        }

        /// <summary>
        /// 获取状态
        /// </summary>
        public TState GetState<TState>() where TState : class, IState<T>
        {
            Type type = typeof(TState);
            if (_states.TryGetValue(type, out IState<T> state))
            {
                return state as TState;
            }
            return null;
        }

        /// <summary>
        /// 检查当前是否为指定状态
        /// </summary>
        public bool IsInState<TState>() where TState : IState<T>
        {
            return _currentState != null && _currentState.GetType() == typeof(TState);
        }

        /// <summary>
        /// 清空状态栈
        /// </summary>
        public void ClearStateStack()
        {
            _stateStack?.Clear();
        }

        /// <summary>
        /// 关闭状态机
        /// </summary>
        public void Shutdown()
        {
            _currentState?.OnExit(_owner);
            _currentState = null;
            _stateStack?.Clear();
            _states.Clear();
        }
    }
}
