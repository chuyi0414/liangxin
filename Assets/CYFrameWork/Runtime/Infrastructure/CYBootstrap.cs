// ============================================================================
// CYFramework 2.2 - Bootstrap 框架启动器
// 功能：框架初始化入口、生命周期调度、平台适配
// ============================================================================

using System.Collections.Generic;
using CYFramework.Core.Config;
using CYFramework.Core.Event;
using CYFramework.Core.HotUpdate;
using CYFramework.Core.Network;
using CYFramework.Core.Pool;
using CYFramework.Core.Resource;
using CYFramework.Core.Save;
using CYFramework.Modules.Audio;
using CYFramework.Platform;
using CYFramework.Platform.Unity;
using UnityEngine;

#if CY_WECHAT || UNITY_WEBGL
using CYFramework.Platform.WeChat;
#endif

namespace CYFramework.Infrastructure
{
    /// <summary>
    /// CYFramework 启动器
    /// 挂载到场景中的 GameObject 上，作为框架入口
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CYBootstrap : MonoBehaviour
    {
        [Header("日志配置")]
        [SerializeField] private LogLevel _logLevel = LogLevel.Debug;
        
        [Header("逻辑帧配置")]
        [SerializeField] private int _fixedTickRate = 30;  // 逻辑帧率 (30/60Hz)
        
        [Header("暂停配置")]
        [SerializeField] private float _maxPauseTolerance = 5f;  // 最大暂停容忍时间
        
        // 单例
        public static CYBootstrap Instance { get; private set; }
        
        // 生命周期列表
        private readonly List<ITickable> _tickables = new();
        private readonly List<IUpdateable> _updateables = new();
        private readonly List<ILateUpdateable> _lateUpdateables = new();
        private readonly List<IPausable> _pausables = new();
        
        // 暂停状态
        private float _pauseTimestamp;
        private bool _isPaused;
        
        #region Unity 生命周期
        
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 初始化框架
            InitializeFramework();
        }
        
        private void FixedUpdate()
        {
            if (_isPaused) return;
            
            float dt = Time.fixedDeltaTime;
            
            // 按优先级执行 Tick
            for (int i = 0; i < _tickables.Count; i++)
            {
                _tickables[i].Tick(dt);
            }
        }
        
        private void Update()
        {
            if (_isPaused) return;
            
            float dt = Time.deltaTime;
            
            // 按优先级执行 Update
            for (int i = 0; i < _updateables.Count; i++)
            {
                _updateables[i].OnUpdate(dt);
            }
        }
        
        private void LateUpdate()
        {
            if (_isPaused) return;
            
            float dt = Time.deltaTime;
            
            // 按优先级执行 LateUpdate
            for (int i = 0; i < _lateUpdateables.Count; i++)
            {
                _lateUpdateables[i].OnLateUpdate(dt);
            }
        }
        
