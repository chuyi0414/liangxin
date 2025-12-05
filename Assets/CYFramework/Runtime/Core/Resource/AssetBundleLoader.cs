// ============================================================================
// CYFramework - AssetBundle 加载器
// 使用 AssetBundle 加载资源（需要自行实现 AB 打包和管理）
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CYFramework.Runtime.Core.Resource
{
    /// <summary>
    /// AssetBundle 加载器
    /// 这是一个基础模板，实际项目需要根据 AB 打包策略完善
    /// </summary>
    public class AssetBundleLoader : IResourceLoader
    {
        // 已加载的 AssetBundle
        private Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();
        
        // 资源到 Bundle 的映射（需要从 manifest 或配置文件读取）
        private Dictionary<string, string> _assetToBundleMap = new Dictionary<string, string>();
        
        // 已加载的资源缓存
        private Dictionary<string, Object> _assetCache = new Dictionary<string, Object>();

        // AB 文件存放路径
        private string _bundlePath;
        
        // 路径前缀
        private string _pathPrefix = "";

        public AssetBundleLoader(string bundlePath = null)
        {
            // 默认路径：StreamingAssets/Bundles
            _bundlePath = bundlePath ?? Application.streamingAssetsPath + "/Bundles";
        }

        /// <summary>
        /// 设置路径前缀
        /// </summary>
        public void SetPathPrefix(string prefix)
        {
            _pathPrefix = prefix ?? "";
            if (!string.IsNullOrEmpty(_pathPrefix) && !_pathPrefix.EndsWith("/"))
            {
                _pathPrefix += "/";
            }
        }

        /// <summary>
        /// 设置根路径（AssetBundle 存放路径）
        /// </summary>
        public void SetRootPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                _bundlePath = Application.streamingAssetsPath + "/" + path;
            }
        }

        /// <summary>
        /// 获取完整路径
        /// </summary>
        private string GetFullPath(string path)
        {
            return string.IsNullOrEmpty(_pathPrefix) ? path : _pathPrefix + path;
        }

        /// <summary>
        /// 初始化（加载 manifest 等）
        /// </summary>
        public void Initialize()
        {
            // TODO: 加载 AssetBundle manifest
            // TODO: 构建 _assetToBundleMap 映射
            Log.I("AssetBundleLoader", "初始化完成");
        }

        /// <summary>
        /// 同步加载
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            // 检查缓存
            if (_assetCache.TryGetValue(path, out Object cached))
            {
                return cached as T;
            }

            // 获取资源所在的 Bundle
            if (!_assetToBundleMap.TryGetValue(path, out string bundleName))
            {
                Log.W("AssetBundleLoader", $"找不到资源对应的 Bundle: {path}");
                return null;
            }

            // 加载 Bundle
            AssetBundle bundle = LoadBundle(bundleName);
            if (bundle == null)
            {
                return null;
            }

            // 从 Bundle 加载资源
            T asset = bundle.LoadAsset<T>(path);
            if (asset != null)
            {
                _assetCache[path] = asset;
            }

            return asset;
        }

        /// <summary>
        /// 异步加载
        /// </summary>
        public void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            // 检查缓存
            if (_assetCache.TryGetValue(path, out Object cached))
            {
                callback?.Invoke(cached as T);
                return;
            }

            // TODO: 实现异步加载
            // 1. 异步加载 Bundle
            // 2. 异步从 Bundle 加载资源
            // 3. 缓存并回调

            Log.W("AssetBundleLoader", "异步加载暂未实现，使用同步加载");
            T asset = Load<T>(path);
            callback?.Invoke(asset);
        }

        /// <summary>
        /// 加载 Bundle
        /// </summary>
        private AssetBundle LoadBundle(string bundleName)
        {
            if (_loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
            {
                return bundle;
            }

            string bundlePath = $"{_bundlePath}/{bundleName}";
            bundle = AssetBundle.LoadFromFile(bundlePath);

            if (bundle != null)
            {
                _loadedBundles[bundleName] = bundle;
            }
            else
            {
                Log.E("AssetBundleLoader", $"加载 Bundle 失败: {bundlePath}");
            }

            return bundle;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(string path)
        {
            _assetCache.Remove(path);
        }

        /// <summary>
        /// 卸载所有
        /// </summary>
        public void UnloadAll()
        {
            _assetCache.Clear();

            foreach (var bundle in _loadedBundles.Values)
            {
                bundle.Unload(true);
            }
            _loadedBundles.Clear();
        }

        /// <summary>
        /// 是否已加载
        /// </summary>
        public bool IsLoaded(string path)
        {
            return _assetCache.ContainsKey(path);
        }

        /// <summary>
        /// 注册资源到 Bundle 的映射
        /// </summary>
        public void RegisterAsset(string assetPath, string bundleName)
        {
            _assetToBundleMap[assetPath] = bundleName;
        }

        /// <summary>
        /// 缓存数量
        /// </summary>
        public int CachedCount => _assetCache.Count;
    }
}
