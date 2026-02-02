// ============================================================================
// CYFramework 2.2 - Bootstrap 框架启动器
// 功能：框架初始化入口、生命周期调度、平台适配
// ============================================================================

using System.Collections.Generic;
using CYFramework.Core.Config;
using CYFramework.Core.Event;
using CYFramework.Core.HotUpdate;
using CYFramework.Core.Scene;
using CYFramework.Core.FSM;
using CYFramework.Core.Network;
using CYFramework.Core.Pool;
using CYFramework.Core.Procedure;
using CYFramework.Core.Resource;
using CYFramework.Core.Save;
using CYFramework.Core.Timer;
using CYFramework.Core.Audio;
using CYFramework.Core.UI;
using CYFramework.Core.Entity;
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
        [Header("配置来源")]
        /// <summary>
        /// 如果场景中有 CYConfigurator，将自动使用其配置
        /// </summary>
        [Tooltip("如果场景中有 CYConfigurator，将自动使用其配置")]
        /// <summary>
        /// 是否使用场景中的 CYConfigurator
        /// </summary>
        [SerializeField] private bool _useConfigurator = true;
        
        [Header("备用配置 (无 CYConfigurator 时使用)")]
        /// <summary>
        /// 备用日志级别
        /// </summary>
        [SerializeField] private LogLevel _logLevel = LogLevel.Debug;
        /// <summary>
        /// 固定逻辑帧率
        /// </summary>
        [SerializeField] private int _fixedTickRate = 30;
        /// <summary>
        /// 最大暂停容忍时长
        /// </summary>
        [SerializeField] private float _maxPauseTolerance = 5f;
        
        // 配置器引用
        /// <summary>
        /// 配置器引用
        /// </summary>
        private CYConfigurator _configurator;
        
        // 单例
        /// <summary>
        /// 启动器单例
        /// </summary>
        public static CYBootstrap Instance { get; private set; }
        
        // 生命周期列表
        /// <summary>
        /// Tick 生命周期列表
        /// </summary>
        private readonly List<ITickable> _tickables = new();
        /// <summary>
        /// Update 生命周期列表
        /// </summary>
        private readonly List<IUpdateable> _updateables = new();
        /// <summary>
        /// LateUpdate 生命周期列表
        /// </summary>
        private readonly List<ILateUpdateable> _lateUpdateables = new();
        /// <summary>
        /// 可暂停对象列表
        /// </summary>
        private readonly List<IPausable> _pausables = new();
        
        // 暂停状态
        /// <summary>
        /// 暂停开始时间戳
        /// </summary>
        private float _pauseTimestamp;
        /// <summary>
        /// 当前是否暂停
        /// </summary>
        private bool _isPaused;

#if UNITY_EDITOR && !(CY_WECHAT || UNITY_WEBGL)
        /// <summary>
        /// 编辑器震动适配器（Stub）
        /// </summary>
        private class EditorVibrationAdapter : IVibrationAdapter
        {
            /// <summary>
            /// 平台类型
            /// </summary>
            public PlatformType Platform => PlatformType.PC;
            /// <summary>
            /// 是否支持震动
            /// </summary>
            public bool IsSupported => false;
            /// <summary>
            /// 初始化
            /// </summary>
            public void Initialize() { }
            /// <summary>
            /// 短震动
            /// </summary>
            public void VibrateShort() { CYLog.Debug("[EditorVibrationAdapter] VibrateShort"); }
            /// <summary>
            /// 长震动
            /// </summary>
            public void VibrateLong() { CYLog.Debug("[EditorVibrationAdapter] VibrateLong"); }
            /// <summary>
            /// 自定义震动
            /// </summary>
            public void Vibrate(int milliseconds) { CYLog.Debug($"[EditorVibrationAdapter] Vibrate: {milliseconds}ms"); }
        }
