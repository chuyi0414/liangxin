// ============================================================================
// CYFramework - 资源加载器接口
// 抽象资源加载方式，支持 Resources / AssetBundle / Addressables 等
// ============================================================================

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CYFramework.Runtime.Core.Resource
{
    /// <summary>
    /// 资源加载器接口
    /// 不同的加载方式实现此接口
    /// </summary>
    public interface IResourceLoader
    {
        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <returns>加载的资源，失败返回 null</returns>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="callback">加载完成回调</param>
        void LoadAsync<T>(string path, Action<T> callback) where T : Object;

        /// <summary>
        /// 卸载资源
        /// </summary>
        /// <param name="path">资源路径</param>
        void Unload(string path);

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        void UnloadAll();

        /// <summary>
        /// 检查资源是否已加载
        /// </summary>
        bool IsLoaded(string path);

        /// <summary>
        /// 获取已缓存的资源数量
        /// </summary>
        int CachedCount { get; }

        /// <summary>
        /// 设置路径前缀
        /// </summary>
        void SetPathPrefix(string prefix);

        /// <summary>
        /// 设置根路径（如 AssetBundle 路径）
        /// </summary>
        void SetRootPath(string path);
    }

    /// <summary>
    /// 资源加载类型
    /// </summary>
    public enum ResourceLoaderType
    {
        /// <summary>
        /// Unity Resources（默认）
        /// </summary>
        Resources,

        /// <summary>
        /// AssetBundle
        /// </summary>
        AssetBundle,

        /// <summary>
        /// Addressables
        /// </summary>
        Addressables,

        /// <summary>
        /// 自定义
        /// </summary>
        Custom
    }
}
