// ============================================================================
// CYFramework 2.2 - ServiceLocator 服务定位器
// 文档位置：3.1.2 基础设施
// 功能：统一管理生命周期，支持三种作用域、循环依赖检测、懒加载
// ============================================================================

using System;
using System.Collections.Generic;

namespace CYFramework.Infrastructure
{
    /// <summary>
    /// 服务注册信息
    /// </summary>
    internal class ServiceRegistration
    {
        /// <summary>
        /// 服务接口类型
        /// </summary>
        public Type ServiceType;
        /// <summary>
        /// 实现类型
        /// </summary>
        public Type ImplementationType;
        /// <summary>
        /// 生命周期作用域
        /// </summary>
        public ServiceScope Scope;
        /// <summary>
        /// 实例工厂
        /// </summary>
        public Func<object> Factory;
        /// <summary>
        /// 缓存实例
        /// </summary>
        public object Instance;
        /// <summary>
        /// 是否懒加载
        /// </summary>
        public bool IsLazy;
    }
    
    /// <summary>
    /// ServiceLocator 服务定位器
    /// 统一管理服务生命周期，支持依赖注入
    /// </summary>
    public static class ServiceLocator
    {
        // 服务注册表
        private static readonly Dictionary<Type, ServiceRegistration> _registrations = new();
        
        // 场景级服务（场景切换时清理）
        private static readonly HashSet<Type> _scopedServices = new();
        
        // 初始化顺序缓存
        private static List<Type> _initOrder;
        
        // 是否已初始化
        private static bool _initialized;
        
        // 循环依赖检测栈
        private static readonly HashSet<Type> _resolvingStack = new();
        
        /// <summary>
        /// 服务注册事件
        /// </summary>
        public static event Action<object> OnServiceRegistered;
        
        /// <summary>
        /// 服务注销事件
        /// </summary>
        public static event Action<object> OnServiceUnregistered;
        
        #region 注册 API

        
        /// <summary>
        /// 注册服务（单例）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <typeparam name="TImplementation">服务实现类型</typeparam>
        /// <param name="scope">生命周期作用域（默认 Singleton）</param>
        public static void Register<TService, TImplementation>(ServiceScope scope = ServiceScope.Singleton)
            where TImplementation : TService, new()
        {
            Register<TService>(() => new TImplementation(), scope);
        }
        
        /// <summary>
        /// 注册服务（带工厂）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="factory">创建实例的工厂方法</param>
        /// <param name="scope">生命周期作用域</param>
        public static void Register<TService>(Func<TService> factory, ServiceScope scope = ServiceScope.Singleton)
        {
            // 服务类型
            var serviceType = typeof(TService);
            
            if (_registrations.ContainsKey(serviceType))
            {
                CYLog.Warning($"[ServiceLocator] 服务已注册，将覆盖: {serviceType.Name}");
            }
            
            _registrations[serviceType] = new ServiceRegistration
            {
                ServiceType = serviceType,
                ImplementationType = typeof(TService),
                Scope = scope,
                Factory = () => factory(),
                Instance = null,
                IsLazy = false
            };
            
            if (scope == ServiceScope.Scoped)
            {
                _scopedServices.Add(serviceType);
            }
            
            // 重置初始化顺序缓存
            _initOrder = null;
        }
        
