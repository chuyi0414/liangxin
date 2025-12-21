// ============================================================================
// CYFramework 2.2 - 配置加载器
// 文档位置：3.1.1 配置烘焙管线 (Config Baking Pipeline)
// 功能：No-Baking Mode，Editor 直读 SO，Runtime 读烘焙数据
// ============================================================================

using System;
using System.Collections.Generic;
using CYFramework.Infrastructure;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CYFramework.Core.Config
{
    /// <summary>
    /// 配置加载器接口
    /// </summary>
    public interface IConfigLoader
    {
        /// <summary>
        /// 加载配置
        /// </summary>
        T Load<T>(string path) where T : ScriptableObject;
        
        /// <summary>
        /// 异步加载配置
        /// </summary>
        void LoadAsync<T>(string path, Action<T> callback) where T : ScriptableObject;
        
        /// <summary>
        /// 预加载配置
        /// </summary>
        void Preload(string[] paths);
        
        /// <summary>
        /// 卸载配置
        /// </summary>
        void Unload(string path);
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        void ClearCache();
    }
    
    /// <summary>
    /// 配置加载器
    /// 文档：No-Baking Mode
    /// - Editor: 直读 SO 引用
    /// - Development Build: 直读 SO
    /// - Release Build: 读 BlobAsset（预留）
    /// </summary>
    public class ConfigLoader : IConfigLoader, IInitializable, IDisposableEx
    {
        /// <summary>
        /// 配置缓存
        /// </summary>
        private readonly Dictionary<string, ScriptableObject> _cache = new();
        
        // 配置根路径
        // 配置根路径（由 ResourceLoaderConfig.ConfigPath 驱动）
        // - Editor: 用于 AssetDatabase.LoadAssetAtPath 的前缀路径（必须是 Assets/...）
        // - Runtime: 用于 Resources.Load 的前缀路径（相对于 Resources）
        /// <summary>
        /// Editor 下的资源路径前缀
        /// </summary>
        private string _assetPathPrefix = "Assets/Resources/Config/";
        /// <summary>
        /// Runtime 下的 Resources 路径前缀
        /// </summary>
        private string _resourcesPathPrefix = "Config/";
        
        /// <summary>
        /// 初始化优先级
        /// </summary>
        public int InitOrder => -50;
        /// <summary>
        /// 销毁优先级
        /// </summary>
        public int DisposeOrder => 50;
        
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            // 从 CYConfigurator 读取配置路径前缀（保持与 ResourceLoaderConfig 一致）
            // 配置器实例
            var configurator = CYConfigurator.Instance;
            if (configurator != null)
            {
                // 资源加载配置
                var resourceConfig = configurator.GetConfig<ResourceLoaderConfig>();
                if (resourceConfig != null && !string.IsNullOrEmpty(resourceConfig.ConfigPath))
                {
                    // 统一末尾斜杠，避免拼接出错
                    _resourcesPathPrefix = EnsureEndsWithSlash(resourceConfig.ConfigPath);
                    _assetPathPrefix = "Assets/Resources/" + _resourcesPathPrefix;
                }
            }
            CYLog.Debug("[ConfigLoader] 初始化完成");
        }
        
        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            ClearCache();
            CYLog.Debug("[ConfigLoader] 已销毁");
        }
        
        /// <summary>
        /// 加载配置
        /// 文档位置：3.1.1
        /// </summary>
        public T Load<T>(string path) where T : ScriptableObject
        {
            // 检查缓存
            if (_cache.TryGetValue(path, out var cached)) // cached 为缓存对象
            {
                return cached as T;
            }
            
            // 配置实例
            T config = null;
            
#if UNITY_EDITOR
            // Editor: 直接读 SO，无需烘焙
            // 资源完整路径
            string fullPath = path.StartsWith("Assets/") ? path : _assetPathPrefix + path;
            if (!fullPath.EndsWith(".asset"))
            {
                fullPath += ".asset";
            }
            
            config = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            
            if (config == null)
            {
                CYLog.Warning($"[ConfigLoader] Editor 模式加载失败: {fullPath}");
            }
#else
            // Runtime: 从 Resources 加载（简化版）
            // 生产环境可替换为 Addressables 或 BlobAsset
            // Resources 路径
            string resourcePath = path.StartsWith(_resourcesPathPrefix) ? path : _resourcesPathPrefix + path;
            resourcePath = resourcePath.Replace(".asset", "");
            
            config = Resources.Load<T>(resourcePath);
            
            if (config == null)
            {
                CYLog.Warning($"[ConfigLoader] Runtime 模式加载失败: {resourcePath}");
            }
#endif
            
            if (config != null)
            {
                _cache[path] = config;
                CYLog.Debug($"[ConfigLoader] 加载成功: {path}");
            }
            
            return config;
        }
        
        /// <summary>
        /// 异步加载配置
        /// </summary>
        public void LoadAsync<T>(string path, Action<T> callback) where T : ScriptableObject
        {
            // 检查缓存
            if (_cache.TryGetValue(path, out var cached)) // cached 为缓存对象
            {
                callback?.Invoke(cached as T);
                return;
            }
            
#if UNITY_EDITOR
            // Editor 下同步加载
            // 配置实例
            var config = Load<T>(path);
            callback?.Invoke(config);
#else
            // Runtime: 使用 Resources.LoadAsync
            // Resources 路径
            string resourcePath = path.StartsWith(_resourcesPathPrefix) ? path : _resourcesPathPrefix + path;
            resourcePath = resourcePath.Replace(".asset", "");
            
            // 异步请求
            var request = Resources.LoadAsync<T>(resourcePath);
            request.completed += _ =>
            {
                // 配置实例
                var config = request.asset as T;
                if (config != null)
                {
                    _cache[path] = config;
                }
                callback?.Invoke(config);
            };
#endif
        }
        
        /// <summary>
        /// 预加载配置
        /// </summary>
        public void Preload(string[] paths)
        {
            foreach (var path in paths) // path 为配置路径
            {
                Load<ScriptableObject>(path);
            }
            
            CYLog.Debug($"[ConfigLoader] 预加载完成，共 {paths.Length} 个配置");
        }
        
        /// <summary>
        /// 卸载配置
        /// </summary>
        public void Unload(string path)
        {
            if (_cache.TryGetValue(path, out var config)) // config 为缓存对象
            {
                _cache.Remove(path);
                
#if !UNITY_EDITOR
                Resources.UnloadAsset(config);
#endif
            }
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            
#if !UNITY_EDITOR
            Resources.UnloadUnusedAssets();
#endif
            
            CYLog.Debug("[ConfigLoader] 缓存已清空");
        }

        /// <summary>
        /// 确保路径以斜杠结尾
        /// </summary>
        /// <param name="path">原始路径</param>
        /// <returns>处理后的路径</returns>
        private static string EnsureEndsWithSlash(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.EndsWith("/") ? path : path + "/";
        }
    }
}