        private void OnApplicationPause(bool isPaused)
        {
            HandlePause(isPaused);
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            // WebGL/微信需要同时处理 Focus 事件
            #if UNITY_WEBGL || CY_WECHAT
            HandlePause(!hasFocus);
            #endif
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                ShutdownFramework();
                Instance = null;
            }
        }
        
        #endregion
        
        #region 框架初始化
        
        /// <summary>
        /// 初始化框架
        /// </summary>
        private void InitializeFramework()
        {
            // 1. 初始化日志系统
            CYLog.Initialize(_logLevel);
            CYLog.Info("=== CYFramework 2.2 启动 ===");
            CYLog.Info($"平台: {Application.platform}");
            CYLog.Info($"逻辑帧率: {_fixedTickRate}Hz");
            
            // 2. 设置固定帧率
            Time.fixedDeltaTime = 1f / _fixedTickRate;
            
            // 3. 注册核心服务
            RegisterCoreServices();
            
            // 4. 初始化所有服务
            ServiceLocator.InitializeAll();
            
            // 5. 收集生命周期接口
            CollectLifecycleInterfaces();
            
            // 6. 注册全局异常处理
            RegisterExceptionHandler();
            
            CYLog.Info("=== CYFramework 初始化完成 ===");
        }
        
        /// <summary>
        /// 注册核心服务
        /// 文档：按依赖顺序注册
        /// </summary>
        private void RegisterCoreServices()
        {
            // ========== 平台适配器 ==========
#if CY_WECHAT || UNITY_WEBGL
            // 微信/WebGL 平台
            ServiceLocator.Register<IStorageAdapter, WeChatStorageAdapter>();
            // ServiceLocator.Register<INetworkAdapter, WeChatNetworkAdapter>(); // 需要时启用
            CYLog.Debug("[CYBootstrap] 平台: 微信/WebGL");
#else
            // Native 平台 (PC/Android/iOS)
            ServiceLocator.Register<IFileSystem, UnityFileSystem>();
            ServiceLocator.Register<IStorageAdapter, UnityStorageAdapter>();
            CYLog.Debug("[CYBootstrap] 平台: Native");
#endif
            
            // ========== 核心服务 ==========
            
            // EventBus - 零 GC 事件系统
            ServiceLocator.Register<EventBus, EventBus>();
            
            // PoolManager - 对象池管理
            ServiceLocator.Register<PoolManager, PoolManager>();
            
            // ConfigLoader - 配置加载器
            ServiceLocator.Register<IConfigLoader, ConfigLoader>();
            
            // ResourceLoader - 资源加载器
            ServiceLocator.Register<IResourceLoader, ResourceLoader>();
            
            // NetworkService - 网络服务
            ServiceLocator.Register<NetworkService>(() => new NetworkService());
            
            // SaveService - 存档服务
            ServiceLocator.Register<SaveService>(() => new SaveService());
            
            // HotUpdateService - 热更新服务
            ServiceLocator.Register<IHotUpdateService, HotUpdateService>();
            
            // AudioService - 音频服务（根据平台选择实现）
#if CY_WECHAT || UNITY_WEBGL
            ServiceLocator.Register<IAudioService, WeChatAudioService>();
#else
            ServiceLocator.Register<IAudioService, UnityAudioService>();
#endif
            
            CYLog.Debug("[CYBootstrap] 核心服务注册完成");
        }
        
        /// <summary>
        /// 收集生命周期接口
        /// </summary>
        private void CollectLifecycleInterfaces()
        {
            // 从 ServiceLocator 收集所有实现了生命周期接口的服务
            
            // EventBus
            if (ServiceLocator.TryGet<EventBus>(out var eventBus))
            {
                RegisterLifecycle(eventBus);
            }
            
            // NetworkService
            if (ServiceLocator.TryGet<NetworkService>(out var network))
            {
                RegisterLifecycle(network);
            }
            
            // AudioService
            if (ServiceLocator.TryGet<IAudioService>(out var audio))
            {
                RegisterLifecycle(audio);
            }
        }
        
        /// <summary>
        /// 注册全局异常处理
        /// </summary>
        private void RegisterExceptionHandler()
        {
            Application.logMessageReceived += OnLogMessageReceived;
            
            #if !UNITY_WEBGL
            System.AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            #endif
        }
        
        /// <summary>
        /// 关闭框架
        /// </summary>
        private void ShutdownFramework()
        {
            CYLog.Info("=== CYFramework 关闭 ===");
            
            Application.logMessageReceived -= OnLogMessageReceived;
            
            #if !UNITY_WEBGL
            System.AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            #endif
            
            ServiceLocator.DisposeAll();
            
            _tickables.Clear();
            _updateables.Clear();
            _lateUpdateables.Clear();
            _pausables.Clear();
        }
        
        #endregion
        
        #region 生命周期注册
        
        /// <summary>
        /// 注册生命周期对象
        /// </summary>
        public void RegisterLifecycle(object obj)
        {
            if (obj is ITickable tickable)
            {
                _tickables.Add(tickable);
                _tickables.Sort((a, b) => a.TickOrder.CompareTo(b.TickOrder));
            }
            
            if (obj is IUpdateable updateable)
            {
                _updateables.Add(updateable);
                _updateables.Sort((a, b) => a.UpdateOrder.CompareTo(b.UpdateOrder));
            }
            
            if (obj is ILateUpdateable lateUpdateable)
            {
                _lateUpdateables.Add(lateUpdateable);
                _lateUpdateables.Sort((a, b) => a.LateUpdateOrder.CompareTo(b.LateUpdateOrder));
            }
            
            if (obj is IPausable pausable)
            {
                _pausables.Add(pausable);
            }
        }
        
        /// <summary>
        /// 注销生命周期对象
        /// </summary>
        public void UnregisterLifecycle(object obj)
        {
            if (obj is ITickable tickable)
                _tickables.Remove(tickable);
            
            if (obj is IUpdateable updateable)
                _updateables.Remove(updateable);
            
            if (obj is ILateUpdateable lateUpdateable)
                _lateUpdateables.Remove(lateUpdateable);
            
            if (obj is IPausable pausable)
                _pausables.Remove(pausable);
        }
        
        #endregion
        
        #region 暂停处理
        
        /// <summary>
        /// 处理暂停/恢复
        /// 文档位置：3.1.7 生命周期挂起处理（微信审核红线）
        /// </summary>
        private void HandlePause(bool isPaused)
        {
            if (_isPaused == isPaused) return;
            _isPaused = isPaused;
            
            if (isPaused)
            {
                // 切后台：强制静音 + 暂停
                AudioListener.pause = true;
                Time.timeScale = 0f;
                _pauseTimestamp = Time.realtimeSinceStartup;
                
                CYLog.Debug("[CYBootstrap] 游戏暂停");
                
                // 通知所有 IPausable
                foreach (var pausable in _pausables)
                {
                    pausable.OnPause();
                }
            }
            else
            {
                // 切前台：恢复
                AudioListener.pause = false;
                Time.timeScale = 1f;
                
                float pauseDuration = Time.realtimeSinceStartup - _pauseTimestamp;
                CYLog.Debug($"[CYBootstrap] 游戏恢复，暂停时长: {pauseDuration:F2}s");
                
                // 通知所有 IPausable
                foreach (var pausable in _pausables)
                {
                    pausable.OnResume(pauseDuration);
                }
                
                // 超过阈值需要特殊处理
                if (pauseDuration > _maxPauseTolerance)
                {
                    CYLog.Warning($"[CYBootstrap] 暂停时间过长 ({pauseDuration:F2}s)，可能需要重置逻辑时间");
                }
            }
        }
        
        #endregion
        
        #region 异常处理
        
        /// <summary>
        /// Unity 日志回调
        /// </summary>
        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                CYLog.Fatal($"[Unity Exception] {condition}\n{stackTrace}");
                // TODO: 上报到服务器
            }
        }
        
        /// <summary>
        /// 未处理异常回调
        /// </summary>
        private void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as System.Exception;
            CYLog.Fatal($"[Unhandled Exception] {ex?.Message}\n{ex?.StackTrace}");
            // TODO: 保存现场快照，上报到服务器
        }
        
        #endregion
    }
}
