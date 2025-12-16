// ============================================================================
// CYFramework 2.2 - 资源加载器
// 功能：统一资源加载接口，支持 Resources/Addressables/AB
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CYFramework.Core.Config;
using CYFramework.Infrastructure;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CYFramework.Core.Resource
{
    /// <summary>
    /// 资源加载器接口
    /// </summary>
    public interface IResourceLoader
    {
        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <returns>加载的资源实例（失败返回 null）</returns>
        T Load<T>(string path) where T : Object;
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="callback">加载完成回调（成功返回实例，失败返回 null）</param>
        void LoadAsync<T>(string path, Action<T> callback) where T : Object;
        
        /// <summary>
        /// 异步加载资源（Task 版）
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <returns>Task 结果为资源实例</returns>
        Task<T> LoadAsync<T>(string path) where T : Object;
        
        /// <summary>
        /// 卸载资源
        /// </summary>
        void Unload(string path);
        
        /// <summary>
        /// 卸载未使用的资源（需在安全时机手动触发，以避免运行帧尖峰）。
        /// </summary>
        /// <param name="forceGC">是否强制执行 GC（Release 下默认不执行，避免帧尖刺）</param>
        void UnloadUnused(bool forceGC = false);
    }

    /// <summary>
    /// 资源加载器实现
    /// </summary>
    public class ResourceLoader : IResourceLoader
    {
        #region 内部类型定义
        
        private class CacheEntry
        {
            public Object Asset;
            public long SizeBytes;
            public LinkedListNode<string> LruNode;
        }
        
        #endregion

        #region 字段
        
        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private readonly Dictionary<string, int> _refCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, List<Action<Object>>> _loadingCallbacks = new Dictionary<string, List<Action<Object>>>();
        private readonly LinkedList<string> _lruList = new LinkedList<string>();
        
        private int _asyncLoadPriority = 0;
        private int _cacheSizeMB = 100; // 默认 100MB 缓存
        private long _cacheBytes = 0;
        private bool _enableRefCount = true;
        private bool _hasPendingUnload = false;
        
        private long MaxCacheBytes => _cacheSizeMB * 1024L * 1024L;

        #endregion

        /// <summary>
        /// 销毁/清理
        /// </summary>
        public void Dispose()
        {
            _cache.Clear();
            _refCounts.Clear();
            _loadingCallbacks.Clear();
            _lruList.Clear();
            _cacheBytes = 0;
            CYLog.Debug("[ResourceLoader] 已销毁");
        }
        
        #region 同步加载
        
        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            // 检查缓存
            if (_cache.TryGetValue(path, out var cachedEntry))
            {
                TouchEntry(path, cachedEntry);
                Retain(path);
                return cachedEntry.Asset as T;
            }
            
            // 从 Resources 加载
            var asset = Resources.Load<T>(path);
            
            if (asset != null)
            {
                AddToCache(path, asset);
                Retain(path);
                CYLog.Debug($"[ResourceLoader] 加载成功: {path}");
            }
            else
            {
                CYLog.Warning($"[ResourceLoader] 加载失败: {path}");
            }
            
            return asset;
        }

        /// <summary>
        /// 是否已缓存（已加载进 ResourceLoader 缓存）。
        /// </summary>
        public bool IsCached(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return _cache.ContainsKey(path);
        }

        /// <summary>
        /// 尝试从缓存获取资源：不会触发 Resources.Load。
        /// </summary>
        public bool TryGetCached<T>(string path, out T asset) where T : Object
        {
            if (!string.IsNullOrEmpty(path) && _cache.TryGetValue(path, out var entry))
            {
                asset = entry.Asset as T;
                return asset != null;
            }

            asset = null;
            return false;
        }

        /// <summary>
        /// TryLoad 形式：加载成功返回 true，失败返回 false（不会抛异常）。
        /// </summary>
        public bool TryLoad<T>(string path, out T asset) where T : Object
        {
            asset = Load<T>(path);
            return asset != null;
        }

        /// <summary>
        /// 获取引用计数（仅当 EnableRefCount=true 时有意义）。
        /// </summary>
        public int GetRefCount(string path)
        {
            if (!_enableRefCount) return 0;
            if (string.IsNullOrEmpty(path)) return 0;
            return _refCounts.TryGetValue(path, out var count) ? count : 0;
        }

        /// <summary>
        /// 手动增加引用计数：适用于“长驻资源”或你希望跨系统共享的资源。
        /// </summary>
        public void RetainAsset(string path)
        {
            Retain(path);
        }

        /// <summary>
        /// 手动减少引用计数：与 <see cref="RetainAsset"/> 成对使用。
        /// </summary>
        public void ReleaseAsset(string path)
        {
            Release(path);
        }
        
        #endregion
        
        #region 异步加载
        
        /// <summary>
        /// 异步加载资源（回调版）
        /// </summary>
        public void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            // 检查缓存
            if (_cache.TryGetValue(path, out var cachedEntry))
            {
                TouchEntry(path, cachedEntry);
                Retain(path);
                callback?.Invoke(cachedEntry.Asset as T);
                return;
            }
            
            // 检查是否正在加载
            if (_loadingCallbacks.TryGetValue(path, out var callbacks))
            {
                callbacks.Add(obj => callback?.Invoke(obj as T));
                return;
            }
            
            // 开始加载
            _loadingCallbacks[path] = new List<Action<Object>> { obj => callback?.Invoke(obj as T) };

            // 重要：这里统一用非泛型 Resources.LoadAsync(path)。
            // 原因：如果首次请求用错了 T（例如先 LoadAsync<Component> 再 LoadAsync<GameObject>），
            // 泛型版本会导致 request.asset 直接为 null，从而让后续正确类型也拿不到资源。
            var request = Resources.LoadAsync(path);
            request.priority = _asyncLoadPriority;
            request.completed += _ =>
            {
                var asset = request.asset;
                
                if (asset != null)
                {
                    AddToCache(path, asset);
                    Retain(path);
                }
                
                // 执行所有回调
                if (_loadingCallbacks.TryGetValue(path, out var cbs))
                {
                    foreach (var cb in cbs)
                    {
                        cb?.Invoke(asset);
                    }
                    _loadingCallbacks.Remove(path);
                }
            };
        }
        
        /// <summary>
        /// 异步加载资源（Task 版）
        /// </summary>
        public async Task<T> LoadAsync<T>(string path) where T : Object
        {
            // 检查缓存
            if (_cache.TryGetValue(path, out var cachedEntry))
            {
                TouchEntry(path, cachedEntry);
                Retain(path);
                return cachedEntry.Asset as T;
            }
            
            // 同回调版：统一走非泛型 LoadAsync，避免首次错误类型导致资源永远为 null。
            var request = Resources.LoadAsync(path);
            request.priority = _asyncLoadPriority;
            
            while (!request.isDone)
            {
                await Task.Yield();
            }

            var raw = request.asset;
            if (raw != null)
            {
                AddToCache(path, raw);
                Retain(path);
            }

            return raw as T;
        }
        
        #endregion
        
        #region 卸载
        
        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(string path)
        {
            if (!_cache.TryGetValue(path, out var entry))
            {
                return;
            }

            if (_enableRefCount)
            {
                Release(path);
                if (_refCounts.TryGetValue(path, out var count) && count > 0)
                {
                    CYLog.Debug($"[ResourceLoader] 引用计数未归零，跳过卸载: {path}, ref={count}");
                    return;
                }
            }

            RemoveFromCache(path, entry);
            CYLog.Debug($"[ResourceLoader] 卸载: {path}");
        }
        
        /// <summary>
        /// 卸载未使用的资源
        /// ❗ 注意：GC.Collect() 可能导致帧尖刺，默认仅在 Editor/Development 下执行
        /// 建议在 Loading 场景或明确的内存清理时机调用
        /// </summary>
        /// <param name="forceGC">是否强制执行 GC（Release 下默认不执行，避免帧尖刺）</param>
        public void UnloadUnused(bool forceGC = false)
        {
            if (!_hasPendingUnload && !forceGC)
            {
                CYLog.Debug("[ResourceLoader] 无待卸载的资源，跳过 UnloadUnusedAssets");
                return;
            }

            // ⚠️ 使用场景建议：
            // - 优先在 Loading 场景或内存压力明显时调用，避免战斗/主循环中触发 GC 帧尖刺
            // - Dev/Editor 默认会执行 GC.Collect，Release 建议仅在必要时传入 forceGC
            Resources.UnloadUnusedAssets();
            _hasPendingUnload = false;
            
            // GC.Collect() 仅在 Editor/Development 或显式要求时执行
            // 避免在 Release 下产生不可控的帧尖刺
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GC.Collect();
            CYLog.Debug("[ResourceLoader] 卸载未使用资源 (含 GC)");
#else
            if (forceGC)
            {
                GC.Collect();
                CYLog.Debug("[ResourceLoader] 卸载未使用资源 (含 GC)");
            }
            else
            {
                CYLog.Debug("[ResourceLoader] 卸载未使用资源");
            }
#endif
        }
        
        #endregion
        
        #region 场景加载
        
        /// <summary>
        /// 加载场景
        /// </summary>
        public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, Action onComplete = null)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, mode);
            
            if (operation != null)
            {
                if (onComplete != null)
                {
                    operation.completed += _ => onComplete();
                }
            }
            else
            {
                onComplete?.Invoke();
            }
        }
        
        /// <summary>
        /// 异步加载场景
        /// </summary>
        public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return SceneManager.LoadSceneAsync(sceneName, mode);
        }
        
        #endregion
        
        #region 实例化 API
        
        /// <summary>
        /// 加载并实例化 GameObject
        /// </summary>
        public GameObject Instantiate(string path, Transform parent = null)
        {
            var prefab = Load<GameObject>(path);
            if (prefab == null)
            {
                CYLog.Warning($"[ResourceLoader] 实例化失败，找不到资源: {path}");
                return null;
            }
            
            var go = Object.Instantiate(prefab, parent);
            return go;
        }
        
        /// <summary>
        /// 异步加载并实例化 GameObject
        /// </summary>
        public void InstantiateAsync(string path, Action<GameObject> callback, Transform parent = null)
        {
            LoadAsync<GameObject>(path, prefab =>
            {
                if (prefab == null)
                {
                    CYLog.Warning($"[ResourceLoader] 异步实例化失败，找不到资源: {path}");
                    callback?.Invoke(null);
                    return;
                }
                
                var go = Object.Instantiate(prefab, parent);
                callback?.Invoke(go);
            });
        }
        
        #endregion
        
        #region 预加载 API
        
        /// <summary>
        /// 预加载资源（不返回，只缓存）
        /// </summary>
        public void Preload<T>(string path) where T : Object
        {
            Load<T>(path);
            if (_enableRefCount)
            {
                Release(path);
            }
        }
        
        /// <summary>
        /// 批量预加载
        /// </summary>
        public void PreloadAsync(string[] paths, Action onComplete = null, Action<float> onProgress = null)
        {
            if (paths == null || paths.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }
            
            int total = paths.Length;
            int loaded = 0;
            
            foreach (var path in paths)
            {
                LoadAsync<Object>(path, _ =>
                {
                    loaded++;
                    onProgress?.Invoke((float)loaded / total);

                    if (_enableRefCount)
                    {
                        Release(path);
                    }
                    
                    if (loaded >= total)
                    {
                        onComplete?.Invoke();
                    }
                });
            }
        }

        #endregion

        #region 内部辅助
        
        private void Retain(string path)
        {
            if (!_enableRefCount) return;
            _refCounts.TryGetValue(path, out var count);
            _refCounts[path] = count + 1;
        }

        private void Release(string path)
        {
            if (!_enableRefCount) return;
            if (!_refCounts.TryGetValue(path, out var count)) return;

            count--;
            if (count <= 0)
            {
                _refCounts.Remove(path);
            }
            else
            {
                _refCounts[path] = count;
            }
        }

        private void AddToCache(string path, Object asset)
        {
            if (string.IsNullOrEmpty(path) || asset == null) return;

            if (_cache.TryGetValue(path, out var existing))
            {
                TouchEntry(path, existing);
                return;
            }

            var entry = new CacheEntry
            {
                Asset = asset,
                SizeBytes = GetAssetSizeBytes(asset),
                LruNode = _lruList.AddFirst(path)
            };

            _cache[path] = entry;
            _cacheBytes += entry.SizeBytes;
            EvictIfNeeded();
        }

        private void TouchEntry(string path, CacheEntry entry)
        {
            if (entry == null || entry.LruNode == null) return;
            _lruList.Remove(entry.LruNode);
            entry.LruNode = _lruList.AddFirst(path);
        }

        private void RemoveFromCache(string path, CacheEntry entry)
        {
            if (entry == null) return;
            if (entry.LruNode != null)
            {
                _lruList.Remove(entry.LruNode);
                entry.LruNode = null;
            }

            _cache.Remove(path);
            _cacheBytes -= entry.SizeBytes;
            if (_cacheBytes < 0) _cacheBytes = 0;
            _hasPendingUnload = true;
        }

        private void EvictIfNeeded()
        {
            if (_cacheSizeMB <= 0) return;
            var maxBytes = MaxCacheBytes;
            if (maxBytes <= 0) return;
            if (_cacheBytes <= maxBytes) return;

            bool evictedAny = false;
            int guard = _cache.Count;
            while (_cacheBytes > maxBytes && _lruList.Last != null && guard-- > 0)
            {
                string key = _lruList.Last.Value;
                if (!_cache.TryGetValue(key, out var entry))
                {
                    _lruList.RemoveLast();
                    continue;
                }

                if (_enableRefCount && _refCounts.TryGetValue(key, out var refCount) && refCount > 0)
                {
                    // 引用计数 > 0 的不淘汰，移到队首
                    _lruList.RemoveLast();
                    _lruList.AddFirst(key);
                    continue;
                }

                RemoveFromCache(key, entry);
                evictedAny = true;
            }

            if (evictedAny)
            {
                _hasPendingUnload = true;
            }
        }

        private static long GetAssetSizeBytes(Object asset)
        {
            if (asset == null) return 0;
            try
            {
                return Profiler.GetRuntimeMemorySizeLong(asset);
            }
            catch
            {
                return 0;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Addressables 资源加载器（预留）
    /// Native 端使用
    /// </summary>
#if !CY_WECHAT && !UNITY_WEBGL
    // TODO: 实现 Addressables 版本
    // public class AddressablesLoader : IResourceLoader { ... }
#endif
}
