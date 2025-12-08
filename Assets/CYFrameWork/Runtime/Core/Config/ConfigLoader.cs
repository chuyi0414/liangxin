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
        // 配置缓存
        private readonly Dictionary<string, ScriptableObject> _cache = new();
        
        // 配置根路径
        private const string CONFIG_PATH_PREFIX = "Assets/Resources/Config/";
        private const string RESOURCES_PREFIX = "Config/";
        
        public int InitOrder => -50;
        public int DisposeOrder => 50;
        
        public void Initialize()
        {
            CYLog.Debug("[ConfigLoader] 初始化完成");
        }
        
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
            if (_cache.TryGetValue(path, out var cached))
            {
                return cached as T;
            }
            
            T config = null;
            
#if UNITY_EDITOR
            // Editor: 直接读 SO，无需烘焙
            string fullPath = path.StartsWith("Assets/") ? path : CONFIG_PATH_PREFIX + path;
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
            string resourcePath = path.StartsWith(RESOURCES_PREFIX) ? path : RESOURCES_PREFIX + path;
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
            if (_cache.TryGetValue(path, out var cached))
            {
                callback?.Invoke(cached as T);
                return;
            }
            
#if UNITY_EDITOR
            // Editor 下同步加载
            var config = Load<T>(path);
            callback?.Invoke(config);
#else
            // Runtime: 使用 Resources.LoadAsync
            string resourcePath = path.StartsWith(RESOURCES_PREFIX) ? path : RESOURCES_PREFIX + path;
            resourcePath = resourcePath.Replace(".asset", "");
            
            var request = Resources.LoadAsync<T>(resourcePath);
            
            // 使用协程等待（需要 MonoBehaviour）
            // 简化实现：直接完成
            if (request.isDone)
            {
                var config = request.asset as T;
                if (config != null)
                {
                    _cache[path] = config;
                }
                callback?.Invoke(config);
            }
#endif
        }
        
        /// <summary>
        /// 预加载配置
        /// </summary>
        public void Preload(string[] paths)
        {
            foreach (var path in paths)
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
            if (_cache.TryGetValue(path, out var config))
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
    }
}