        /// <summary>
        /// 注册已存在的实例
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="instance">服务实例</param>
        public static void RegisterInstance<TService>(TService instance)
        {
            // 服务类型
            var serviceType = typeof(TService);
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance), $"[ServiceLocator] RegisterInstance 失败：{serviceType.Name} instance 为空");
            }
            
            _registrations[serviceType] = new ServiceRegistration
            {
                ServiceType = serviceType,
                ImplementationType = instance.GetType(),
                Scope = ServiceScope.Singleton,
                Factory = null,
                Instance = instance,
                IsLazy = false
            };

            // 如果框架已完成 InitializeAll，后续动态注册的实例也必须补齐 Initialize，保持行为一致。
            if (_initialized)
            {
                InitializeInstanceIfNeeded(instance);
            }

            OnServiceRegistered?.Invoke(instance);
        }
        
        /// <summary>
        /// 注册懒加载服务
        /// </summary>
        public static void RegisterLazy<TService, TImplementation>(ServiceScope scope = ServiceScope.Singleton)
            where TImplementation : TService, new()
        {
            // 服务类型
            var serviceType = typeof(TService);
            
            if (_registrations.ContainsKey(serviceType))
            {
                CYLog.Warning($"[ServiceLocator] 服务已注册，将覆盖: {serviceType.Name}");
            }
            
            _registrations[serviceType] = new ServiceRegistration
            {
                ServiceType = serviceType,
                ImplementationType = typeof(TImplementation),
                Scope = scope,
                Factory = () => new TImplementation(),
                Instance = null,
                IsLazy = true
            };
            
            if (scope == ServiceScope.Scoped)
            {
                _scopedServices.Add(serviceType);
            }
            
            _initOrder = null;
        }
        
        /// <summary>
        /// 注销服务
        /// </summary>
        public static void Unregister<TService>()
        {
            // 服务类型
            var type = typeof(TService);
            if (_registrations.TryGetValue(type, out var reg)) // reg 为服务注册信息
            {
                if (reg.Instance != null)
                {
                    OnServiceUnregistered?.Invoke(reg.Instance);
                }
                
                _registrations.Remove(type);
                if (_scopedServices.Contains(type))
                {
                    _scopedServices.Remove(type);
                }
                CYLog.Debug($"[ServiceLocator] 服务已注销: {type.Name}");
            }
        }
        
        #endregion
        
        #region 解析 API
        
        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例</returns>
        /// <exception cref="InvalidOperationException">如果服务未注册</exception>
        public static T Get<T>()
        {
            return (T)Get(typeof(T));
        }
        
        /// <summary>
        /// 尝试获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="service">输出服务实例</param>
        /// <returns>是否获取成功</returns>
        public static bool TryGet<T>(out T service)
        {
            if (_registrations.TryGetValue(typeof(T), out var reg)) // reg 为服务注册信息
            {
                service = (T)ResolveInstance(reg);
                return true;
            }
            service = default;
            return false;
        }
        
        /// <summary>
        /// 获取服务（按类型）
        /// </summary>
        public static object Get(Type serviceType)
        {
            if (!_registrations.TryGetValue(serviceType, out var registration)) // registration 为服务注册信息
            {
                throw new InvalidOperationException($"[ServiceLocator] 服务未注册: {serviceType.Name}");
            }
            
            return ResolveInstance(registration);
        }
        
        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public static bool IsRegistered<T>()
        {
            return _registrations.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// 获取所有已实例化的服务
        /// 用于生命周期注册
        /// </summary>
        public static IEnumerable<object> GetAllInstances()
        {
            // 实例列表
            var instances = new List<object>();
            foreach (var reg in _registrations.Values) // reg 为服务注册信息
            {
                if (reg.Instance != null)
                {
                    instances.Add(reg.Instance);
                }
            }
            return instances;
        }
        
        #endregion
        
        #region 生命周期管理
        
        /// <summary>
        /// 初始化所有非懒加载服务
        /// 按依赖关系拓扑排序后初始化
        /// </summary>
        public static void InitializeAll()
        {
            if (_initialized)
            {
                CYLog.Warning("[ServiceLocator] 已初始化，跳过重复调用");
                return;
            }
            
            // 构建初始化顺序
            BuildInitOrder();
            
            // 先创建实例，再按 InitOrder 排序初始化（当前实现不做依赖拓扑排序）
            // 待初始化列表
            var initializables = new List<(int order, IInitializable instance)>();
            
            foreach (var type in _initOrder) // type 为服务类型
            {
                if (!_registrations.TryGetValue(type, out var reg)) continue; // reg 为服务注册信息
                if (reg.IsLazy) continue; // 跳过懒加载
                
                // 服务实例
                var instance = ResolveInstance(reg);
                
                if (instance is IInitializable initializable) // initializable 为可初始化实例
                {
                    initializables.Add((initializable.InitOrder, initializable));
                }
            }
            
            // 按优先级排序后初始化
            initializables.Sort((a, b) => a.order.CompareTo(b.order));
            
            foreach (var (_, initializable) in initializables) // initializable 为可初始化实例
            {
                try
                {
                    initializable.Initialize();
                    CYLog.Debug($"[ServiceLocator] 初始化完成: {initializable.GetType().Name}");
                }
                catch (Exception ex)
                {
                    // ex 为初始化异常
                    CYLog.Error($"[ServiceLocator] 初始化失败: {initializable.GetType().Name}", ex);
                    throw;
                }
            }
            
            _initialized = true;
            CYLog.Info($"[ServiceLocator] 所有服务初始化完成，共 {initializables.Count} 个");
        }
        
        /// <summary>
        /// 清理场景级服务
        /// </summary>
        public static void ClearScoped()
        {
            foreach (var type in _scopedServices) // type 为服务类型
            {
                if (_registrations.TryGetValue(type, out var reg)) // reg 为服务注册信息
                {
                    if (reg.Instance != null)
                    {
                        // 先通知外部解绑（例如 CYBootstrap 的生命周期列表移除），再执行 Dispose。
                        OnServiceUnregistered?.Invoke(reg.Instance);
                        DisposeInstance(reg);
                        reg.Instance = null;
                    }
                }
            }
            
            CYLog.Debug($"[ServiceLocator] 清理场景级服务，共 {_scopedServices.Count} 个");
        }
        
        /// <summary>
        /// 销毁所有服务
        /// </summary>
        public static void DisposeAll()
        {
            // 按销毁优先级排序
            // 待销毁列表
            var disposables = new List<(int order, IDisposable instance, Type type)>();
            
            foreach (var kvp in _registrations) // kvp 为服务注册表项
            {
                if (kvp.Value.Instance is IDisposable disposable) // disposable 为可销毁实例
                {
                    // 销毁优先级
                    int order = (disposable is IDisposableEx disposableEx) ? disposableEx.DisposeOrder : 0; // disposableEx 为扩展销毁接口
                    disposables.Add((order, disposable, kvp.Key));
                }
            }
            
            // 优先级越大越先销毁
            disposables.Sort((a, b) => b.order.CompareTo(a.order));
            
            foreach (var (_, disposable, type) in disposables) // disposable/type 为待销毁实例与类型
            {
                try
                {
                    disposable.Dispose();
                    CYLog.Debug($"[ServiceLocator] 销毁完成: {type.Name}");
                }
                catch (Exception ex)
                {
                    // ex 为销毁异常
                    CYLog.Error($"[ServiceLocator] 销毁失败: {type.Name}", ex);
                }
            }
            
            _registrations.Clear();
            _scopedServices.Clear();
            _initOrder = null;
            _initialized = false;
            
            CYLog.Info("[ServiceLocator] 所有服务已销毁");
        }
        
        /// <summary>
        /// 清理所有服务（用于测试）
        /// 不调用 Dispose，直接清空注册表
        /// </summary>
        public static void ClearAll()
        {
            _registrations.Clear();
            _scopedServices.Clear();
            _initOrder = null;
            _initialized = false;
            _resolvingStack.Clear();
        }
        
        #endregion
        
        #region 私有方法
        
        /// <summary>
        /// 解析实例
        /// 如果框架已初始化，新创建的实例会自动调用 Initialize
        /// </summary>
        private static object ResolveInstance(ServiceRegistration registration)
        {
            // 循环依赖检测
            if (_resolvingStack.Contains(registration.ServiceType))
            {
                throw new InvalidOperationException(
                    $"[ServiceLocator] 检测到循环依赖: {string.Join(" -> ", _resolvingStack)} -> {registration.ServiceType.Name}");
            }
            
            switch (registration.Scope)
            {
                case ServiceScope.Singleton:
                case ServiceScope.Scoped:
                    if (registration.Instance != null)
                        return registration.Instance;
                    
                    _resolvingStack.Add(registration.ServiceType);
                    try
                    {
                        registration.Instance = registration.Factory();
                        // 如果框架已初始化，自动初始化新创建的实例（Lazy/Scoped 重建场景）
                        if (_initialized)
                        {
                            InitializeInstanceIfNeeded(registration.Instance);
                        }
                        // 触发注册事件（主要用于生命周期关联）
                        OnServiceRegistered?.Invoke(registration.Instance);
                    }
                    finally
                    {
                        _resolvingStack.Remove(registration.ServiceType);
                    }
                    return registration.Instance;
                    
                case ServiceScope.Transient:
                    _resolvingStack.Add(registration.ServiceType);
                    try
                    {
                        // 新建实例
                        var instance = registration.Factory();
                        // Transient 实例也需要初始化
                        if (_initialized)
                        {
                            InitializeInstanceIfNeeded(instance);
                        }

                        // Transient 不参与全局生命周期管理：
                        // - 它可能被高频创建，触发 OnServiceRegistered 会导致生命周期列表无限增长（严重泄漏）。
                        // - 如确需被框架调度，请使用 Singleton/Scoped，或由业务自行管理生命周期。
                        return instance;
                    }
                    finally
                    {
                        _resolvingStack.Remove(registration.ServiceType);
                    }
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        /// <summary>
        /// 如果实例实现了 IInitializable，自动调用 Initialize
        /// </summary>
        private static void InitializeInstanceIfNeeded(object instance)
        {
            if (instance is IInitializable initializable) // initializable 为可初始化实例
            {
                try
                {
                    initializable.Initialize();
                    CYLog.Debug($"[ServiceLocator] 延迟初始化完成: {instance.GetType().Name}");
                }
                catch (Exception ex)
                {
                    // ex 为初始化异常
                    CYLog.Error($"[ServiceLocator] 延迟初始化失败: {instance.GetType().Name}", ex);
                    throw;
                }
            }
        }
        
        /// <summary>
        /// 销毁实例
        /// </summary>
        private static void DisposeInstance(ServiceRegistration registration)
        {
            if (registration.Instance is IDisposable disposable) // disposable 为可销毁实例
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    // ex 为销毁异常
                    CYLog.Error($"[ServiceLocator] 销毁实例失败: {registration.ServiceType.Name}", ex);
                }
            }
        }
        
        /// <summary>
        /// 构建初始化顺序
        /// 说明：
        /// - 初始化顺序主要依赖 IInitializable.InitOrder
        /// - 若需要显式依赖声明，可在未来扩展 Attribute 方案
        /// </summary>
        private static void BuildInitOrder()
        {
            if (_initOrder != null) return;

            // 目前不做依赖拓扑排序，直接按注册集合生成列表
            _initOrder = new List<Type>(_registrations.Keys);
        }
        
        #endregion
    }
}
