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
        public Type ServiceType;
        public Type ImplementationType;
        public ServiceScope Scope;
        public Func<object> Factory;
        public object Instance;
        public bool IsLazy;
        public string[] Dependencies;
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
        
        #region 注册 API
        
        /// <summary>
        /// 注册服务（单例）
        /// </summary>
        public static void Register<TService, TImplementation>(ServiceScope scope = ServiceScope.Singleton)
            where TImplementation : TService, new()
        {
            Register<TService>(() => new TImplementation(), scope);
        }
        
        /// <summary>
        /// 注册服务（带工厂）
        /// </summary>
        public static void Register<TService>(Func<TService> factory, ServiceScope scope = ServiceScope.Singleton)
        {
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
        public static void RegisterInstance<TService>(TService instance)
        {
            var serviceType = typeof(TService);
            
            _registrations[serviceType] = new ServiceRegistration
            {
                ServiceType = serviceType,
                ImplementationType = instance.GetType(),
                Scope = ServiceScope.Singleton,
                Factory = null,
                Instance = instance,
                IsLazy = false
            };
        }
        
        /// <summary>
        /// 注册懒加载服务
        /// </summary>
        public static void RegisterLazy<TService, TImplementation>(ServiceScope scope = ServiceScope.Singleton)
            where TImplementation : TService, new()
        {
            var serviceType = typeof(TService);
            
            _registrations[serviceType] = new ServiceRegistration
            {
                ServiceType = serviceType,
                ImplementationType = typeof(TImplementation),
                Scope = scope,
                Factory = () => new TImplementation(),
                Instance = null,
                IsLazy = true
            };
        }
        
        #endregion
        
        #region 解析 API
        
        /// <summary>
        /// 获取服务
        /// </summary>
        public static T Get<T>()
        {
            return (T)Get(typeof(T));
        }
        
        /// <summary>
        /// 尝试获取服务
        /// </summary>
        public static bool TryGet<T>(out T service)
        {
            if (_registrations.TryGetValue(typeof(T), out var reg))
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
            if (!_registrations.TryGetValue(serviceType, out var registration))
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
            var instances = new List<object>();
            foreach (var reg in _registrations.Values)
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
            
            // 按顺序创建实例并初始化
            var initializables = new List<(int order, IInitializable instance)>();
            
            foreach (var type in _initOrder)
            {
                if (!_registrations.TryGetValue(type, out var reg)) continue;
                if (reg.IsLazy) continue; // 跳过懒加载
                
                var instance = ResolveInstance(reg);
                
                if (instance is IInitializable initializable)
                {
                    initializables.Add((initializable.InitOrder, initializable));
                }
            }
            
            // 按优先级排序后初始化
            initializables.Sort((a, b) => a.order.CompareTo(b.order));
            
            foreach (var (_, initializable) in initializables)
            {
                try
                {
                    initializable.Initialize();
                    CYLog.Debug($"[ServiceLocator] 初始化完成: {initializable.GetType().Name}");
                }
                catch (Exception ex)
                {
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
            foreach (var type in _scopedServices)
            {
                if (_registrations.TryGetValue(type, out var reg))
                {
                    DisposeInstance(reg);
                    reg.Instance = null;
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
            var disposables = new List<(int order, IDisposable instance, Type type)>();
            
            foreach (var kvp in _registrations)
            {
                if (kvp.Value.Instance is IDisposable disposable)
                {
                    int order = (disposable is IDisposableEx disposableEx) ? disposableEx.DisposeOrder : 0;
                    disposables.Add((order, disposable, kvp.Key));
                }
            }
            
            // 优先级越大越先销毁
            disposables.Sort((a, b) => b.order.CompareTo(a.order));
            
            foreach (var (_, disposable, type) in disposables)
            {
                try
                {
                    disposable.Dispose();
                    CYLog.Debug($"[ServiceLocator] 销毁完成: {type.Name}");
                }
                catch (Exception ex)
                {
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
                        return registration.Factory();
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
        /// 销毁实例
        /// </summary>
        private static void DisposeInstance(ServiceRegistration registration)
        {
            if (registration.Instance is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    CYLog.Error($"[ServiceLocator] 销毁实例失败: {registration.ServiceType.Name}", ex);
                }
            }
        }
        
        /// <summary>
        /// 构建初始化顺序（拓扑排序）
        /// </summary>
        private static void BuildInitOrder()
        {
            if (_initOrder != null) return;
            
            _initOrder = new List<Type>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();
            
            foreach (var type in _registrations.Keys)
            {
                TopologicalSort(type, visited, visiting);
            }
        }
        
        /// <summary>
        /// 拓扑排序
        /// </summary>
        private static void TopologicalSort(Type type, HashSet<Type> visited, HashSet<Type> visiting)
        {
            if (visited.Contains(type)) return;
            
            if (visiting.Contains(type))
            {
                throw new InvalidOperationException($"[ServiceLocator] 检测到循环依赖: {type.Name}");
            }
            
            visiting.Add(type);
            
            // 获取依赖（通过构造函数参数推断）
            if (_registrations.TryGetValue(type, out var reg) && reg.Dependencies != null)
            {
                foreach (var depName in reg.Dependencies)
                {
                    var depType = Type.GetType(depName);
                    if (depType != null && _registrations.ContainsKey(depType))
                    {
                        TopologicalSort(depType, visited, visiting);
                    }
                }
            }
            
            visiting.Remove(type);
            visited.Add(type);
            _initOrder.Add(type);
        }
        
        #endregion
    }
}
