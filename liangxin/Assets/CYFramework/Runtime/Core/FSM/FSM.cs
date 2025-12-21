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
        /// <summary>
        /// 状态类型
        /// </summary>
        T StateType { get; }
        /// <summary>
        /// 进入状态
        /// </summary>
        void OnEnter();
        /// <summary>
        /// 状态更新
        /// </summary>
        void OnUpdate(float deltaTime);
        /// <summary>
        /// 退出状态
        /// </summary>
        void OnExit();
    }
    
    /// <summary>
    /// 状态基类
    /// </summary>
    public abstract class StateBase<T> : IState<T> where T : Enum
    {
        /// <summary>
        /// 状态类型
        /// </summary>
        public abstract T StateType { get; }
        /// <summary>
        /// 所属 FSM 引用
        /// </summary>
        protected FSM<T> FSM { get; private set; }
        
        /// <summary>
        /// 注入 FSM 引用（内部调用）
        /// </summary>
        internal void SetFSM(FSM<T> fsm) => FSM = fsm;
        
        /// <summary>
        /// 进入状态（可重写）
        /// </summary>
        public virtual void OnEnter() { }
        /// <summary>
        /// 状态更新（可重写）
        /// </summary>
        public virtual void OnUpdate(float deltaTime) { }
        /// <summary>
        /// 退出状态（可重写）
        /// </summary>
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
        /// <summary>
        /// 状态表
        /// </summary>
        private readonly Dictionary<T, IState<T>> _states = new Dictionary<T, IState<T>>();
        /// <summary>
        /// 当前状态实例
        /// </summary>
        private IState<T> _currentState;
        
        /// <summary>
        /// 当前状态类型
        /// </summary>
        public T CurrentStateType => _currentState != null ? _currentState.StateType : default;
        /// <summary>
        /// 是否处于运行状态
        /// </summary>
        public bool IsRunning => _currentState != null;
        
        /// <summary>
        /// 注册状态
        /// </summary>
        public FSM<T> AddState(IState<T> state)
        {
            // 兼容 StateBase，注入 FSM 引用
            if (state is StateBase<T> stateBase)
            {
                // 状态基类实例
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
                // 当前状态实例
                AddState(state);
            }
            return this;
        }
        
        /// <summary>
        /// 启动状态机
        /// </summary>
        public void Start(T initialState)
        {
            // 初始状态实例
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
            // 目标状态实例
            if (!_states.TryGetValue(newState, out var nextState))
            {
                CYLog.Error($"[FSM] 未找到状态: {newState}");
                return;
            }
            
            if (_currentState != null && _currentState.StateType.Equals(newState))
            {
                return;
            }
            
            // 当前状态名称
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
    /// FSM 包装器接口（避免反射）
    /// </summary>
    internal interface IFSMWrapper
    {
        /// <summary>
        /// 更新 FSM
        /// </summary>
        void Update(float deltaTime);
        /// <summary>
        /// 停止 FSM
        /// </summary>
        void Stop();
    }
    
    /// <summary>
    /// FSM 包装器（避免反射调用）
    /// </summary>
    internal class FSMWrapper<T> : IFSMWrapper where T : Enum
    {
        /// <summary>
        /// FSM 实例
        /// </summary>
        private readonly FSM<T> _fsm;
        
        /// <summary>
        /// 包装器构造
        /// </summary>
        public FSMWrapper(FSM<T> fsm) => _fsm = fsm;
        /// <summary>
        /// 公开 FSM 实例
        /// </summary>
        public FSM<T> FSM => _fsm;
        
        /// <summary>
        /// 更新 FSM
        /// </summary>
        public void Update(float deltaTime) => _fsm.Update(deltaTime);
        /// <summary>
        /// 停止 FSM
        /// </summary>
        public void Stop() => _fsm.Stop();
    }
    
    /// <summary>
    /// 有限状态机管理器
    /// 管理多个 FSM 实例，自动驱动更新
    /// </summary>
    public class FSMManager : IInitializable, IUpdateable, IDisposableEx
    {
        /// <summary>
        /// FSM 包装器表（名称 -> 包装器）
        /// </summary>
        private readonly Dictionary<string, IFSMWrapper> _fsmWrappers = new();
        /// <summary>
        /// FSM 实例表（名称 -> 实例）
        /// </summary>
        private readonly Dictionary<string, object> _fsmInstances = new();
        
        /// <summary>
        /// 初始化顺序
        /// </summary>
        public int InitOrder => 10;
        /// <summary>
        /// Update 顺序
        /// </summary>
        public int UpdateOrder => 50;
        /// <summary>
        /// 释放顺序
        /// </summary>
        public int DisposeOrder => 10;
        
        /// <summary>
        /// 初始化 FSM 管理器
        /// </summary>
        public void Initialize()
        {
            CYLog.Debug("[FSMManager] 初始化完成");
        }
        
        /// <summary>
        /// 更新所有注册的 FSM
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            foreach (var wrapper in _fsmWrappers.Values)
            {
                // 当前 FSM 包装器
                wrapper.Update(deltaTime);
            }
        }
        
        /// <summary>
        /// 释放 FSM 管理器
        /// </summary>
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
                return (_fsmWrappers[name] as FSMWrapper<T>)?.FSM;
            }
            
            // 新建 FSM 实例
            var fsm = new FSM<T>();
            // FSM 包装器
            var wrapper = new FSMWrapper<T>(fsm);
            _fsmWrappers[name] = wrapper;
            _fsmInstances[name] = fsm;
            CYLog.Debug($"[FSMManager] 创建 FSM: {name}");
            return fsm;
        }
        
        /// <summary>
        /// 获取 FSM
        /// </summary>
        public FSM<T> Get<T>(string name) where T : Enum
        {
            if (_fsmWrappers.TryGetValue(name, out var wrapper))
            {
                // FSM 包装器
                return (wrapper as FSMWrapper<T>)?.FSM;
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
        /// 销毁 FSM（无反射）
        /// </summary>
        public void Destroy(string name)
        {
            if (_fsmWrappers.TryGetValue(name, out var wrapper))
            {
                // 目标 FSM 包装器
                wrapper.Stop();
                _fsmWrappers.Remove(name);
                _fsmInstances.Remove(name);
                CYLog.Debug($"[FSMManager] 销毁 FSM: {name}");
            }
        }
        
        /// <summary>
        /// 销毁所有 FSM（无反射）
        /// </summary>
        public void DestroyAll()
        {
            foreach (var wrapper in _fsmWrappers.Values)
            {
                // 当前 FSM 包装器
                wrapper.Stop();
            }
            _fsmWrappers.Clear();
            _fsmInstances.Clear();
        }
        
        /// <summary>
        /// 获取所有 FSM 名称
        /// </summary>
        public string[] GetAllNames()
        {
            // 名称数组
            var names = new string[_fsmInstances.Count];
            _fsmInstances.Keys.CopyTo(names, 0);
            return names;
        }
    }
}
