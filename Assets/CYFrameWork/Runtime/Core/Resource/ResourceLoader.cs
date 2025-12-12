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
        T Load<T>(string path) where T : Object;
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        void LoadAsync<T>(string path, Action<T> callback) where T : Object;
        
        /// <summary>
        /// 异步加载资源（Task 版）
        /// </summary>
        Task<T> LoadAsync<T>(string path) where T : Object;
        
        /// <summary>
        /// 卸载资源
        /// </summary>
        void Unload(string path);
        
        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        void UnloadUnused();
        
        /// <summary>
        /// 加载场景
        /// </summary>
        void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, Action onComplete = null);
        
        /// <summary>
        /// 异步加载场景
        /// </summary>
        AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single);
        
        /// <summary>
        /// 加载并实例化 GameObject
        /// </summary>
        GameObject Instantiate(string path, Transform parent = null);
        
        /// <summary>
        /// 异步加载并实例化 GameObject
        /// </summary>
        void InstantiateAsync(string path, Action<GameObject> callback, Transform parent = null);
        
        /// <summary>
        /// 预加载资源（不返回，只缓存）
        /// </summary>
        void Preload<T>(string path) where T : Object;
        
        /// <summary>
        /// 批量预加载
        /// </summary>
        void PreloadAsync(string[] paths, Action onComplete = null, Action<float> onProgress = null);
    }
    
    /// <summary>
    /// Resources 资源加载器
    /// 基础实现，可扩展为 Addressables
    /// </summary>
    public class ResourceLoader : IResourceLoader, IInitializable, IDisposableEx
    {
        // 资源缓存
        private readonly Dictionary<string, Object> _cache = new();
        
        // 加载中的资源（防止重复加载）
        private readonly Dictionary<string, List<Action<Object>>> _loadingCallbacks = new();
        
        // 配置
        private ResourceLoadMode _loadMode = ResourceLoadMode.Resources;
        private int _cacheSizeMB = 100;
        private bool _enableRefCount = true;
        
        public int InitOrder => -40;
        public int DisposeOrder => 40;
        
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                var config = configurator.GetConfig<ResourceLoaderConfig>();
                if (config != null)
                {
                    _loadMode = config.LoadMode;
                    _cacheSizeMB = config.CacheSizeMB;
                    _enableRefCount = config.EnableRefCount;
                    CYLog.Debug($"[ResourceLoader] 使用 CYConfigurator 配置, 模式: {_loadMode}");
                }
            }
            
            CYLog.Debug("[ResourceLoader] 初始化完成");
        }
        
        public void Dispose()
        {
            _cache.Clear();
            _loadingCallbacks.Clear();
            
#if !UNITY_EDITOR
            Resources.UnloadUnusedAssets();
#endif
            
            CYLog.Debug("[ResourceLoader] 已销毁");
        }
        
        #region 同步加载
        
        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            // 检查缓存
            if (_cache.TryGetValue(path, out var cached))
            {
                return cached as T;
            }
            
            // 从 Resources 加载
            var asset = Resources.Load<T>(path);
            
            if (asset != null)
            {
                _cache[path] = asset;
                CYLog.Debug($"[ResourceLoader] 加载成功: {path}");
            }
            else
            {
                CYLog.Warning($"[ResourceLoader] 加载失败: {path}");
            }
            
            return asset;
        }
        
        #endregion
        
        #region 异步加载
        
        /// <summary>
        /// 异步加载资源（回调版）
        /// </summary>
        public void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            // 检查缓存
            if (_cache.TryGetValue(path, out var cached))
            {
                callback?.Invoke(cached as T);
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
            
            var request = Resources.LoadAsync<T>(path);
            request.completed += _ =>
            {
                var asset = request.asset as T;
                
                if (asset != null)
                {
                    _cache[path] = asset;
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
            if (_cache.TryGetValue(path, out var cached))
            {
                return cached as T;
            }
            
            var request = Resources.LoadAsync<T>(path);
            
            while (!request.isDone)
            {
                await Task.Yield();
            }
            
            var asset = request.asset as T;
            
            if (asset != null)
            {
                _cache[path] = asset;
            }
            
            return asset;
        }
        
        #endregion
        
        #region 卸载
        
        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(string path)
        {
            if (_cache.TryGetValue(path, out var asset))
            {
                _cache.Remove(path);
                
#if !UNITY_EDITOR
                if (!(asset is GameObject)) // GameObject 不能单独卸载
                {
                    Resources.UnloadAsset(asset);
                }
#endif
                
                CYLog.Debug($"[ResourceLoader] 卸载: {path}");
            }
        }
        
        /// <summary>
        /// 卸载未使用的资源
        /// ❗ 注意：GC.Collect() 可能导致帧尖刺，默认仅在 Editor/Development 下执行
        /// 建议在 Loading 场景或明确的内存清理时机调用
        /// </summary>
        /// <param name="forceGC">是否强制执行 GC（Release 下默认不执行，避免帧尖刺）</param>
        public void UnloadUnused(bool forceGC = false)
        {
            Resources.UnloadUnusedAssets();
            
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
            
            if (onComplete != null)
            {
                operation.completed += _ => onComplete();
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
                    
                    if (loaded >= total)
                    {
                        onComplete?.Invoke();
                    }
                });
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
