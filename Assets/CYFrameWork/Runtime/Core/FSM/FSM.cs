// ============================================================================
// CYFramework - 有限状态机
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Infrastructure;

namespace CYFramework.Core.FSM
{
    /// <summary>
    /// 状态接口
    /// </summary>
    public interface IState<T> where T : Enum
    {
        T StateType { get; }
        void OnEnter();
        void OnUpdate(float deltaTime);
        void OnExit();
    }
    
    /// <summary>
    /// 状态基类
    /// </summary>
    public abstract class StateBase<T> : IState<T> where T : Enum
    {
        public abstract T StateType { get; }
        protected FSM<T> FSM { get; private set; }
        
        internal void SetFSM(FSM<T> fsm) => FSM = fsm;
        
        public virtual void OnEnter() { }
        public virtual void OnUpdate(float deltaTime) { }
        public virtual void OnExit() { }
        
        /// <summary>
        /// 切换到另一个状态
        /// </summary>
        protected void ChangeState(T newState) => FSM.ChangeState(newState);
    }
    
    /// <summary>
    /// 有限状态机
    /// </summary>
    public class FSM<T> where T : Enum
    {
        private readonly Dictionary<T, IState<T>> _states = new Dictionary<T, IState<T>>();
        private IState<T> _currentState;
        
        public T CurrentStateType => _currentState != null ? _currentState.StateType : default;
        public bool IsRunning => _currentState != null;
        
        /// <summary>
        /// 注册状态
        /// </summary>
        public FSM<T> AddState(IState<T> state)
        {
            if (state is StateBase<T> stateBase)
            {
                stateBase.SetFSM(this);
            }
            _states[state.StateType] = state;
            return this;
        }
        
        /// <summary>
        /// 注册多个状态
        /// </summary>
        public FSM<T> AddStates(params IState<T>[] states)
        {
            foreach (var state in states)
            {
                AddState(state);
            }
            return this;
        }
        
        /// <summary>
        /// 启动状态机
        /// </summary>
        public void Start(T initialState)
        {
            if (_states.TryGetValue(initialState, out var state))
            {
                _currentState = state;
                _currentState.OnEnter();
                CYLog.Info($"[FSM] 启动，初始状态: {initialState}");
            }
            else
            {
                CYLog.Error($"[FSM] 未找到状态: {initialState}");
            }
        }
        
        /// <summary>
        /// 切换状态
        /// </summary>
        public void ChangeState(T newState)
        {
            if (!_states.TryGetValue(newState, out var nextState))
            {
                CYLog.Error($"[FSM] 未找到状态: {newState}");
                return;
            }
            
            if (_currentState != null && _currentState.StateType.Equals(newState))
            {
                return;
            }
            
            var oldStateName = _currentState != null ? _currentState.StateType.ToString() : "None";
            _currentState?.OnExit();
            _currentState = nextState;
            _currentState.OnEnter();
            
            CYLog.Info($"[FSM] {oldStateName} → {newState}");
        }
        
        /// <summary>
        /// 更新（每帧调用）
        /// </summary>
        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(deltaTime);
        }
        
        /// <summary>
        /// 停止状态机
        /// </summary>
        public void Stop()
        {
            _currentState?.OnExit();
            _currentState = null;
        }
    }
}
