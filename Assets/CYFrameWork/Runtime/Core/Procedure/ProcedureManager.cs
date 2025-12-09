// ============================================================================
// CYFramework - 流程管理器
// 类似 GameFramework 的 Procedure 系统
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Core.Procedure
{
    /// <summary>
    /// 标记流程自动注册
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoRegisterProcedureAttribute : Attribute
    {
        public string Name { get; }
        public int Order { get; }
        
        public AutoRegisterProcedureAttribute(string name = null, int order = 0)
        {
            Name = name;
            Order = order;
        }
    }

    /// <summary>
    /// 流程基类
    /// </summary>
    public abstract class ProcedureBase
    {
        protected ProcedureManager Owner { get; private set; }
        
        internal void SetOwner(ProcedureManager owner) => Owner = owner;
        
        // Internal 包装方法，供 ProcedureManager 调用
        internal void InternalOnEnter(ProcedureBase prev) => OnEnter(prev);
        internal void InternalOnUpdate(float dt) => OnUpdate(dt);
        internal void InternalOnLeave(ProcedureBase next) => OnLeave(next);
        
        /// <summary>
        /// 进入流程（子类重写）
        /// </summary>
        protected virtual void OnEnter(ProcedureBase previousProcedure) { }
        
        /// <summary>
        /// 流程轮询（子类重写）
        /// </summary>
        protected virtual void OnUpdate(float deltaTime) { }
        
        /// <summary>
        /// 离开流程（子类重写）
        /// </summary>
        protected virtual void OnLeave(ProcedureBase nextProcedure) { }
        
        /// <summary>
        /// 切换到指定流程
        /// </summary>
        protected void ChangeProcedure<T>() where T : ProcedureBase
        {
            Owner.ChangeProcedure<T>();
        }
        
        /// <summary>
        /// 切换到指定流程（带参数）
        /// </summary>
        protected void ChangeProcedure<T>(object userData) where T : ProcedureBase
        {
            Owner.ChangeProcedure<T>(userData);
        }
    }
    
    /// <summary>
    /// 可接收参数的流程基类
    /// </summary>
    public abstract class ProcedureBase<TData> : ProcedureBase
    {
        protected TData UserData { get; private set; }
        
        internal void SetUserData(object data)
        {
            if (data is TData typedData)
            {
                UserData = typedData;
            }
        }
    }
    
    /// <summary>
    /// 流程管理器
    /// 实现 IUpdateable 由框架自动调度
    /// </summary>
    public class ProcedureManager : IUpdateable
    {
        public int UpdateOrder => -50; // 优先级较高，在 Timer 之后
        private readonly Dictionary<Type, ProcedureBase> _procedures = new Dictionary<Type, ProcedureBase>();
        private readonly Dictionary<string, Type> _procedureNames = new Dictionary<string, Type>();
        private ProcedureBase _currentProcedure;
        private object _pendingUserData;
        
        public ProcedureBase CurrentProcedure => _currentProcedure;
        public string CurrentProcedureName => _currentProcedure?.GetType().Name;
        public bool IsRunning => _currentProcedure != null;
        
        /// <summary>
        /// 注册流程
        /// </summary>
        public ProcedureManager AddProcedure<T>(string name = null) where T : ProcedureBase, new()
        {
            var procedure = new T();
            procedure.SetOwner(this);
            var type = typeof(T);
            _procedures[type] = procedure;
            
            // 注册名称映射
            var procedureName = name ?? type.Name.Replace("Procedure", "");
            _procedureNames[procedureName] = type;
            return this;
        }
        
        /// <summary>
        /// 注册流程（实例）
        /// </summary>
        public ProcedureManager AddProcedure(ProcedureBase procedure, string name = null)
        {
            procedure.SetOwner(this);
            var type = procedure.GetType();
            _procedures[type] = procedure;
            
            var procedureName = name ?? type.Name.Replace("Procedure", "");
            _procedureNames[procedureName] = type;
            return this;
        }
        
        /// <summary>
        /// 自动扫描并注册所有标记了 [AutoRegisterProcedure] 的流程
        /// </summary>
        public void AutoRegisterAll(Assembly assembly = null)
        {
            assembly ??= Assembly.GetCallingAssembly();
            
            var procedureTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ProcedureBase).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttribute<AutoRegisterProcedureAttribute>() != null)
                .OrderBy(t => t.GetCustomAttribute<AutoRegisterProcedureAttribute>()?.Order ?? 0)
                .ToList();
            
            foreach (var type in procedureTypes)
            {
                var attr = type.GetCustomAttribute<AutoRegisterProcedureAttribute>();
                var procedure = (ProcedureBase)Activator.CreateInstance(type);
                procedure.SetOwner(this);
                _procedures[type] = procedure;
                
                var name = attr?.Name ?? type.Name.Replace("Procedure", "");
                _procedureNames[name] = type;
                CYLog.Debug($"[Procedure] 自动注册: {name}");
            }
        }
        
        /// <summary>
        /// 启动流程管理器
        /// </summary>
        public void Start<T>() where T : ProcedureBase
        {
            if (_procedures.TryGetValue(typeof(T), out var procedure))
            {
                _currentProcedure = procedure;
                _currentProcedure.InternalOnEnter(null);
                CYLog.Info($"[Procedure] 启动，初始流程: {typeof(T).Name}");
            }
            else
            {
                CYLog.Error($"[Procedure] 未找到流程: {typeof(T).Name}");
            }
        }
        
        /// <summary>
        /// 切换流程
        /// </summary>
        public void ChangeProcedure<T>() where T : ProcedureBase
        {
            ChangeProcedureInternal(typeof(T), null);
        }
        
        /// <summary>
        /// 切换流程（带参数）
        /// </summary>
        public void ChangeProcedure<T>(object userData) where T : ProcedureBase
        {
            ChangeProcedureInternal(typeof(T), userData);
        }
        
        /// <summary>
        /// 按名称切换流程
        /// </summary>
        public void Change(string procedureName, object userData = null)
        {
            if (_procedureNames.TryGetValue(procedureName, out var type))
            {
                ChangeProcedureInternal(type, userData);
            }
            else
            {
                CYLog.Error($"[Procedure] 未找到流程: {procedureName}");
            }
        }
        
        /// <summary>
        /// 按名称启动
        /// </summary>
        public void Start(string procedureName)
        {
            if (_procedureNames.TryGetValue(procedureName, out var type))
            {
                if (_procedures.TryGetValue(type, out var procedure))
                {
                    _currentProcedure = procedure;
                    _currentProcedure.InternalOnEnter(null);
                    CYLog.Info($"[Procedure] 启动，初始流程: {procedureName}");
                }
            }
            else
            {
                CYLog.Error($"[Procedure] 未找到流程: {procedureName}");
            }
        }
        
        private void ChangeProcedureInternal(Type procedureType, object userData)
        {
            if (!_procedures.TryGetValue(procedureType, out var nextProcedure))
            {
                CYLog.Error($"[Procedure] 未找到流程: {procedureType.Name}");
                return;
            }
            
            if (_currentProcedure == nextProcedure)
            {
                return;
            }
            
            var previousProcedure = _currentProcedure;
            var previousName = previousProcedure?.GetType().Name ?? "None";
            
            _currentProcedure?.InternalOnLeave(nextProcedure);
            
            // 设置用户数据
            if (userData != null && nextProcedure is ProcedureBase procedure)
            {
                var method = procedure.GetType().GetMethod("SetUserData", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(procedure, new[] { userData });
            }
            
            _currentProcedure = nextProcedure;
            _currentProcedure.InternalOnEnter(previousProcedure);
            
            CYLog.Info($"[Procedure] {previousName} → {procedureType.Name}");
        }
        
        /// <summary>
        /// IUpdateable 实现 - 由框架自动调用
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            _currentProcedure?.InternalOnUpdate(deltaTime);
        }
        
        /// <summary>
        /// 获取流程
        /// </summary>
        public T GetProcedure<T>() where T : ProcedureBase
        {
            return _procedures.TryGetValue(typeof(T), out var procedure) ? (T)procedure : null;
        }
        
        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            _currentProcedure?.InternalOnLeave(null);
            _currentProcedure = null;
        }
    }
}