#endif
        
        #region Unity 生命周期
        
        /// <summary>
        /// Unity Awake
        /// </summary>
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
        
        /// <summary>
        /// Unity FixedUpdate
        /// </summary>
        private void FixedUpdate()
        {
            if (_isPaused) return;
            
            // 固定时间步长
            float dt = Time.fixedDeltaTime;
            
            // 按优先级执行 Tick
            for (int i = 0; i < _tickables.Count; i++) // i 为索引
            {
                _tickables[i].Tick(dt);
            }
        }
        
        /// <summary>
        /// Unity Update
        /// </summary>
        private void Update()
        {
            if (_isPaused) return;
            
            // 帧时间步长
            float dt = Time.deltaTime;
            
            // 按优先级执行 Update（Timer/Procedure 已通过 IUpdateable 注册）
            for (int i = 0; i < _updateables.Count; i++) // i 为索引
            {
                _updateables[i].OnUpdate(dt);
            }
        }
        
        /// <summary>
        /// Unity LateUpdate
        /// </summary>
        private void LateUpdate()
        {
            if (_isPaused) return;
            
            // 帧时间步长
            float dt = Time.deltaTime;
            
            // 按优先级执行 LateUpdate
            for (int i = 0; i < _lateUpdateables.Count; i++) // i 为索引
            {
                _lateUpdateables[i].OnLateUpdate(dt);
            }
        }
        
        /// <summary>
        /// Unity OnApplicationPause
        /// </summary>
        private void OnApplicationPause(bool isPaused)
        {
            HandlePause(isPaused);
        }
        
        /// <summary>
        /// Unity OnApplicationFocus
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            // WebGL/微信需要同时处理 Focus 事件
            #if UNITY_WEBGL || CY_WECHAT
            HandlePause(!hasFocus);
            #endif
        }
        
        /// <summary>
        /// Unity OnDestroy
        /// </summary>
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
            // 0. 获取配置器
            if (_useConfigurator)
            {
                _configurator = GetComponent<CYConfigurator>();
                if (_configurator == null)
                {
                    _configurator = FindObjectOfType<CYConfigurator>();
                }
            }
            
            // 读取配置
            // 启动器配置
            var bootstrapConfig = _configurator?.GetConfig<BootstrapConfig>();
            // 日志服务配置
            var logConfig = _configurator?.GetConfig<LogServiceConfig>();
            
            // 生效的日志级别
            var logLevel = logConfig?.Level ?? _logLevel;
            // 生效的固定逻辑帧率
            var fixedTickRate = bootstrapConfig?.FixedTickRate ?? _fixedTickRate;
            // 生效的目标帧率
            var targetFrameRate = bootstrapConfig?.TargetFrameRate ?? 60;
            // 生效的垂直同步开关
            var vSync = bootstrapConfig?.VSync ?? false;
            // 生效的后台运行开关
            var runInBackground = bootstrapConfig?.RunInBackground ?? true;
            // 生效的屏幕常亮开关
            var screenNeverSleep = bootstrapConfig?.ScreenNeverSleep ?? true;
            // 生效的暂停容忍时长
            var maxPauseTolerance = bootstrapConfig?.MaxPauseTolerance ?? _maxPauseTolerance;
            _maxPauseTolerance = maxPauseTolerance;
            
            // 1. 初始化日志系统（应用 LogServiceConfig 的所有开关）
            CYLog.Initialize(logLevel);
            if (logConfig != null)
            {
                CYLog.ApplyConfig(logConfig);
            }
            CYLog.Info("=== CYFramework 2.2 启动 ===");
            CYLog.Info($"平台: {Application.platform}");
            CYLog.Info($"逻辑帧率: {fixedTickRate}Hz");
            CYLog.Info($"配置来源: {(_configurator != null ? "CYConfigurator" : "默认配置")}");
            
            // 2. 应用配置
            Time.fixedDeltaTime = 1f / fixedTickRate;
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.runInBackground = runInBackground;
            Screen.sleepTimeout = screenNeverSleep ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            
            // 3. 注册核心服务
            RegisterCoreServices();
            
            // 监听服务动态变动（必须在 InitializeAll 之前）
            ServiceLocator.OnServiceRegistered += RegisterLifecycle;
            ServiceLocator.OnServiceUnregistered += UnregisterLifecycle;
            
            // 4. 初始化所有服务（包括流程自动注册）
            ServiceLocator.InitializeAll();
            
            // 5. 收集生命周期接口 (已通过事件自动收集)
            // 6. 注册全局异常处理
            RegisterExceptionHandler();
            
            // 7. 启动入口流程
            // 流程管理器
            var procedureManager = ServiceLocator.Get<ProcedureManager>();
            procedureManager?.StartEntry();
            
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
            ServiceLocator.Register<INetworkAdapter, WeChatNetworkAdapter>();
            CYLog.Debug("[CYBootstrap] 平台: 微信/WebGL");
#else
            // Native 平台 (PC/Android/iOS)
            ServiceLocator.Register<IFileSystem, UnityFileSystem>();
            ServiceLocator.Register<IStorageAdapter, UnityStorageAdapter>();
            ServiceLocator.Register<INetworkAdapter, UnityNetworkAdapter>();
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
            
            // SceneLoader - 场景加载器
            ServiceLocator.Register<SceneLoader, SceneLoader>();
            
            ServiceLocator.Register<FSMManager, FSMManager>();
            
