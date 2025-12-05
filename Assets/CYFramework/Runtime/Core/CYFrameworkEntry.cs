// ============================================================================
// CYFramework - 框架入口组件
// 挂载在常驻 GameObject 上，负责驱动整个框架的生命周期
// 使用方式：
// 1. 在场景中创建一个空 GameObject，命名为 "CYFramework" 或 "GameEntry"
// 2. 挂载此组件
// 3. 框架会自动初始化并在整个游戏生命周期中驱动所有模块
// ============================================================================

using UnityEngine;
using CYFramework.Runtime.Core.UI;
using CYFramework.Runtime.Core.Resource;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// CYFramework 框架入口
    /// 负责：
    /// - 初始化和管理 ModuleManager
    /// - 驱动所有模块的帧更新
    /// - 处理框架的启动和关闭
    /// </summary>
    public class CYFrameworkEntry : MonoBehaviour
    {
        // ====================================================================
        // Inspector 配置
        // ====================================================================

        [Header("=== 日志配置 ===")]
        [Tooltip("日志级别：Debug < Info < Warning < Error")]
        [SerializeField] private LogLevel _logLevel = LogLevel.Debug;
        
        [Tooltip("是否在日志中显示时间戳")]
        [SerializeField] private bool _logShowTimestamp = true;
        
        [Tooltip("是否在日志中显示模块名")]
        [SerializeField] private bool _logShowModule = true;

        [Header("=== 对象池配置 ===")]
        [Tooltip("每种类型的默认最大容量")]
        [SerializeField] private int _poolDefaultCapacity = 100;
        
        [Tooltip("是否在获取时自动扩容")]
        [SerializeField] private bool _poolAutoExpand = true;

        [Header("=== 资源加载配置 ===")]
        [Tooltip("资源加载方式")]
        [SerializeField] private ResourceLoaderType _resourceLoaderType = ResourceLoaderType.Resources;

        [Tooltip("Resources 路径前缀（如 'Game/' 则加载 'Game/xxx'）")]
        [SerializeField] private string _resourcesPathPrefix = "";

        [Tooltip("AssetBundle 存放路径（相对于 StreamingAssets）")]
        [SerializeField] private string _assetBundlePath = "Bundles";

        [Tooltip("Addressables 地址前缀（如 'Assets/Game/' 则加载 'Assets/Game/xxx'）")]
        [SerializeField] private string _addressablesPrefix = "";

        [Header("=== 存储配置 ===")]
        [Tooltip("存储键名前缀（用于区分不同项目）")]
        [SerializeField] private string _storagePrefix = "CYGame_";
        
        [Tooltip("是否自动保存（每次 Set 后自动调用 Save）")]
        [SerializeField] private bool _storageAutoSave = false;

        [Header("=== 声音配置 ===")]
        [Tooltip("背景音乐音量 (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _bgmVolume = 1f;
        
        [Tooltip("音效音量 (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 1f;
        
        [Tooltip("是否静音背景音乐")]
        [SerializeField] private bool _muteBGM = false;
        
        [Tooltip("是否静音音效")]
        [SerializeField] private bool _muteSFX = false;

        [Header("=== UI 配置 ===")]
        [Tooltip("UI 面板预制体路径前缀")]
        [SerializeField] private string _uiPanelPathPrefix = "UI/Panels/";
        
        [Tooltip("UI 点击音效路径（留空则不播放）")]
        [SerializeField] private string _uiClickSoundPath = "";

        [Header("=== 调度器配置 ===")]
        [Tooltip("每帧最大执行时间（毫秒）")]
        [SerializeField] private float _schedulerMaxTimePerFrame = 5f;

        [Header("=== 流程配置 ===")]
        [Tooltip("是否自动注册所有 ProcedureBase 子类（使用反射，默认关闭）")]
        [SerializeField] private bool _procedureAutoRegister = false;

        [Header("=== 玩法世界配置 ===")]
        [Tooltip("是否使用 DOTS 实现（需要安装 Entities 包，仅 PC/移动端有效）")]
        [SerializeField] private bool _useDotsImplementation = false;

        [Tooltip("是否启用调试模式")]
        [SerializeField] private bool _debugMode = false;

        [Tooltip("时间缩放（1.0 = 正常速度）")]
        [SerializeField] private float _timeScale = 1f;

        [Tooltip("最大实体数量")]
        [SerializeField] private int _maxEntityCount = 1000;

        [Tooltip("关卡 ID")]
        [SerializeField] private int _levelId = 0;

        [Tooltip("难度等级（1=简单，2=普通，3=困难）")]
        [SerializeField] private int _difficulty = 1;

        [Tooltip("随机种子（0 表示使用系统时间）")]
        [SerializeField] private int _randomSeed = 0;

        /// <summary>
        /// 框架单例（方便全局访问，但建议尽量通过模块接口交互）
        /// </summary>
        public static CYFrameworkEntry Instance { get; private set; }

        /// <summary>
        /// 模块管理器
        /// </summary>
        public ModuleManager ModuleManager { get; private set; }

        /// <summary>
        /// 玩法世界实例（通过 IGameplayWorld 接口访问）
        /// </summary>
        public Gameplay.Abstraction.IGameplayWorld GameplayWorld { get; private set; }

        /// <summary>
        /// 框架是否已初始化
        /// </summary>
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            // 单例检查
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CYFramework] 检测到重复的 CYFrameworkEntry，销毁当前实例");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 初始化框架
            InitializeFramework();
        }

        /// <summary>
        /// 初始化框架
        /// </summary>
        private void InitializeFramework()
        {
            Debug.Log("[CYFramework] 开始初始化框架...");

            // 创建模块管理器
            ModuleManager = new ModuleManager();

            // 注册核心模块（后续在这里添加）
            RegisterCoreModules();

            // 在初始化前应用需要提前设置的配置
            ApplyPreInitConfigs();

            // 初始化所有模块
            ModuleManager.Initialize();

            // 应用其他模块配置
            ApplyModuleConfigs();

            // 创建玩法世界（根据平台选择实现）
            CreateGameplayWorld();

            IsInitialized = true;
            Debug.Log("[CYFramework] 框架初始化完成");
        }

        /// <summary>
        /// 注册核心模块
        /// 在这里添加框架需要的核心模块
        /// </summary>
        private void RegisterCoreModules()
        {
            // 日志模块（优先级 -10，最先初始化）
            ModuleManager.RegisterModule<LogModule>(new LogModule());
            
            // 事件模块（优先级 0）
            ModuleManager.RegisterModule<EventModule>(new EventModule());
            
            // 对象池模块（优先级 5）
            ModuleManager.RegisterModule<ObjectPoolModule>(new ObjectPoolModule());
            
            // 定时器模块（优先级 10）
            ModuleManager.RegisterModule<TimerModule>(new TimerModule());
            
            // 分帧调度器模块（优先级 12）
            ModuleManager.RegisterModule<SchedulerModule>(new SchedulerModule());
            
            // 资源模块（优先级 15）
            ModuleManager.RegisterModule<ResourceModule>(new ResourceModule());
            
            // 存储模块（优先级 20）
            ModuleManager.RegisterModule<StorageModule>(new StorageModule());
            
            // 声音模块（优先级 25）
            ModuleManager.RegisterModule<SoundModule>(new SoundModule());
            
            // UI 模块（优先级 25）
            ModuleManager.RegisterModule<UIModule>(new UIModule());
            
            // 流程模块（优先级 30）
            ModuleManager.RegisterModule<ProcedureModule>(new ProcedureModule());
        }

        // ====================================================================
        // 便捷访问属性（缓存常用模块引用，避免频繁 GetModule）
        // ====================================================================

        private LogModule _logModule;
        private EventModule _eventModule;
        private TimerModule _timerModule;
        private ObjectPoolModule _poolModule;
        private StorageModule _storageModule;
        private ResourceModule _resourceModule;
        private SoundModule _soundModule;
        private ProcedureModule _procedureModule;
        private SchedulerModule _schedulerModule;
        private UIModule _uiModule;

        /// <summary>
        /// 日志模块
        /// </summary>
        public LogModule Log => _logModule ??= GetModule<LogModule>();

        /// <summary>
        /// 事件模块
        /// </summary>
        public EventModule Event => _eventModule ??= GetModule<EventModule>();

        /// <summary>
        /// 定时器模块
        /// </summary>
        public TimerModule Timer => _timerModule ??= GetModule<TimerModule>();

        /// <summary>
        /// 对象池模块
        /// </summary>
        public ObjectPoolModule Pool => _poolModule ??= GetModule<ObjectPoolModule>();

        /// <summary>
        /// 存储模块
        /// </summary>
        public StorageModule Storage => _storageModule ??= GetModule<StorageModule>();

        /// <summary>
        /// 资源模块
        /// </summary>
        public ResourceModule Resource => _resourceModule ??= GetModule<ResourceModule>();

        /// <summary>
        /// 声音模块
        /// </summary>
        public SoundModule Sound => _soundModule ??= GetModule<SoundModule>();

        /// <summary>
        /// 流程模块
        /// </summary>
        public ProcedureModule Procedure => _procedureModule ??= GetModule<ProcedureModule>();

        /// <summary>
        /// 分帧调度器模块
        /// </summary>
        public SchedulerModule Scheduler => _schedulerModule ??= GetModule<SchedulerModule>();

        /// <summary>
        /// UI 模块
        /// </summary>
        public UIModule UI => _uiModule ??= GetModule<UIModule>();

        /// <summary>
        /// 应用所有模块配置
        /// </summary>
        private void ApplyModuleConfigs()
        {
            // 日志配置
            var logModule = GetModule<LogModule>();
            if (logModule != null)
            {
                logModule.SetLogLevel(_logLevel);
                logModule.ShowTimestamp = _logShowTimestamp;
                logModule.ShowModule = _logShowModule;
            }

            // 对象池配置
            var poolModule = GetModule<ObjectPoolModule>();
            if (poolModule != null)
            {
                poolModule.DefaultCapacity = _poolDefaultCapacity;
                poolModule.AutoExpand = _poolAutoExpand;
            }

            // 资源配置
            var resourceModule = GetModule<ResourceModule>();
            if (resourceModule != null)
            {
                resourceModule.SetLoader(_resourceLoaderType);
                
                // 根据加载器类型设置对应的配置
                switch (_resourceLoaderType)
                {
                    case ResourceLoaderType.Resources:
                        if (!string.IsNullOrEmpty(_resourcesPathPrefix))
                            resourceModule.Loader?.SetPathPrefix(_resourcesPathPrefix);
                        break;
                        
                    case ResourceLoaderType.AssetBundle:
                        if (!string.IsNullOrEmpty(_resourcesPathPrefix))
                            resourceModule.Loader?.SetPathPrefix(_resourcesPathPrefix);
                        if (!string.IsNullOrEmpty(_assetBundlePath))
                            resourceModule.Loader?.SetRootPath(_assetBundlePath);
                        break;
                        
                    case ResourceLoaderType.Addressables:
                        if (!string.IsNullOrEmpty(_addressablesPrefix))
                            resourceModule.Loader?.SetPathPrefix(_addressablesPrefix);
                        break;
                }
            }

            // 存储配置
            var storageModule = GetModule<StorageModule>();
            if (storageModule != null)
            {
                storageModule.KeyPrefix = _storagePrefix;
                storageModule.AutoSave = _storageAutoSave;
            }

            // 声音配置
            var soundModule = GetModule<SoundModule>();
            if (soundModule != null)
            {
                soundModule.BGMVolume = _bgmVolume;
                soundModule.SFXVolume = _sfxVolume;
                soundModule.MuteBGM = _muteBGM;
                soundModule.MuteSFX = _muteSFX;
            }

            // UI 配置
            var uiModule = GetModule<UIModule>();
            if (uiModule != null)
            {
                uiModule.SetPanelPathPrefix(_uiPanelPathPrefix);
                uiModule.SetClickSoundPath(_uiClickSoundPath);
            }

            // 调度器配置
            var schedulerModule = GetModule<SchedulerModule>();
            if (schedulerModule != null)
            {
                schedulerModule.MaxTimePerFrameMs = _schedulerMaxTimePerFrame;
            }

        }

        /// <summary>
        /// 应用需要在模块初始化前设置的配置
        /// </summary>
        private void ApplyPreInitConfigs()
        {
            // 流程配置（需要在 Initialize 前设置，因为自动注册在 Initialize 中执行）
            var procedureModule = GetModule<ProcedureModule>();
            if (procedureModule != null)
            {
                procedureModule.AutoRegister = _procedureAutoRegister;
            }
        }

        /// <summary>
        /// 创建玩法世界
        /// 根据平台选择合适的实现
        /// </summary>
        private void CreateGameplayWorld()
        {
            // 使用 Inspector 中的配置创建玩法世界
            var config = new Gameplay.Abstraction.GameplayConfig
            {
                UseDotsImplementation = _useDotsImplementation,
                DebugMode = _debugMode,
                TimeScale = _timeScale,
                MaxEntityCount = _maxEntityCount,
                LevelId = _levelId,
                Difficulty = _difficulty,
                RandomSeed = _randomSeed
            };
            GameplayWorld = Gameplay.GameplayWorldFactory.Create(config);
            
            Debug.Log("[CYFramework] 玩法世界创建完成");
        }

        private void Update()
        {
            if (!IsInitialized) return;

            float deltaTime = Time.deltaTime;

            // 更新模块管理器
            ModuleManager.Update(deltaTime);

            // 更新玩法世界
            GameplayWorld?.Tick(deltaTime);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            ShutdownFramework();
            Instance = null;
        }

        private void OnApplicationQuit()
        {
            ShutdownFramework();
        }

        /// <summary>
        /// 关闭框架
        /// </summary>
        private void ShutdownFramework()
        {
            if (!IsInitialized) return;

            Debug.Log("[CYFramework] 开始关闭框架...");

            // 关闭玩法世界
            GameplayWorld?.Shutdown();
            GameplayWorld = null;

            // 关闭模块管理器
            ModuleManager?.Shutdown();
            ModuleManager = null;

            IsInitialized = false;
            Debug.Log("[CYFramework] 框架已关闭");
        }

        /// <summary>
        /// 获取模块的便捷方法
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>模块实例</returns>
        public T GetModule<T>() where T : class, IModule
        {
            return ModuleManager?.GetModule<T>();
        }
    }
}
