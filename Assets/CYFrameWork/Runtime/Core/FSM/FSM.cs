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
    
    /// <summary>
    /// 有限状态机管理器
    /// 管理多个 FSM 实例
    /// </summary>
    public class FSMManager : IInitializable, IUpdateable, IDisposableEx
    {
        private readonly Dictionary<string, object> _fsmInstances = new();
        
        public int InitOrder => 10;
        public int UpdateOrder => 50;
        public int DisposeOrder => 10;
        
        public void Initialize()
        {
            CYLog.Debug("[FSMManager] 初始化完成");
        }
        
        public void OnUpdate(float deltaTime)
        {
            // 更新所有注册的 FSM
            // 注意：实际使用中可能需要手动调用各 FSM 的 Update
        }
        
        public void Dispose()
        {
            DestroyAll();
            CYLog.Debug("[FSMManager] 已销毁");
        }
        
        /// <summary>
        /// 创建并注册 FSM
        /// </summary>
        /// <typeparam name="T">状态枚举类型</typeparam>
        /// <param name="name">FSM 名称</param>
        /// <returns>新建的 FSM 实例</returns>
        public FSM<T> Create<T>(string name) where T : Enum
        {
            if (_fsmInstances.ContainsKey(name))
            {
                CYLog.Warning($"[FSMManager] FSM 已存在: {name}");
                return _fsmInstances[name] as FSM<T>;
            }
            
            var fsm = new FSM<T>();
            _fsmInstances[name] = fsm;
            CYLog.Debug($"[FSMManager] 创建 FSM: {name}");
            return fsm;
        }
        
        /// <summary>
        /// 获取 FSM
        /// </summary>
        public FSM<T> Get<T>(string name) where T : Enum
        {
            if (_fsmInstances.TryGetValue(name, out var fsm))
            {
                return fsm as FSM<T>;
            }
            
            CYLog.Warning($"[FSMManager] 未找到 FSM: {name}");
            return null;
        }
        
        /// <summary>
        /// 获取或创建 FSM
        /// </summary>
        public FSM<T> GetOrCreate<T>(string name) where T : Enum
        {
            return Get<T>(name) ?? Create<T>(name);
        }
        
        /// <summary>
        /// 检查 FSM 是否存在
        /// </summary>
        public bool Has(string name)
        {
            return _fsmInstances.ContainsKey(name);
        }
        
        /// <summary>
        /// 销毁 FSM
        /// </summary>
        public void Destroy(string name)
        {
            if (_fsmInstances.TryGetValue(name, out var fsm))
            {
                // 尝试停止 FSM
                var stopMethod = fsm.GetType().GetMethod("Stop");
                stopMethod?.Invoke(fsm, null);
                
                _fsmInstances.Remove(name);
                CYLog.Debug($"[FSMManager] 销毁 FSM: {name}");
            }
        }
        
        /// <summary>
        /// 销毁所有 FSM
        /// </summary>
        public void DestroyAll()
        {
            foreach (var kvp in _fsmInstances)
            {
                var stopMethod = kvp.Value.GetType().GetMethod("Stop");
                stopMethod?.Invoke(kvp.Value, null);
            }
            _fsmInstances.Clear();
        }
        
        /// <summary>
        /// 获取所有 FSM 名称
        /// </summary>
        public string[] GetAllNames()
        {
            var names = new string[_fsmInstances.Count];
            _fsmInstances.Keys.CopyTo(names, 0);
            return names;
        }
    }
}
