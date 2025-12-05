// ============================================================================
// CYFramework - Addressables 加载器
// 使用 Unity Addressables 系统加载资源
// 需要安装 Addressables 包：Window > Package Manager > Addressables
// ============================================================================

#if ADDRESSABLES_ENABLED
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace CYFramework.Runtime.Core.Resource
{
    /// <summary>
    /// Addressables 加载器
    /// </summary>
    public class AddressablesLoader : IResourceLoader
    {
        // 资源缓存
        private Dictionary<string, Object> _cache = new Dictionary<string, Object>();
        
        // 加载句柄缓存（用于卸载）
        private Dictionary<string, AsyncOperationHandle> _handles = new Dictionary<string, AsyncOperationHandle>();
        
        // 路径前缀（标签或地址前缀）
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
        /// 设置根路径（Addressables 不需要，忽略）
        /// </summary>
        public void SetRootPath(string path)
        {
            // Addressables 不需要根路径，使用远程配置
        }

        /// <summary>
        /// 获取完整地址
        /// </summary>
        private string GetFullAddress(string path)
        {
            return string.IsNullOrEmpty(_pathPrefix) ? path : _pathPrefix + path;
        }

        /// <summary>
        /// 同步加载（注意：Addressables 同步加载会阻塞，建议用异步）
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            string address = GetFullAddress(path);
            
            // 检查缓存
            if (_cache.TryGetValue(address, out Object cached))
            {
                return cached as T;
            }

            try
            {
                // 同步加载（会阻塞主线程）
                var handle = Addressables.LoadAssetAsync<T>(address);
                T asset = handle.WaitForCompletion();
                
                if (asset != null)
                {
                    _cache[address] = asset;
                    _handles[address] = handle;
                }
                else
                {
                    Log.W("AddressablesLoader", $"加载失败: {address}");
                }
                
                return asset;
            }
            catch (Exception e)
            {
                Log.E("AddressablesLoader", $"加载异常: {address}, {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步加载（推荐）
        /// </summary>
        public void LoadAsync<T>(string path, Action<T> callback) where T : Object
        {
            string address = GetFullAddress(path);
            
            // 检查缓存
            if (_cache.TryGetValue(address, out Object cached))
            {
                callback?.Invoke(cached as T);
                return;
            }

            // 异步加载
            var handle = Addressables.LoadAssetAsync<T>(address);
            handle.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    _cache[address] = op.Result;
                    _handles[address] = handle;
                    callback?.Invoke(op.Result);
                }
                else
                {
                    Log.W("AddressablesLoader", $"异步加载失败: {address}");
                    callback?.Invoke(null);
                }
            };
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(string path)
        {
            string address = GetFullAddress(path);
            
            if (_handles.TryGetValue(address, out AsyncOperationHandle handle))
            {
                Addressables.Release(handle);
                _handles.Remove(address);
            }
            _cache.Remove(address);
        }

        /// <summary>
        /// 卸载所有
        /// </summary>
        public void UnloadAll()
        {
            foreach (var handle in _handles.Values)
            {
                Addressables.Release(handle);
            }
            _handles.Clear();
            _cache.Clear();
        }

        /// <summary>
        /// 是否已加载
        /// </summary>
        public bool IsLoaded(string path)
        {
            return _cache.ContainsKey(GetFullAddress(path));
        }

        /// <summary>
        /// 缓存数量
        /// </summary>
        public int CachedCount => _cache.Count;
    }
}
#endif
