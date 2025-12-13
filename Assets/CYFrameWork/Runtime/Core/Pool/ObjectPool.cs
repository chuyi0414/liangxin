// ============================================================================
// CYFramework 2.2 - 对象池系统
// 文档位置：3.1.6 对象池 (Object Pool)
// 功能：预热、峰值处理、内存收缩、多类型支持
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Core.Config;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Core.Pool
{
    /// <summary>
    /// 可池化接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 从池中取出时调用
        /// </summary>
        void OnSpawn();
        
        /// <summary>
        /// 放回池中时调用
        /// </summary>
        void OnDespawn();
    }
    
    /// <summary>
    /// 池配置
    /// </summary>
    [Serializable]
    public class PoolConfig
    {
        /// <summary>
        /// 初始容量
        /// </summary>
        public int InitialCapacity = 10;
        
        /// <summary>
        /// 最大容量（超出后创建 Overflow 对象）
        /// </summary>
        public int MaxCapacity = 100;
        
        /// <summary>
        /// 预热数量
        /// </summary>
        public int WarmupCount = 5;
    }
    
    /// <summary>
    /// 通用对象池
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _factory;
        private readonly Action<T> _onSpawn;
        private readonly Action<T> _onDespawn;
        private readonly PoolConfig _config;
        
        // 统计
        private int _totalCreated;
        private int _overflowCount;
        private readonly HashSet<T> _overflowObjects = new();
        
        public int ActiveCount => _totalCreated - _pool.Count;
        public int PooledCount => _pool.Count;
        public int OverflowCount => _overflowCount;
        public int TotalCreated => _totalCreated;
        
        /// <summary>
        /// 创建通用对象池
        /// </summary>
        /// <param name="factory">对象创建工厂方法（必需）</param>
        /// <param name="config">池配置（可选，默认使用 DefaultConfig）</param>
        /// <param name="onSpawn">对象取出时的回调（可选）</param>
        /// <param name="onDespawn">对象回收时的回调（可选）</param>
        public ObjectPool(Func<T> factory, PoolConfig config = null, 
            Action<T> onSpawn = null, Action<T> onDespawn = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _config = config ?? new PoolConfig();
            _onSpawn = onSpawn;
            _onDespawn = onDespawn;
            
            _pool = new Stack<T>(_config.InitialCapacity);
        }
        
        /// <summary>
        /// 预热对象池
        /// </summary>
        public void Warmup()
        {
            Warmup(_config.WarmupCount);
        }
        
        /// <summary>
        /// 预热指定数量
        /// </summary>
        public void Warmup(int count)
        {
            for (int i = 0; i < count && _pool.Count < _config.MaxCapacity; i++)
            {
                var obj = CreateNew();
                _pool.Push(obj);
            }
            
            CYLog.Debug($"[ObjectPool<{typeof(T).Name}>] 预热完成，池中数量: {_pool.Count}");
        }
        
        /// <summary>
        /// 获取对象
        /// </summary>
        public T Get()
        {
            T obj;
            
            if (_pool.Count > 0)
            {
                obj = _pool.Pop();
            }
            else if (_totalCreated < _config.MaxCapacity)
            {
                obj = CreateNew();
            }
            else
            {
                // 超出上限，创建 Overflow 对象
                obj = CreateNew();
                _overflowObjects.Add(obj);
                _overflowCount++;
                CYLog.Warning($"[ObjectPool<{typeof(T).Name}>] 创建 Overflow 对象，当前溢出数: {_overflowCount}");
            }
            
            // 调用 Spawn 回调
            _onSpawn?.Invoke(obj);
            
            if (obj is IPoolable poolable)
            {
                poolable.OnSpawn();
            }
            
            return obj;
        }
        
        /// <summary>
        /// 归还对象
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;
            
            // 调用 Despawn 回调
            _onDespawn?.Invoke(obj);
            
            if (obj is IPoolable poolable)
            {
                poolable.OnDespawn();
            }
            
            // Overflow 对象不放回池中
            if (_overflowObjects.Contains(obj))
            {
                _overflowObjects.Remove(obj);
                DestroyObject(obj);
                _overflowCount--;
                return;
            }
            
            // 正常放回池中
            if (_pool.Count < _config.MaxCapacity)
            {
                _pool.Push(obj);
            }
            else
            {
                DestroyObject(obj);
            }
        }
        
        /// <summary>
        /// 内存收缩（低内存时调用）
        /// </summary>
        public void Shrink()
        {
            // 回收所有 Overflow 对象
            foreach (var obj in _overflowObjects)
            {
                DestroyObject(obj);
            }
            _overflowObjects.Clear();
            _overflowCount = 0;
            
            // 回收 50% 空闲对象
            int shrinkCount = _pool.Count / 2;
            for (int i = 0; i < shrinkCount; i++)
            {
                var obj = _pool.Pop();
                DestroyObject(obj);
            }
            
            CYLog.Info($"[ObjectPool<{typeof(T).Name}>] 内存收缩完成，剩余池中数量: {_pool.Count}");
        }
        
        /// <summary>
        /// 清空对象池
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Pop();
                DestroyObject(obj);
            }
            
            foreach (var obj in _overflowObjects)
            {
                DestroyObject(obj);
            }
            _overflowObjects.Clear();
            
            _totalCreated = 0;
            _overflowCount = 0;
        }
        
        /// <summary>
        /// 创建新对象
        /// </summary>
        private T CreateNew()
        {
            var obj = _factory();
            _totalCreated++;
            return obj;
        }
        
        /// <summary>
        /// 销毁对象
        /// </summary>
        private void DestroyObject(T obj)
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _totalCreated--;
        }
    }
    
    /// <summary>
    /// GameObject 对象池
    /// 缓存 IPoolable 组件以避免高频 GetComponents 分配
    /// </summary>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _poolRoot;
        private readonly Stack<GameObject> _pool;
        private readonly PoolConfig _config;
        
        private int _totalCreated;
        private int _overflowCount;
        private readonly HashSet<GameObject> _overflowObjects = new();

        // 最近一次使用时间（unscaled），用于 IdleTimeout 清理
        public float LastUsedTime { get; private set; }
        
        private readonly Dictionary<GameObject, IPoolable[]> _poolableCache = new();
        
        public int ActiveCount => _totalCreated - _pool.Count;
        public int PooledCount => _pool.Count;
        
        /// <summary>
        /// 创建 GameObject 对象池
        /// </summary>
        /// <param name="prefab">预制体对象（必需）</param>
        /// <param name="poolRoot">池根节点（可选，为 null 则自动创建隐藏根节点）</param>
        /// <param name="config">池配置（可选）</param>
        public GameObjectPool(GameObject prefab, Transform poolRoot = null, PoolConfig config = null)
        {
            _prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            _config = config ?? new PoolConfig();
            
            // 创建池根节点
            if (poolRoot == null)
            {
                var root = new GameObject($"Pool_{prefab.name}");
                root.SetActive(false);
                _poolRoot = root.transform;
            }
            else
            {
                _poolRoot = poolRoot;
            }
            
            _pool = new Stack<GameObject>(_config.InitialCapacity);
        }
        
        /// <summary>
        /// 预热
        /// </summary>
        public void Warmup()
        {
            Warmup(_config.WarmupCount);
        }
        
        /// <summary>
        /// 预热指定数量
        /// </summary>
        public void Warmup(int count)
        {
            for (int i = 0; i < count && _pool.Count < _config.MaxCapacity; i++)
            {
                var go = CreateNew();
                go.SetActive(false);
                go.transform.SetParent(_poolRoot, false);
                _pool.Push(go);
            }

            LastUsedTime = Time.unscaledTime;
            
            CYLog.Debug($"[GameObjectPool<{_prefab.name}>] 预热完成，池中数量: {_pool.Count}");
        }
        
        /// <summary>
        /// 获取对象
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="rotation">目标旋转</param>
        /// <param name="parent">目标父节点（重要：从池中取出后将挂载到此节点下；若为 null，则挂载到场景根节点）</param>
        /// <returns>已激活的 GameObject 实例</returns>
        public GameObject Get(Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            GameObject go;
            
            if (_pool.Count > 0)
            {
                go = _pool.Pop();
            }
            else if (_totalCreated < _config.MaxCapacity)
            {
                go = CreateNew();
            }
            else
            {
                // Overflow
                go = CreateNew();
                _overflowObjects.Add(go);
                _overflowCount++;
            }
            
            // 设置位置和父级
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.SetActive(true);
            LastUsedTime = Time.unscaledTime;
            
            // 调用 IPoolable（使用缓存避免 GC）
            var poolables = GetCachedPoolables(go);
            if (poolables != null)
            {
                for (int i = 0; i < poolables.Length; i++)
                {
                    poolables[i].OnSpawn();
                }
            }
            
            return go;
        }
        
        /// <summary>
        /// 归还对象
        /// </summary>
        public void Return(GameObject go)
        {
            if (go == null) return;
            
            // 调用 IPoolable（使用缓存避免 GC）
            var poolables = GetCachedPoolables(go);
            if (poolables != null)
            {
                for (int i = 0; i < poolables.Length; i++)
                {
                    poolables[i].OnDespawn();
                }
            }
            
            go.SetActive(false);
            
            // Overflow 对象直接销毁
            if (_overflowObjects.Contains(go))
            {
                _overflowObjects.Remove(go);
                UnityEngine.Object.Destroy(go);
                _totalCreated--;
                _overflowCount--;
                return;
            }
            
            // 放回池中
            go.transform.SetParent(_poolRoot, false);
            _pool.Push(go);
            LastUsedTime = Time.unscaledTime;
        }
        
        /// <summary>
        /// 内存收缩
        /// </summary>
        public void Shrink()
        {
            // 销毁 Overflow 对象
            foreach (var go in _overflowObjects)
            {
                UnityEngine.Object.Destroy(go);
                _totalCreated--;
            }
            _overflowObjects.Clear();
            _overflowCount = 0;
            
            // 销毁 50% 空闲对象
            int shrinkCount = _pool.Count / 2;
            for (int i = 0; i < shrinkCount; i++)
            {
                var go = _pool.Pop();
                UnityEngine.Object.Destroy(go);
                _totalCreated--;
            }
            
            CYLog.Info($"[GameObjectPool<{_prefab.name}>] 内存收缩完成");
        }

        /// <summary>
        /// 是否处于空闲状态（没有 Active 对象）
        /// </summary>
        public bool IsIdle => ActiveCount <= 0;
        
        /// <summary>
        /// 清空
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var go = _pool.Pop();
                UnityEngine.Object.Destroy(go);
            }
            
            foreach (var go in _overflowObjects)
            {
                UnityEngine.Object.Destroy(go);
            }
            _overflowObjects.Clear();
            
            if (_poolRoot != null)
            {
                UnityEngine.Object.Destroy(_poolRoot.gameObject);
            }
            
            _totalCreated = 0;
            _overflowCount = 0;
        }
        
        /// <summary>
        /// 创建新对象
        /// </summary>
        private GameObject CreateNew()
        {
            var go = UnityEngine.Object.Instantiate(_prefab);
            _totalCreated++;
            
            // 缓存 IPoolable 组件（避免高频 GetComponents 分配）
            var poolables = go.GetComponents<IPoolable>();
            if (poolables.Length > 0)
            {
                _poolableCache[go] = poolables;
            }
            
            return go;
        }
        
        /// <summary>
        /// 获取缓存的 IPoolable 组件（零 GC）
        /// </summary>
        private IPoolable[] GetCachedPoolables(GameObject go)
        {
            if (_poolableCache.TryGetValue(go, out var poolables))
            {
                return poolables;
            }
            return null;
        }
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        private void RemoveFromCache(GameObject go)
        {
            _poolableCache.Remove(go);
        }
    }
    
    /// <summary>
    /// 池管理器（统一管理所有对象池）
    /// </summary>
    public class PoolManager : IInitializable, IUpdateable, IDisposableEx
    {
        private readonly Dictionary<Type, object> _genericPools = new();
        private readonly Dictionary<string, GameObjectPool> _goPools = new();

        // [ObjectPools] 根与分组
        private Transform _poolRoot;
        private readonly Dictionary<string, Transform> _groupRoots = new();
        private string[] _poolGroups = Array.Empty<string>();

        // 配置
        private int _defaultInitialCapacity = 16;
        private int _defaultMaxCapacity = 256;
        private int _defaultWarmupCount = 8;
        private float _cleanupInterval = 60f;
        private float _idleTimeout = 120f;
        private float _cleanupTimer;
        
        public int InitOrder => 0;
        public int UpdateOrder => 50;
        public int DisposeOrder => 0;
        
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                var config = configurator.GetConfig<PoolManagerConfig>();
                if (config != null)
                {
                    _defaultInitialCapacity = config.DefaultInitialCapacity;
                    _defaultMaxCapacity = config.DefaultMaxCapacity;
                    _defaultWarmupCount = config.DefaultWarmupCount;
                    _cleanupInterval = Mathf.Max(0f, config.CleanupInterval);
                    _idleTimeout = Mathf.Max(0f, config.IdleTimeout);
                    _poolGroups = config.PoolGroups ?? Array.Empty<string>();
                    CYLog.Debug("[PoolManager] 使用 CYConfigurator 配置");
                }
            }

            CreateOrBindPoolRoot();
            
            // 注册低内存回调
            Application.lowMemory += OnLowMemory;
            CYLog.Debug("[PoolManager] 初始化完成");
        }
        
        public void Dispose()
        {
            Application.lowMemory -= OnLowMemory;
            
            // 清理所有池
            foreach (var pool in _goPools.Values)
            {
                pool.Clear();
            }
            _goPools.Clear();
            _genericPools.Clear();
            _groupRoots.Clear();

            // 框架关闭时统一销毁 [ObjectPools]：
            // - 即使该节点是场景里预置的，也应在退出/销毁框架时清理，避免编辑器/场景关闭时残留导致警告。
            // - Dispose 仅在框架 Shutdown 时调用，不影响运行中的跨场景对象池复用。
            if (_poolRoot != null)
            {
                UnityEngine.Object.Destroy(_poolRoot.gameObject);
                _poolRoot = null;
            }
            
            CYLog.Debug("[PoolManager] 已销毁");
        }

        /// <summary>
        /// 对象池周期性清理（受 <see cref="PoolManagerConfig.CleanupInterval"/> 控制）。
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            if (_cleanupInterval <= 0f) return;

            _cleanupTimer += Time.unscaledDeltaTime;
            if (_cleanupTimer < _cleanupInterval) return;
            _cleanupTimer = 0f;

            if (_idleTimeout <= 0f) return;

            float now = Time.unscaledTime;
            foreach (var kv in _goPools)
            {
                var pool = kv.Value;
                if (pool == null) continue;
                if (!pool.IsIdle) continue;

                if (now - pool.LastUsedTime >= _idleTimeout)
                {
                    pool.Shrink();
                }
            }
        }
        
        /// <summary>
        /// 获取或创建通用对象池
        /// 如果未指定配置，使用默认配置
        /// </summary>
        public ObjectPool<T> GetOrCreatePool<T>(Func<T> factory, PoolConfig config = null) where T : class
        {
            var type = typeof(T);
            
            if (_genericPools.TryGetValue(type, out var pool))
            {
                return (ObjectPool<T>)pool;
            }
            
            // 使用默认配置
            config ??= CreateDefaultConfig();
            
            var newPool = new ObjectPool<T>(factory, config);
            _genericPools[type] = newPool;
            return newPool;
        }
        
        /// <summary>
        /// 获取或创建 GameObject 对象池
        /// 如果未指定配置，使用默认配置
        /// </summary>
        public GameObjectPool GetOrCreatePool(string key, GameObject prefab, PoolConfig config = null)
        {
            if (_goPools.TryGetValue(key, out var pool))
            {
                return pool;
            }
            
            // 使用默认配置
            config ??= CreateDefaultConfig();

            // 默认放入 Misc 分组
            var groupRoot = GetOrCreateGroupRoot("Misc");
            var poolRoot = CreatePoolRootUnder(groupRoot, prefab != null ? prefab.name : key);
            var newPool = new GameObjectPool(prefab, poolRoot, config);
            _goPools[key] = newPool;
            return newPool;
        }

        /// <summary>
        /// 获取或创建 GameObject 对象池，并指定分组（运行时层级会放到 [ObjectPools]/{groupName}/ 下）。
        /// </summary>
        /// <param name="key">对象池 Key</param>
        /// <param name="prefab">预制体资源</param>
        /// <param name="groupName">分组名称（如 "Bullets", "Effects"）</param>
        /// <param name="config">配置</param>
        /// <returns>对象池实例</returns>
        public GameObjectPool GetOrCreatePool(string key, GameObject prefab, string groupName, PoolConfig config = null)
        {
            if (_goPools.TryGetValue(key, out var pool))
            {
                return pool;
            }

            config ??= CreateDefaultConfig();

            var groupRoot = GetOrCreateGroupRoot(string.IsNullOrEmpty(groupName) ? "Misc" : groupName);
            var poolRoot = CreatePoolRootUnder(groupRoot, prefab != null ? prefab.name : key);
            var newPool = new GameObjectPool(prefab, poolRoot, config);
            _goPools[key] = newPool;
            return newPool;
        }
        
        /// <summary>
        /// 创建默认配置（使用 CYConfigurator 中的默认值）
        /// </summary>
        private PoolConfig CreateDefaultConfig()
        {
            return new PoolConfig
            {
                InitialCapacity = _defaultInitialCapacity,
                MaxCapacity = _defaultMaxCapacity,
                WarmupCount = _defaultWarmupCount
            };
        }

        private void CreateOrBindPoolRoot()
        {
            if (_poolRoot != null) return;

            // 注意：GameObject.Find 找不到未激活对象。
            // 你的场景/预制体里可能已经放了一个未激活的 [ObjectPools]，这时如果仅用 Find，会导致框架再创建一个新的根节点。
            var existing = GameObject.Find("[ObjectPools]") ?? FindInSceneIncludingInactive("[ObjectPools]");
            if (existing != null)
            {
                _poolRoot = existing.transform;
            }
            else
            {
                var rootGo = new GameObject("[ObjectPools]");

                // 如果框架入口存在，把对象池挂到入口下，层级更清晰（依赖入口对象的 DontDestroyOnLoad，而不是对自己调用）。
                // 注意：DontDestroyOnLoad 只能作用于根节点，如果 rootGo 作为子节点就不需要单独调用。
                if (CYBootstrap.Instance != null)
                {
                    rootGo.transform.SetParent(CYBootstrap.Instance.transform, false);
                }
                else
                {
                    UnityEngine.Object.DontDestroyOnLoad(rootGo);
                }

                _poolRoot = rootGo.transform;
            }

            // 创建分组节点
            if (_poolGroups != null)
            {
                for (int i = 0; i < _poolGroups.Length; i++)
                {
                    GetOrCreateGroupRoot(_poolGroups[i]);
                }
            }

            // 确保 Misc 存在（兼容未配置的情况）
            GetOrCreateGroupRoot("Misc");
        }

        /// <summary>
        /// 在场景中查找（包含未激活对象）的节点。
        /// </summary>
        /// <remarks>
        /// - 仅在初始化时调用一次，允许使用 Resources.FindObjectsOfTypeAll（会产生一定 GC）。
        /// - 会过滤掉 Project/Resources 里的资产对象，仅匹配场景实例。
        /// </remarks>
        private static GameObject FindInSceneIncludingInactive(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                if (!string.Equals(t.name, name, StringComparison.Ordinal)) continue;

                // 过滤非场景对象（例如 Prefab Asset）
                var go = t.gameObject;
                if (!go.scene.IsValid()) continue;

                return go;
            }

            return null;
        }

        private Transform GetOrCreateGroupRoot(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) groupName = "Misc";

            if (_groupRoots.TryGetValue(groupName, out var cached) && cached != null)
            {
                return cached;
            }

            if (_poolRoot == null)
            {
                CreateOrBindPoolRoot();
            }

            var found = _poolRoot.Find(groupName);
            if (found != null)
            {
                _groupRoots[groupName] = found;
                return found;
            }

            var go = new GameObject(groupName);
            go.transform.SetParent(_poolRoot, false);
            _groupRoots[groupName] = go.transform;
            return go.transform;
        }

        /// <summary>
        /// 获取或创建对象池分组根节点（运行时层级位于 [ObjectPools]/{groupName}）。
        /// </summary>
        /// <remarks>
        /// 这是给其它系统（例如 UIManager）使用的“桥接 API”，避免各系统各自创建 [ObjectPools] 根节点导致重复与清理警告。
        /// </remarks>
        public Transform GetOrCreatePoolGroupRoot(string groupName)
        {
            return GetOrCreateGroupRoot(groupName);
        }

        private Transform CreatePoolRootUnder(Transform groupRoot, string prefabNameOrKey)
        {
            if (groupRoot == null)
            {
                groupRoot = GetOrCreateGroupRoot("Misc");
            }

            var root = new GameObject($"Pool_{prefabNameOrKey}");
            root.SetActive(false);
            root.transform.SetParent(groupRoot, false);
            return root.transform;
        }
        
        /// <summary>
        /// 低内存回调
        /// </summary>
        private void OnLowMemory()
        {
            CYLog.Warning("[PoolManager] 检测到低内存，执行内存收缩");
            ShrinkAll();
        }
        
        /// <summary>
        /// 收缩所有池
        /// </summary>
        public void ShrinkAll()
        {
            foreach (var pool in _goPools.Values)
            {
                pool.Shrink();
            }
        }
    }
}
