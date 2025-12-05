// ============================================================================
// CYFramework - 资源模块
// 提供统一的资源加载接口，支持同步/异步加载
// 
// 设计要点：
// - 统一的加载接口，屏蔽底层实现
// - 支持切换加载器（Resources/AssetBundle/Addressables）
// - 支持同步和异步加载
// ============================================================================

using System;
using UnityEngine;
using CYFramework.Runtime.Core.Resource;
using Object = UnityEngine.Object;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 资源模块
    /// </summary>
    public class ResourceModule : IModule
    {
        public int Priority => 15;
        public bool NeedUpdate => false;

        // 当前使用的加载器
        private IResourceLoader _loader;
        
        // 当前加载器类型
        private ResourceLoaderType _loaderType;

        public void Initialize()
        {
            // 默认使用 Resources 加载器
            SetLoader(ResourceLoaderType.Resources);
            Log.I("ResourceModule", "初始化完成");
        }

        public void Update(float deltaTime) { }

        public void Shutdown()
        {
            UnloadAll();
            Log.I("ResourceModule", "已关闭");
        }

        // ====================================================================
        // 加载器管理
        // ====================================================================

        /// <summary>
        /// 当前加载器类型
        /// </summary>
        public ResourceLoaderType LoaderType => _loaderType;

        /// <summary>
        /// 设置加载器类型
        /// </summary>
        public void SetLoader(ResourceLoaderType type)
        {
            switch (type)
            {
                case ResourceLoaderType.Resources:
                    _loader = new ResourcesLoader();
                    break;
                case ResourceLoaderType.AssetBundle:
                    _loader = new AssetBundleLoader();
                    break;
                case ResourceLoaderType.Addressables:
#if ADDRESSABLES_ENABLED
                    _loader = new AddressablesLoader();
#else
                    Log.W("ResourceModule", "Addressables 未安装，回退到 Resources");
                    _loader = new ResourcesLoader();
                    type = ResourceLoaderType.Resources;
#endif
                    break;
                default:
                    Log.W("ResourceModule", $"未知的加载器类型: {type}，使用默认 Resources");
                    _loader = new ResourcesLoader();
                    break;
            }
            _loaderType = type;
            Log.I("ResourceModule", $"切换加载器: {type}");
        }

        /// <summary>
        /// 设置自定义加载器
        /// </summary>
        public void SetLoader(IResourceLoader loader)
        {
            _loader = loader ?? new ResourcesLoader();
            _loaderType = ResourceLoaderType.Custom;
            Log.I("ResourceModule", "使用自定义加载器");
        }

        // ====================================================================
        // 同步加载
        // ====================================================================

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <returns>资源对象</returns>
        public T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
                return null;

            return _loader.Load<T>(path);
        }

        /// <summary>
        /// 同步加载 GameObject 并实例化
        /// </summary>
        public GameObject LoadAndInstantiate(string path, Transform parent = null)
        {
            GameObject prefab = Load<GameObject>(path);
            if (prefab == null)
            {
                Log.W("ResourceModule", $"加载失败: {path}");
                return null;
            }

            return Object.Instantiate(prefab, parent);
        }

        // ====================================================================
        // 异步加载
        // ====================================================================

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="callback">加载完成回调</param>
        public void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                callback?.Invoke(null);
                return;
            }

            _loader.LoadAsync(path, callback);
        }

        /// <summary>
        /// 异步加载并实例化
        /// </summary>
        public void LoadAndInstantiateAsync(string path, Action<GameObject> callback, Transform parent = null)
        {
            LoadAsync<GameObject>(path, prefab =>
            {
                if (prefab == null)
                {
                    Log.W("ResourceModule", $"异步加载失败: {path}");
                    callback?.Invoke(null);
                    return;
                }

                GameObject instance = Object.Instantiate(prefab, parent);
                callback?.Invoke(instance);
            });
        }

        // ====================================================================
        // 卸载
        // ====================================================================

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(string path)
        {
            _loader?.Unload(path);
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        public void UnloadAll()
        {
            _loader?.UnloadAll();
        }

        /// <summary>
        /// 检查资源是否已加载
        /// </summary>
        public bool IsCached(string path)
        {
            return _loader?.IsLoaded(path) ?? false;
        }

        /// <summary>
        /// 获取当前加载器
        /// </summary>
        public IResourceLoader Loader => _loader;

        /// <summary>
        /// 获取缓存数量
        /// </summary>
        public int CachedCount => _loader?.CachedCount ?? 0;
    }
}