#if CY_WECHAT || UNITY_WEBGL
            ServiceLocator.Register<IAudioService, WeChatAudioService>();
#else
            ServiceLocator.Register<IAudioService, UnityAudioService>();
#endif
            
            // UIManager - UI 管理器
            ServiceLocator.Register<UIManager, UIManager>();
            
            // ProcedureManager - 流程管理器
            ServiceLocator.Register<ProcedureManager, ProcedureManager>();
            
            // TimerManager - 计时器管理器
            ServiceLocator.Register<TimerManager, TimerManager>();
            
            // EntityManager - 实体管理器
            ServiceLocator.Register<EntityManager, EntityManager>();
            
            // NetworkService - 网络服务
            ServiceLocator.Register<NetworkService>(() => new NetworkService());
            
            // SaveService - 存档服务
            ServiceLocator.Register<SaveService>(() => new SaveService());
            
            // HotUpdateService - 热更新服务
            ServiceLocator.Register<IHotUpdateService, HotUpdateService>();
            
            // VibrationAdapter - 震动适配器
#if CY_WECHAT || UNITY_WEBGL
            ServiceLocator.Register<IVibrationAdapter, WeChatVibrationAdapter>();
#elif UNITY_EDITOR
            ServiceLocator.Register<IVibrationAdapter, EditorVibrationAdapter>();
#elif UNITY_ANDROID || UNITY_IOS
            ServiceLocator.Register<IVibrationAdapter, UnityVibrationAdapter>();
#endif
            
            CYLog.Debug("[CYBootstrap] 核心服务注册完成");
        }
        

        
        /// <summary>
        /// 注册全局异常处理
        /// 文档：WebGL/微信不支持 AppDomain
        /// </summary>
        private void RegisterExceptionHandler()
        {
            Application.logMessageReceived += OnLogMessageReceived;
            
#if !(CY_WECHAT || UNITY_WEBGL)
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
            
#if !(CY_WECHAT || UNITY_WEBGL)
            System.AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
#endif
            
            ServiceLocator.DisposeAll();
            
            ServiceLocator.OnServiceRegistered -= RegisterLifecycle;
            ServiceLocator.OnServiceUnregistered -= UnregisterLifecycle;
            
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
            if (obj is ITickable tickable) // tickable 为 Tick 生命周期对象
            {
                _tickables.Add(tickable);
                _tickables.Sort((a, b) => a.TickOrder.CompareTo(b.TickOrder));
            }
            
            if (obj is IUpdateable updateable) // updateable 为 Update 生命周期对象
            {
                _updateables.Add(updateable);
                _updateables.Sort((a, b) => a.UpdateOrder.CompareTo(b.UpdateOrder));
            }
            
            if (obj is ILateUpdateable lateUpdateable) // lateUpdateable 为 LateUpdate 生命周期对象
            {
                _lateUpdateables.Add(lateUpdateable);
                _lateUpdateables.Sort((a, b) => a.LateUpdateOrder.CompareTo(b.LateUpdateOrder));
            }
            
            if (obj is IPausable pausable) // pausable 为可暂停对象
            {
                _pausables.Add(pausable);
            }
        }
        
        /// <summary>
        /// 注销生命周期对象
        /// </summary>
        public void UnregisterLifecycle(object obj)
        {
            if (obj is ITickable tickable) // tickable 为 Tick 生命周期对象
                _tickables.Remove(tickable);
            
            if (obj is IUpdateable updateable) // updateable 为 Update 生命周期对象
                _updateables.Remove(updateable);
            
            if (obj is ILateUpdateable lateUpdateable) // lateUpdateable 为 LateUpdate 生命周期对象
                _lateUpdateables.Remove(lateUpdateable);
            
            if (obj is IPausable pausable) // pausable 为可暂停对象
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
                foreach (var pausable in _pausables) // pausable 为可暂停对象
                {
                    pausable.OnPause();
                }
            }
            else
            {
                // 切前台：恢复
                AudioListener.pause = false;
                Time.timeScale = 1f;
                
                // 暂停持续时长
                float pauseDuration = Time.realtimeSinceStartup - _pauseTimestamp;
                CYLog.Debug($"[CYBootstrap] 游戏恢复，暂停时长: {pauseDuration:F2}s");
                
                // 通知所有 IPausable
                foreach (var pausable in _pausables) // pausable 为可暂停对象
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
            // 未处理异常对象
            var ex = e.ExceptionObject as System.Exception;
            CYLog.Fatal($"[Unhandled Exception] {ex?.Message}\n{ex?.StackTrace}");
            // TODO: 保存现场快照，上报到服务器
        }
        
        #endregion
    }
}
