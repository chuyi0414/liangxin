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
        /// 创建对象池
        /// </summary>
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
        
        public int ActiveCount => _totalCreated - _pool.Count;
        public int PooledCount => _pool.Count;
        
        /// <summary>
        /// 创建 GameObject 对象池
        /// </summary>
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
            
            CYLog.Debug($"[GameObjectPool<{_prefab.name}>] 预热完成，池中数量: {_pool.Count}");
        }
        
        /// <summary>
        /// 获取对象
        /// </summary>
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
            
            // 调用 IPoolable
            var poolables = go.GetComponents<IPoolable>();
            foreach (var poolable in poolables)
            {
                poolable.OnSpawn();
            }
            
            return go;
        }
        
        /// <summary>
        /// 归还对象
        /// </summary>
        public void Return(GameObject go)
        {
            if (go == null) return;
            
            // 调用 IPoolable
            var poolables = go.GetComponents<IPoolable>();
            foreach (var poolable in poolables)
            {
                poolable.OnDespawn();
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
            return go;
        }
    }
    
    /// <summary>
    /// 池管理器（统一管理所有对象池）
    /// </summary>
    public class PoolManager : IInitializable, IDisposableEx
    {
        private readonly Dictionary<Type, object> _genericPools = new();
        private readonly Dictionary<string, GameObjectPool> _goPools = new();
        
        // 配置
        private int _defaultInitialCapacity = 16;
        private int _defaultMaxCapacity = 256;
        private int _defaultWarmupCount = 8;
        
        public int InitOrder => 0;
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
                    CYLog.Debug("[PoolManager] 使用 CYConfigurator 配置");
                }
            }
            
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
            
            CYLog.Debug("[PoolManager] 已销毁");
        }
        
        /// <summary>
        /// 获取或创建通用对象池
        /// </summary>
        public ObjectPool<T> GetOrCreatePool<T>(Func<T> factory, PoolConfig config = null) where T : class
        {
            var type = typeof(T);
            
            if (_genericPools.TryGetValue(type, out var pool))
            {
                return (ObjectPool<T>)pool;
            }
            
            var newPool = new ObjectPool<T>(factory, config);
            _genericPools[type] = newPool;
            return newPool;
        }
        
        /// <summary>
        /// 获取或创建 GameObject 对象池
        /// </summary>
        public GameObjectPool GetOrCreatePool(string key, GameObject prefab, PoolConfig config = null)
        {
            if (_goPools.TryGetValue(key, out var pool))
            {
                return pool;
            }
            
            var newPool = new GameObjectPool(prefab, null, config);
            _goPools[key] = newPool;
            return newPool;
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
