// ============================================================================
// CYFramework - 流程管理器
// 类似 GameFramework 的 Procedure 系统
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CYFramework.Core.Config;
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
        /// <summary>
        /// 流程显示名称（为空时默认使用类名去掉 Procedure 后缀）
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 注册顺序（数值越小越靠前）
        /// </summary>
        public int Order { get; }
        
        /// <summary>
        /// 自动注册特性
        /// </summary>
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
        /// <summary>
        /// 所属流程管理器
        /// </summary>
        protected ProcedureManager Owner { get; private set; }
        
        /// <summary>
        /// 设置所属管理器（由框架内部调用）
        /// </summary>
        internal void SetOwner(ProcedureManager owner) => Owner = owner;
        
        // Internal 包装方法，供 ProcedureManager 调用
        /// <summary>
        /// 内部进入流程（由管理器调用）
        /// </summary>
        internal void InternalOnEnter(ProcedureBase prev) => OnEnter(prev);

        /// <summary>
        /// 内部流程更新（由管理器调用）
        /// </summary>
        internal void InternalOnUpdate(float dt) => OnUpdate(dt);

        /// <summary>
        /// 内部离开流程（由管理器调用）
        /// </summary>
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
    public abstract class ProcedureBase<TData> : ProcedureBase, IUserDataReceiver
    {
        /// <summary>
        /// 当前流程接收到的用户数据
        /// </summary>
        protected TData UserData { get; private set; }
        
        void IUserDataReceiver.SetUserData(object data)
        {
            if (data is TData typedData)
            {
                UserData = typedData;
            }
        }
    }
    
    /// <summary>
    /// 用户数据接收器接口（避免反射）
    /// </summary>
    internal interface IUserDataReceiver
    {
        /// <summary>
        /// 接收用户数据（由流程管理器调用）
        /// </summary>
        void SetUserData(object data);
    }
    
    /// <summary>
    /// 流程管理器
    /// 实现 IUpdateable 由框架自动调度
    /// </summary>
    public class ProcedureManager : IInitializable, IUpdateable
    {
        /// <summary>
        /// 初始化顺序（数值越小越靠前）
        /// </summary>
        public int InitOrder => -30;

        /// <summary>
        /// Update 顺序（数值越小越靠前）
        /// </summary>
        public int UpdateOrder => -50; // 优先级较高，在 Timer 之后

        // 流程实例缓存：类型 -> 实例
        private readonly Dictionary<Type, ProcedureBase> _procedures = new Dictionary<Type, ProcedureBase>();

        // 名称 -> 类型映射
        private readonly Dictionary<string, Type> _procedureNames = new Dictionary<string, Type>();

        // 类型 -> 名称映射
        private readonly Dictionary<Type, string> _procedureTypeToName = new Dictionary<Type, string>();

        // 记录注册顺序：用于 StartEntry 在未配置入口流程时保持稳定行为（不要依赖 Dictionary 枚举顺序）。
        private readonly List<Type> _registeredOrder = new List<Type>(16);

        // 当前流程实例
        private ProcedureBase _currentProcedure;

        // 挂起的用户数据（预留扩展）
        private object _pendingUserData;

        // 是否已初始化
        private bool _initialized;

        /// <summary>
        /// 尝试从流程注册表资源中注册流程
        /// </summary>
        private bool TryRegisterFromRegistry()
        {
            var registry = Resources.Load<ProcedureRegistryAsset>("CYFramework/ProcedureRegistry"); // 注册表资源
            if (registry == null || registry.Procedures == null || registry.Procedures.Count == 0)
            {
                return false;
            }

            var entries = registry.Procedures.OrderBy(e => e.Order).ToList(); // 排序后的注册条目
            int registered = 0; // 已注册数量
            for (int i = 0; i < entries.Count; i++) // i 为索引
            {
                var entry = entries[i]; // 当前条目
                if (string.IsNullOrEmpty(entry.TypeName))
                {
                    continue;
                }

                var type = Type.GetType(entry.TypeName); // 目标流程类型
                if (type == null)
                {
                    CYLog.Warning($"[ProcedureManager] 注册表类型不存在（可能被裁剪/类型名变更）: {entry.TypeName}。请在 Editor 执行菜单 CYFramework/Generate Procedure Registry 重新生成，并确保已生成/提交 link.xml");
                    continue;
                }

                if (!typeof(ProcedureBase).IsAssignableFrom(type) || type.IsAbstract)
                {
                    CYLog.Warning($"[ProcedureManager] 注册表类型不是有效流程: {type.FullName}");
                    continue;
                }

                var procedure = (ProcedureBase)Activator.CreateInstance(type); // 流程实例
                procedure.SetOwner(this);
                _procedures[type] = procedure;

                var name = string.IsNullOrEmpty(entry.Name) ? type.Name.Replace("Procedure", "") : entry.Name; // 注册名
                _procedureNames[name] = type;
                _procedureTypeToName[type] = name;
                if (!_registeredOrder.Contains(type))
                {
                    _registeredOrder.Add(type);
                }
                registered++;
            }

            if (registered > 0)
            {
                CYLog.Debug($"[ProcedureManager] 从流程注册表加载完成: {registered} 个");
                return true;
            }
            return false;
        }
        
        // 配置
        /// <summary>
        /// 入口流程名称（为空时按注册顺序选择）
        /// </summary>
        private string _entryProcedure = "";
        
        // 自动注册开关
        // ❗ WebGL/微信平台不支持 AppDomain.GetAssemblies()，默认禁用自动扫描
        // 建议使用显式注册: AddProcedure<T>() 或传入指定 Assembly
#if UNITY_WEBGL || CY_WECHAT
        /// <summary>
        /// 是否启用自动注册（受平台限制）
        /// </summary>
        private bool _autoRegister = false;
#else
        /// <summary>
        /// 是否启用自动注册（受平台限制）
        /// </summary>
        private bool _autoRegister = true;
#endif
        
        /// <summary>
        /// 当前流程实例
        /// </summary>
        public ProcedureBase CurrentProcedure => _currentProcedure;

        /// <summary>
        /// 当前流程类型名（不含注册别名）
        /// </summary>
        public string CurrentProcedureName => _currentProcedure?.GetType().Name;

        /// <summary>
        /// 当前流程（兼容别名：与文档/旧用法对齐）。
        /// </summary>
        public ProcedureBase Current => _currentProcedure;

        /// <summary>
        /// 当前流程名称（优先返回注册名；若未注册名则回退到类型名）。
        /// </summary>
        public string CurrentName
        {
            get
            {
                if (_currentProcedure == null) return null;
                var type = _currentProcedure.GetType(); // 当前流程类型
                return _procedureTypeToName.TryGetValue(type, out var name) ? name : type.Name; // name 为注册名
            }
        }
        /// <summary>
        /// 是否正在运行流程
        /// </summary>
        public bool IsRunning => _currentProcedure != null;

        /// <summary>
        /// 是否处于指定流程（按类型）
        /// </summary>
        /// <typeparam name="T">流程类型</typeparam>
        /// <returns>是否当前正是该流程</returns>
        public bool IsCurrent<T>() where T : ProcedureBase
        {
            return _currentProcedure != null && _currentProcedure.GetType() == typeof(T);
        }

        /// <summary>
        /// 是否处于指定流程（按名称，名称来自 Start/Change 的 name）
        /// </summary>
        public bool IsCurrent(string procedureName)
        {
            if (string.IsNullOrEmpty(procedureName) || _currentProcedure == null)
            {
                return false;
            }

            if (_procedureNames.TryGetValue(procedureName, out var type)) // type 为注册名对应的流程类型
            {
                return _currentProcedure.GetType() == type;
            }

            // 兼容直接用类名判断
            return string.Equals(_currentProcedure.GetType().Name, procedureName, StringComparison.Ordinal);
        }

        /// <summary>
        /// 尝试按名称切换流程（不存在则返回 false）
        /// </summary>
        public bool TryChange(string procedureName, object userData = null)
        {
            if (string.IsNullOrEmpty(procedureName))
            {
                return false;
            }

            if (!_procedureNames.TryGetValue(procedureName, out var type)) // type 为待切换的流程类型
            {
                return false;
            }

            Change(procedureName, userData);
            return true;
        }

        /// <summary>
        /// 如果当前不是指定流程才切换（避免重复触发 OnLeave/OnEnter）
        /// </summary>
        /// <typeparam name="T">目标流程类型</typeparam>
        /// <param name="userData">用户数据</param>
        /// <returns>是否发生了切换</returns>
        public bool ChangeIfNot<T>(object userData = null) where T : ProcedureBase
        {
            if (IsCurrent<T>())
            {
                return false;
            }

            ChangeProcedure<T>(userData);
            return true;
        }
        
        /// <summary>
        /// 初始化流程管理器
        /// </summary>
        public void Initialize()
        {
            // 允许被多次调用（例如：CY.Procedure 在 CYBootstrap.InitializeAll 前被访问并提前创建）
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance; // 配置中心实例
            if (configurator != null)
            {
                var config = configurator.GetConfig<ProcedureManagerConfig>(); // 流程配置
                if (config != null)
                {
                    _entryProcedure = config.EntryProcedure;
                    _autoRegister = config.AutoRegisterProcedures;
                    CYLog.Debug("[ProcedureManager] 使用 CYConfigurator 配置");
                }
            }
            
            // 自动注册流程
            // ❗ WebGL/微信平台不支持 AppDomain，默认禁用自动扫描
            // 若需要在这些平台使用，请通过 AddProcedure<T>() 显式注册
            // 📌 说明：平台限制原因（见文档 6.1），WebGL/微信不支持 AppDomain.GetAssemblies。
            // 推荐做法：
            // 1) 运行期显式 AddProcedure<T>()；
            // 2) 或者在 Editor 生成流程注册表，运行期读取，避免反射扫描卡顿/GC。
            if (_autoRegister)
            {
                if (!TryRegisterFromRegistry())
                {
                    AutoRegisterAll();
                }
            }
            
            CYLog.Debug("[ProcedureManager] 初始化完成");
        }
        
        /// <summary>
        /// 注册流程
        /// </summary>
        public ProcedureManager AddProcedure<T>(string name = null) where T : ProcedureBase, new()
        {
            var procedure = new T(); // 流程实例
            procedure.SetOwner(this);
            var type = typeof(T); // 流程类型
            _procedures[type] = procedure;
            
            // 注册名称映射
            var procedureName = name ?? type.Name.Replace("Procedure", ""); // 注册名
            _procedureNames[procedureName] = type;
            _procedureTypeToName[type] = procedureName;
            if (!_registeredOrder.Contains(type))
            {
                _registeredOrder.Add(type);
            }
            return this;
        }
        
        /// <summary>
        /// 注册流程（实例）
        /// </summary>
        public ProcedureManager AddProcedure(ProcedureBase procedure, string name = null)
        {
            procedure.SetOwner(this);
            var type = procedure.GetType(); // 流程类型
            _procedures[type] = procedure;
            
            var procedureName = name ?? type.Name.Replace("Procedure", ""); // 注册名
            _procedureNames[procedureName] = type;
            _procedureTypeToName[type] = procedureName;
            if (!_registeredOrder.Contains(type))
            {
                _registeredOrder.Add(type);
            }
            return this;
        }
        
        /// <summary>
        /// 自动扫描并注册所有标记了 [AutoRegisterProcedure] 的流程
        /// ❗ 注意：WebGL/微信平台不支持 AppDomain.GetAssemblies()，必须传入指定 Assembly
        /// 推荐做法：
        /// 1) 显式注册: AddProcedure&lt;MyProcedure&gt;()
        /// 2) 传入指定程序集: AutoRegisterAll(typeof(MyProcedure).Assembly)
        /// 3) Editor 生成注册表（已实现：使用菜单 CYFramework/Generate Procedure Registry）
        /// </summary>
        /// <param name="assembly">指定的程序集，不传则扫描所有程序集（仅 Native 端支持）</param>
        public void AutoRegisterAll(Assembly assembly = null)
        {
#if UNITY_WEBGL || CY_WECHAT
            // WebGL/微信平台不支持 AppDomain，必须传入指定程序集
            if (assembly == null)
            {
                CYLog.Warning("[ProcedureManager] WebGL/微信平台不支持自动扫描程序集，请使用流程注册表（ProcedureRegistryAsset）或 AddProcedure<T>() 显式注册或传入指定 Assembly");
                return;
            }
#endif
            
            // 如果没有指定程序集，扫描所有已加载的程序集（仅 Native 端支持）
            var assemblies = assembly != null 
                ? new[] { assembly } 
                : AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.FullName.StartsWith("Unity") && 
                                !a.FullName.StartsWith("System") && 
                                !a.FullName.StartsWith("mscorlib") &&
                                !a.FullName.StartsWith("netstandard"))
                    .ToArray(); // 参与扫描的程序集
            
            // 扫描到的流程类型
            var procedureTypes = assemblies
                .SelectMany(a => {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ProcedureBase).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttribute<AutoRegisterProcedureAttribute>() != null)
                .OrderBy(t => t.GetCustomAttribute<AutoRegisterProcedureAttribute>()?.Order ?? 0)
                .ToList(); // 待注册流程类型列表
            
            foreach (var type in procedureTypes)
            {
                // type 为待注册流程类型
                var attr = type.GetCustomAttribute<AutoRegisterProcedureAttribute>(); // 注册特性
                var procedure = (ProcedureBase)Activator.CreateInstance(type); // 流程实例
                procedure.SetOwner(this);
                _procedures[type] = procedure;
                
                var name = attr?.Name ?? type.Name.Replace("Procedure", ""); // 注册名
                _procedureNames[name] = type;
                _procedureTypeToName[type] = name;
                if (!_registeredOrder.Contains(type))
                {
                    _registeredOrder.Add(type);
                }
                CYLog.Debug($"[Procedure] 自动注册: {name}");
            }
        }
        
        /// <summary>
        /// 启动流程管理器并进入指定流程
        /// </summary>
        /// <typeparam name="T">初始流程类型</typeparam>
        public void Start<T>() where T : ProcedureBase
        {
            if (_procedures.TryGetValue(typeof(T), out var procedure)) // procedure 为目标流程实例
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
        /// <typeparam name="T">目标流程类型</typeparam>
        /// <param name="userData">传递给目标流程的数据（目标流程需实现 ProcedureBase&lt;TData&gt; 接收）</param>
        public void ChangeProcedure<T>(object userData) where T : ProcedureBase
        {
            ChangeProcedureInternal(typeof(T), userData);
        }
        
        /// <summary>
        /// 按名称切换流程
        /// </summary>
        public void Change(string procedureName, object userData = null)
        {
            if (_procedureNames.TryGetValue(procedureName, out var type)) // type 为注册名对应的流程类型
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
            if (_procedureNames.TryGetValue(procedureName, out var type)) // type 为注册名对应的流程类型
            {
                if (_procedures.TryGetValue(type, out var procedure)) // procedure 为目标流程实例
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
        
        /// <summary>
        /// 启动入口流程（使用配置的入口流程，如果未配置则使用第一个注册的流程）
        /// </summary>
        public void StartEntry()
        {
            // 如果配置了入口流程名称，使用它
            if (!string.IsNullOrEmpty(_entryProcedure))
            {
                Start(_entryProcedure);
                return;
            }
            
            // 否则使用“注册顺序中的第一个流程”（稳定且可控）
            if (_registeredOrder.Count > 0)
            {
                var type = _registeredOrder[0]; // 第一个注册的流程类型
                if (_procedures.TryGetValue(type, out var procedure)) // procedure 为入口流程实例
                {
                    _currentProcedure = procedure;
                    _currentProcedure.InternalOnEnter(null);
                    CYLog.Info($"[Procedure] 启动，初始流程: {CurrentName}");
                    return;
                }
            }
            else
            {
                CYLog.Warning("[Procedure] 没有注册任何流程，无法启动");
            }
        }
        
        /// <summary>
        /// 内部切换流程
        /// </summary>
        private void ChangeProcedureInternal(Type procedureType, object userData)
        {
            if (!_procedures.TryGetValue(procedureType, out var nextProcedure)) // nextProcedure 为目标流程实例
            {
                CYLog.Error($"[Procedure] 未找到流程: {procedureType.Name}");
                return;
            }
            
            if (_currentProcedure == nextProcedure)
            {
                return;
            }
            
            var previousProcedure = _currentProcedure; // 旧流程
            var previousName = previousProcedure?.GetType().Name ?? "None"; // 旧流程名
            
            _currentProcedure?.InternalOnLeave(nextProcedure);
            
            // 设置用户数据（使用接口避免反射）
            if (userData != null && nextProcedure is IUserDataReceiver receiver)
            {
                // receiver 为可接收用户数据的流程
                receiver.SetUserData(userData);
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
            return _procedures.TryGetValue(typeof(T), out var procedure) ? (T)procedure : null; // procedure 为目标流程实例
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
