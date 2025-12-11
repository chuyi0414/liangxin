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
        public string SceneName;
        public float Progress;
        public bool IsDone;
    }
    
    /// <summary>
    /// 场景加载器
    /// </summary>
    public class SceneLoader : IInitializable, IDisposableEx
    {
        private readonly Dictionary<string, UnityEngine.SceneManagement.Scene> _loadedScenes = new();
        private string _currentSceneName;
        private bool _isLoading;
        
        public int InitOrder => 20;
        public int DisposeOrder => 20;
        
        /// <summary>当前场景名称</summary>
        public string CurrentSceneName => _currentSceneName;
        
        /// <summary>是否正在加载</summary>
        public bool IsLoading => _isLoading;
        
        /// <summary>场景加载完成事件</summary>
        public event Action<string> OnSceneLoaded;
        
        /// <summary>场景卸载完成事件</summary>
        public event Action<string> OnSceneUnloaded;
        
        public void Initialize()
        {
            _currentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            CYLog.Debug($"[SceneLoader] 初始化完成，当前场景: {_currentSceneName}");
        }
        
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
            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;
            
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
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="onProgress">进度回调 (0-1)</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="mode">加载模式</param>
        public void LoadSceneAsync(string sceneName, Action<float> onProgress = null, 
            Action onComplete = null, SceneLoadMode mode = SceneLoadMode.Single)
        {
            if (_isLoading)
            {
                CYLog.Warning("[SceneLoader] 正在加载场景中，请等待");
                return;
            }
            
            CYBootstrap.Instance?.StartCoroutine(
                LoadSceneCoroutine(sceneName, onProgress, onComplete, mode));
        }
        
        private IEnumerator LoadSceneCoroutine(string sceneName, Action<float> onProgress, 
            Action onComplete, SceneLoadMode mode)
        {
            _isLoading = true;
            
            var loadMode = mode == SceneLoadMode.Single 
                ? LoadSceneMode.Single 
                : LoadSceneMode.Additive;
            
            var asyncOp = SceneManager.LoadSceneAsync(sceneName, loadMode);
            asyncOp.allowSceneActivation = false;
            
            CYLog.Info($"[SceneLoader] 开始异步加载: {sceneName}");
            
            while (!asyncOp.isDone)
            {
                // Unity 异步加载进度在 0.9 时暂停等待 allowSceneActivation
                float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                onProgress?.Invoke(progress);
                
                if (asyncOp.progress >= 0.9f)
                {
                    asyncOp.allowSceneActivation = true;
                }
                
                yield return null;
            }
            
            if (mode == SceneLoadMode.Single)
            {
                _currentSceneName = sceneName;
            }
            
            _isLoading = false;
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
            var loadingUI = CY.UI?.Open<TLoadingUI>();
            
            LoadSceneAsync(sceneName, progress =>
            {
                // 更新 Loading 进度（如果 Loading UI 有进度接口）
                if (loadingUI is ILoadingProgress loadingProgress)
                {
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
        
        private IEnumerator UnloadSceneCoroutine(string sceneName, Action onComplete)
        {
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
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
                _currentSceneName = sceneName;
            }
        }
        
        #endregion
        
        #region 事件处理
        
        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            _loadedScenes[scene.name] = scene;
            OnSceneLoaded?.Invoke(scene.name);
        }
        
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
