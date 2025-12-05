// ============================================================================
// CYFramework - 对象池模块
// 提供通用对象池，用于复用频繁创建/销毁的对象
// 
// 设计要点：
// - 泛型对象池，支持任意类型
// - 限制最大容量，防止无限膨胀
// - 支持按需预热
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 对象池模块
    /// 管理多种类型的对象池
    /// </summary>
    public class ObjectPoolModule : IModule
    {
        // ====================================================================
        // IModule 实现
        // ====================================================================

        public int Priority => 5; // 对象池优先级较高
        public bool NeedUpdate => false; // 对象池不需要帧更新

        public void Initialize()
        {
            _pools = new Dictionary<Type, object>();
            Log.I("ObjectPoolModule", "初始化完成");
        }

        public void Update(float deltaTime) { }

        public void Shutdown()
        {
            ClearAll();
            Log.I("ObjectPoolModule", "已关闭");
        }

        // ====================================================================
        // 配置
        // ====================================================================

        /// <summary>
        /// 默认最大容量
        /// </summary>
        public int DefaultCapacity { get; set; } = 100;

        /// <summary>
        /// 是否自动扩容
        /// </summary>
        public bool AutoExpand { get; set; } = true;

        // ====================================================================
        // 对象池核心
        // ====================================================================

        // 存储所有类型的对象池
        private Dictionary<Type, object> _pools;

        /// <summary>
        /// 缓存的对象池类型数量
        /// </summary>
        public int CachedCount => _pools?.Count ?? 0;

        /// <summary>
        /// 获取或创建指定类型的对象池
        /// </summary>
        /// <typeparam name="T">对象类型（必须有无参构造函数）</typeparam>
        /// <param name="maxCapacity">最大容量</param>
        /// <returns>对象池</returns>
        public ObjectPool<T> GetPool<T>(int maxCapacity = 100) where T : class, new()
        {
            Type type = typeof(T);

            if (_pools.TryGetValue(type, out object poolObj))
            {
                return poolObj as ObjectPool<T>;
            }

            ObjectPool<T> pool = new ObjectPool<T>(maxCapacity);
            _pools[type] = pool;
            return pool;
        }

        /// <summary>
        /// 从池中获取对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>对象实例</returns>
        public T Get<T>() where T : class, new()
        {
            return GetPool<T>().Get();
        }

        /// <summary>
        /// 将对象归还到池中
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">对象实例</param>
        public void Return<T>(T obj) where T : class, new()
        {
            GetPool<T>().Return(obj);
        }

        /// <summary>
        /// 预热对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="count">预创建数量</param>
        public void Prewarm<T>(int count) where T : class, new()
        {
            GetPool<T>().Prewarm(count);
        }

        /// <summary>
        /// 清空指定类型的对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        public void Clear<T>() where T : class, new()
        {
            if (_pools.TryGetValue(typeof(T), out object poolObj))
            {
                (poolObj as ObjectPool<T>)?.Clear();
            }
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public void ClearAll()
        {
            foreach (var pool in _pools.Values)
            {
                (pool as IObjectPoolBase)?.Clear();
            }
            _pools.Clear();
        }

        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        public string GetStats()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("对象池统计:");
            foreach (var kvp in _pools)
            {
                var pool = kvp.Value as IObjectPoolBase;
                if (pool != null)
                {
                    sb.AppendLine($"  {kvp.Key.Name}: {pool.PooledCount}/{pool.MaxCapacity}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 对象池基础接口（用于非泛型访问）
    /// </summary>
    internal interface IObjectPoolBase
    {
        int PooledCount { get; }
        int MaxCapacity { get; }
        void Clear();
    }

    /// <summary>
    /// 泛型对象池
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public class ObjectPool<T> : IObjectPoolBase where T : class, new()
    {
        private readonly Stack<T> _pool;
        private readonly int _maxCapacity;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onReturn;

        /// <summary>
        /// 当前池中对象数量
        /// </summary>
        public int PooledCount => _pool.Count;

        /// <summary>
        /// 最大容量
        /// </summary>
        public int MaxCapacity => _maxCapacity;

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="maxCapacity">最大容量</param>
        /// <param name="onGet">获取对象时的回调（可选）</param>
        /// <param name="onReturn">归还对象时的回调（可选）</param>
        public ObjectPool(int maxCapacity = 100, Action<T> onGet = null, Action<T> onReturn = null)
        {
            _maxCapacity = maxCapacity;
            _pool = new Stack<T>(Math.Min(maxCapacity, 32));
            _onGet = onGet;
            _onReturn = onReturn;
        }

        /// <summary>
        /// 从池中获取对象
        /// </summary>
        /// <returns>对象实例</returns>
        public T Get()
        {
            T obj;
            if (_pool.Count > 0)
            {
                obj = _pool.Pop();
            }
            else
            {
                obj = new T();
            }

            _onGet?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// 将对象归还到池中
        /// </summary>
        /// <param name="obj">对象实例</param>
        public void Return(T obj)
        {
            if (obj == null) return;

            _onReturn?.Invoke(obj);

            // 超过最大容量时不再入池
            if (_pool.Count < _maxCapacity)
            {
                _pool.Push(obj);
            }
        }

        /// <summary>
        /// 预热对象池
        /// </summary>
        /// <param name="count">预创建数量</param>
        public void Prewarm(int count)
        {
            count = Math.Min(count, _maxCapacity - _pool.Count);
            for (int i = 0; i < count; i++)
            {
                _pool.Push(new T());
            }
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
        }
    }
}
