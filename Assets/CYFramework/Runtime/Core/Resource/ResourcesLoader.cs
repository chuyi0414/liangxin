// ============================================================================
// CYFramework - Unity Resources 加载器
// 使用 Unity 内置的 Resources 系统加载资源
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CYFramework.Runtime.Core.Resource
{
    /// <summary>
    /// Unity Resources 加载器
    /// 资源需要放在 Assets/Resources/ 目录下
    /// </summary>
    public class ResourcesLoader : IResourceLoader
    {
        // 资源缓存
        private Dictionary<string, Object> _cache = new Dictionary<string, Object>();
        
        // 路径前缀
        private string _pathPrefix = "";

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
        /// 设置根路径（Resources 不需要，忽略）
        /// </summary>
        public void SetRootPath(string path)
        {
            // Resources 加载器不需要根路径
        }

        /// <summary>
        /// 获取完整路径
        /// </summary>
        private string GetFullPath(string path)
        {
            return string.IsNullOrEmpty(_pathPrefix) ? path : _pathPrefix + path;
        }

        /// <summary>
        /// 同步加载
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            string fullPath = GetFullPath(path);
            
            // 检查缓存
            if (_cache.TryGetValue(fullPath, out Object cached))
            {
                return cached as T;
            }

            // 加载资源
            T asset = Resources.Load<T>(fullPath);
            if (asset != null)
            {
                _cache[fullPath] = asset;
            }
            else
            {
                Log.W("ResourcesLoader", $"加载失败: {fullPath}");
            }

            return asset;
        }

        /// <summary>
        /// 异步加载
        /// </summary>
        public void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            string fullPath = GetFullPath(path);
            
            // 检查缓存
            if (_cache.TryGetValue(fullPath, out Object cached))
            {
                callback?.Invoke(cached as T);
                return;
            }

            // 异步加载
            ResourceRequest request = Resources.LoadAsync<T>(fullPath);
            
            // 使用协程等待完成
            CoroutineRunner.Instance.StartCoroutine(WaitForLoad(request, fullPath, callback));
        }

        private IEnumerator WaitForLoad<T>(ResourceRequest request, string path, Action<T> callback) where T : Object
        {
            yield return request;

            T asset = request.asset as T;
            if (asset != null)
            {
                _cache[path] = asset;
            }
            else
            {
                Log.W("ResourcesLoader", $"异步加载失败: {path}");
            }

            callback?.Invoke(asset);
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(string path)
        {
            if (_cache.TryGetValue(path, out Object asset))
            {
                _cache.Remove(path);
                // Resources.UnloadAsset 只能卸载非 GameObject 资源
                if (!(asset is GameObject))
                {
                    Resources.UnloadAsset(asset);
                }
            }
        }

        /// <summary>
        /// 卸载所有
        /// </summary>
        public void UnloadAll()
        {
            _cache.Clear();
            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 是否已加载
        /// </summary>
        public bool IsLoaded(string path)
        {
            return _cache.ContainsKey(path);
        }

        /// <summary>
        /// 缓存数量
        /// </summary>
        public int CachedCount => _cache.Count;
    }

    /// <summary>
    /// 协程运行器（用于在非 MonoBehaviour 中启动协程）
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[CoroutineRunner]");
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunner>();
                }
                return _instance;
            }
        }
    }
}
