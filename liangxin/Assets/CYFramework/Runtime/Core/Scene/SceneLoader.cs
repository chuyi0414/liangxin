// ============================================================================
// CYFramework 2.2 - 场景加载器
// 功能：场景加载、切换、进度回调
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using CYFramework.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CYFramework.Core.Scene
{
    /// <summary>
    /// 场景加载模式
    /// </summary>
    public enum SceneLoadMode
    {
        /// <summary>单独加载（卸载当前场景）</summary>
        Single,
        /// <summary>叠加加载（保留当前场景）</summary>
        Additive
    }
    
    /// <summary>
    /// 场景加载进度
    /// </summary>
    public struct SceneLoadProgress
    {
        /// <summary>
        /// 场景名称
        /// </summary>
        public string SceneName;
        /// <summary>
        /// 进度（0~1）
        /// </summary>
        public float Progress;
        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsDone;
    }
    
    /// <summary>
    /// 场景加载器
    /// </summary>
    public class SceneLoader : IInitializable, IDisposableEx
    {
        /// <summary>
        /// 已加载场景表
        /// </summary>
        private readonly Dictionary<string, UnityEngine.SceneManagement.Scene> _loadedScenes = new();
        /// <summary>
        /// 当前场景名称
        /// </summary>
        private string _currentSceneName;
        /// <summary>
        /// 是否正在加载
        /// </summary>
        private bool _isLoading;
        
        /// <summary>
        /// 初始化顺序
        /// </summary>
        public int InitOrder => 20;
        /// <summary>
        /// 释放顺序
        /// </summary>
        public int DisposeOrder => 20;
        
        /// <summary>当前场景名称</summary>
        public string CurrentSceneName => _currentSceneName;
        
        /// <summary>是否正在加载</summary>
        public bool IsLoading => _isLoading;
        
        /// <summary>场景加载完成事件</summary>
        public event Action<string> OnSceneLoaded;
        
        /// <summary>场景卸载完成事件</summary>
        public event Action<string> OnSceneUnloaded;
        
        /// <summary>
        /// 初始化场景加载器
        /// </summary>
        public void Initialize()
        {
            _currentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            CYLog.Debug($"[SceneLoader] 初始化完成，当前场景: {_currentSceneName}");
        }
        
        /// <summary>
        /// 释放场景加载器
        /// </summary>
        public void Dispose()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _loadedScenes.Clear();
            CYLog.Debug("[SceneLoader] 已销毁");
        }
        
        #region 同步加载
        
        /// <summary>
        /// 同步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式</param>
        public void LoadScene(string sceneName, SceneLoadMode mode = SceneLoadMode.Single)
        {
            // Unity 场景加载模式
            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;
            
            // 异步加载操作
            SceneManager.LoadScene(sceneName, loadMode);
            
            if (mode == SceneLoadMode.Single)
            {
                _currentSceneName = sceneName;
            }
            
            CYLog.Info($"[SceneLoader] 同步加载场景: {sceneName}");
        }
        
        #endregion
        
        #region 异步加载
        
        /// <summary>
        /// 当前加载协程
        /// </summary>
        private Coroutine _currentLoadCoroutine;
        /// <summary>
        /// 是否请求取消加载
        /// </summary>
        private bool _cancelRequested;
        
        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="onProgress">进度回调 (0-1)</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="mode">加载模式</param>
        /// <param name="onError">错误回调</param>
        public void LoadSceneAsync(string sceneName, Action<float> onProgress = null, 
            Action onComplete = null, SceneLoadMode mode = SceneLoadMode.Single,
            Action<string> onError = null)
        {
            if (_isLoading)
            {
                CYLog.Warning("[SceneLoader] 正在加载场景中，请等待");
                onError?.Invoke("正在加载场景中");
                return;
            }
            
            _cancelRequested = false;
            _currentLoadCoroutine = CYBootstrap.Instance?.StartCoroutine(
                LoadSceneCoroutine(sceneName, onProgress, onComplete, mode, onError));
        }
        
        /// <summary>
        /// 取消当前加载
        /// </summary>
        /// <remarks>
        /// 重要：Unity 的场景异步加载 <see cref="AsyncOperation"/> 不支持真正“中断/取消下载”。
        /// 本接口的语义是：
        /// - 尽快取消进度/完成回调（避免业务误触发后续逻辑）
        /// - 防止 asyncOp 因 allowSceneActivation=false 而卡在 0.9 的“半加载”状态
        /// - Additive 模式下会在加载完成后尝试卸载该场景以回收资源
        /// </remarks>
        public void CancelLoading()
        {
            if (_isLoading && _currentLoadCoroutine != null)
            {
                _cancelRequested = true;
                CYLog.Debug("[SceneLoader] 取消加载请求已发送");
            }
        }
        
        /// <summary>
        /// 场景加载协程
        /// </summary>
        private IEnumerator LoadSceneCoroutine(string sceneName, Action<float> onProgress,
            Action onComplete, SceneLoadMode mode, Action<string> onError = null)
        {
            _isLoading = true;
            // 是否被取消
            bool canceled = false;
            
            // Unity 场景加载模式
            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;
            
            // 异步加载操作
            AsyncOperation asyncOp = null;
            
            try
            {
                asyncOp = SceneManager.LoadSceneAsync(sceneName, loadMode);
            }
            catch (System.Exception ex)
            {
                _isLoading = false;
                CYLog.Error($"[SceneLoader] 加载场景失败: {sceneName}", ex);
                onError?.Invoke(ex.Message);
                yield break;
            }
            
            if (asyncOp == null)
            {
                _isLoading = false;
                CYLog.Error($"[SceneLoader] 场景不存在: {sceneName}");
                onError?.Invoke($"场景不存在: {sceneName}");
                yield break;
            }
            
            asyncOp.allowSceneActivation = false;
            
            CYLog.Info($"[SceneLoader] 开始异步加载: {sceneName}");
            
            while (!asyncOp.isDone)
            {
                // 检查取消请求
                if (_cancelRequested)
                {
                    _cancelRequested = false;
                    canceled = true;
                    CYLog.Warning($"[SceneLoader] 收到取消请求: {sceneName}（注意：Unity 不支持真正取消加载，框架将取消回调并尽量回收）");
                }

                // 取消后不再回调业务进度，避免 UI/流程误触发；但必须允许激活，避免卡在 0.9。
                if (!canceled)
                {
                    // 归一化进度
                    float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                    onProgress?.Invoke(progress);

                    if (asyncOp.progress >= 0.9f)
                    {
                        asyncOp.allowSceneActivation = true;
                    }
                }
                else
                {
                    asyncOp.allowSceneActivation = true;
                }
                
                yield return null;
            }

            // Single 模式下即使取消，场景也可能已经完成激活：这里以实际加载目标为准更新当前场景名。
            if (mode == SceneLoadMode.Single)
            {
                _currentSceneName = sceneName;
            }

            _isLoading = false;
            _currentLoadCoroutine = null;

            if (canceled)
            {
                // Additive 模式下尝试卸载，尽量回收资源；Single 模式无法回滚，只能提示。
                if (mode == SceneLoadMode.Additive)
                {
                    // 卸载操作
                    var unloadOp = SceneManager.UnloadSceneAsync(sceneName);
                    if (unloadOp != null)
                    {
                        while (!unloadOp.isDone)
                        {
                            yield return null;
                        }
                    }
                }

                onError?.Invoke("加载已取消");
                yield break;
            }

            onProgress?.Invoke(1f);
            onComplete?.Invoke();
            
            CYLog.Info($"[SceneLoader] 场景加载完成: {sceneName}");
        }
        
        /// <summary>
        /// 异步加载场景（带 Loading 界面）
        /// </summary>
        public void LoadSceneWithLoading<TLoadingUI>(string sceneName, Action onComplete = null) 
            where TLoadingUI : UI.UIPanel
        {
            // 显示 Loading 界面
            // Loading 面板实例
            var loadingUI = CY.UI?.Open<TLoadingUI>();
            
            LoadSceneAsync(sceneName, progress =>
            {
                // 更新 Loading 进度（如果 Loading UI 有进度接口）
                if (loadingUI is ILoadingProgress loadingProgress)
                {
                    // Loading 进度接口
                    loadingProgress.SetProgress(progress);
                }
            }, () =>
            {
                // 关闭 Loading 界面
                CY.UI?.Close<TLoadingUI>();
                onComplete?.Invoke();
            });
        }
        
        #endregion
        
        #region 场景管理
        
        /// <summary>
        /// 卸载场景
        /// </summary>
        public void UnloadScene(string sceneName, Action onComplete = null)
        {
            CYBootstrap.Instance?.StartCoroutine(UnloadSceneCoroutine(sceneName, onComplete));
        }
        
        /// <summary>
        /// 场景卸载协程
        /// </summary>
        private IEnumerator UnloadSceneCoroutine(string sceneName, Action onComplete)
        {
            // 卸载操作
            var asyncOp = SceneManager.UnloadSceneAsync(sceneName);
            
            if (asyncOp == null)
            {
                CYLog.Warning($"[SceneLoader] 无法卸载场景: {sceneName}");
                yield break;
            }
            
            while (!asyncOp.isDone)
            {
                yield return null;
            }
            
            _loadedScenes.Remove(sceneName);
            onComplete?.Invoke();
            
            CYLog.Info($"[SceneLoader] 场景卸载完成: {sceneName}");
        }
        
        /// <summary>
        /// 重新加载当前场景
        /// </summary>
        public void ReloadCurrentScene(Action onComplete = null)
        {
            LoadSceneAsync(_currentSceneName, null, onComplete);
        }
        
        /// <summary>
        /// 检查场景是否已加载
        /// </summary>
        public bool IsSceneLoaded(string sceneName)
        {
            // i 为索引
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == sceneName)
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 获取已加载的场景数量
        /// </summary>
        public int LoadedSceneCount => SceneManager.sceneCount;
        
        /// <summary>
        /// 设置活动场景
        /// </summary>
        public void SetActiveScene(string sceneName)
        {
            // 目标场景
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
                _currentSceneName = sceneName;
            }
        }
        
        #endregion
        
        #region 事件处理
        
        /// <summary>
        /// 场景加载完成回调
        /// </summary>
        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            _loadedScenes[scene.name] = scene;
            OnSceneLoaded?.Invoke(scene.name);
        }
        
        /// <summary>
        /// 场景卸载完成回调
        /// </summary>
        private void HandleSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            _loadedScenes.Remove(scene.name);
            OnSceneUnloaded?.Invoke(scene.name);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Loading 进度接口
    /// 实现此接口的 UI 面板可以接收加载进度
    /// </summary>
    public interface ILoadingProgress
    {
        void SetProgress(float progress);
    }
}
