// ============================================================================
// CYFramework 2.2 - 框架配置
// 功能：统一管理所有模块的配置，支持编辑器可视化配置
// 注意：使用各服务模块内部定义的配置类，避免重复定义
// ============================================================================

using System;
using UnityEngine;
using CYFramework.Infrastructure;
using CYFramework.Core.Audio;
using CYFramework.Core.UI;
using CYFramework.Core.Network;
using CYFramework.Core.Save;
using CYFramework.Core.HotUpdate;
using CYFramework.Core.Pool;

namespace CYFramework.Core.Config
{
    /// <summary>
    /// CYFramework 主配置
    /// 在编辑器中创建：Assets/Create/CYFramework/Framework Config
    /// </summary>
    [CreateAssetMenu(fileName = "CYFrameworkConfig", menuName = "CYFramework/Framework Config", order = 0)]
    public class CYFrameworkConfig : ScriptableObject
    {
        [Header("=== 框架基础配置 ===")]
        public BootstrapConfig Bootstrap = new();
        
        [Header("=== 日志配置 ===")]
        public LogServiceConfig Log = new();
        
        [Header("=== 音频配置 ===")]
        public AudioConfig Audio = new();
        
        [Header("=== UI 配置 ===")]
        public UIManagerConfig UI = new();
        
        [Header("=== 网络配置 ===")]
        public NetworkServiceConfig Network = new();
        
        [Header("=== 存档配置 ===")]
        public SaveServiceConfig Save = new();
        
        [Header("=== 热更新配置 ===")]
        public HotUpdateServiceConfig HotUpdate = new();
        
        [Header("=== 对象池配置 ===")]
        public PoolManagerConfig Pool = new();
        
        [Header("=== 实体配置 ===")]
        public EntityManagerConfig Entity = new();
        
        [Header("=== 资源配置 ===")]
        public ResourceLoaderConfig Resource = new();
        
        [Header("=== 计时器配置 ===")]
        public TimerManagerConfig Timer = new();
        
        [Header("=== 流程配置 ===")]
        public ProcedureManagerConfig Procedure = new();
        
        [Header("=== 调试配置 ===")]
        public DebugToolsConfig Debug = new();
    }
    
    #region 框架级配置类（不与服务模块冲突）
    
    /// <summary>
    /// 框架启动配置
    /// </summary>
    [Serializable]
    public class BootstrapConfig
    {
        [Tooltip("逻辑帧率 (30/60Hz)")]
        [Range(15, 120)]
        public int FixedTickRate = 30;
        
        [Tooltip("目标帧率 (-1 表示不限制)")]
        public int TargetFrameRate = 60;
        
        [Tooltip("垂直同步")]
        public bool VSync = false;
        
        [Tooltip("后台运行")]
        public bool RunInBackground = true;
        
        [Tooltip("屏幕常亮")]
        public bool ScreenNeverSleep = true;
        
        [Tooltip("最大暂停容忍时间(秒)")]
        public float MaxPauseTolerance = 5f;
    }
    
    /// <summary>
    /// 日志服务配置
    /// </summary>
    [Serializable]
    public class LogServiceConfig
    {
        [Tooltip("日志级别")]
        public LogLevel Level = LogLevel.Debug;
        
        [Tooltip("是否显示时间戳")]
        public bool ShowTimestamp = true;
        
        [Tooltip("是否显示调用栈")]
        public bool ShowStackTrace = false;
        
        [Tooltip("是否输出到文件")]
        public bool WriteToFile = false;
        
        [Tooltip("日志文件路径 (相对于 persistentDataPath)")]
        public string LogFilePath = "Logs/game.log";
        
        [Tooltip("最大日志文件大小 (MB)")]
        public int MaxLogFileSizeMB = 10;
    }
    
    /// <summary>
    /// UI 管理器配置（相机和Canvas直接在预制体上配置，路径在 ResourceLoaderConfig 中配置）
    /// </summary>
    [Serializable]
    public class UIManagerConfig
    {
        // 注意：UI面板路径已移至 ResourceLoaderConfig.UIPanelPath
        
        [Tooltip("是否启用面板对象池")]
        public bool EnablePanelPool = true;
        
        [Tooltip("面板池容量")]
        public int PanelPoolCapacity = 5;
        
        [Tooltip("默认面板动画时长")]
        public float DefaultAnimDuration = 0.25f;
        
        [Tooltip("Toast 显示时长")]
        public float ToastDuration = 2f;
        
        [Tooltip("Toast 最大同时显示数")]
        public int MaxToastCount = 3;
        
        [Header("自定义层级")]
        [Tooltip("自定义 UI 层级配置（启动时自动创建）")]
        public CustomUILayer[] CustomLayers = new CustomUILayer[0];
    }
    
    /// <summary>
    /// 自定义 UI 层级配置
    /// </summary>
    [Serializable]
    public class CustomUILayer
    {
        [Tooltip("层级名称")]
        public string Name;
        
        [Tooltip("排序顺序（越大越靠前）")]
        public int SortOrder;
    }
    
    /// <summary>
    /// 网络服务配置
    /// </summary>
    [Serializable]
    public class NetworkServiceConfig
    {
        [Tooltip("HTTP 请求超时时间(秒)")]
        public float HttpTimeout = 10f;
        
        [Tooltip("HTTP 最大重试次数")]
        public int HttpMaxRetry = 3;
        
        [Tooltip("WebSocket 心跳间隔(秒)")]
        public float HeartbeatInterval = 30f;
        
        [Tooltip("WebSocket 心跳超时(秒)")]
        public float HeartbeatTimeout = 10f;
        
        [Tooltip("WebSocket 最大重连次数")]
        public int MaxReconnectAttempts = 5;
        
        [Tooltip("WebSocket 重连间隔(秒)")]
        public float ReconnectInterval = 3f;
        
        [Tooltip("熔断器失败阈值")]
        public int CircuitBreakerThreshold = 5;
        
        [Tooltip("熔断器重置时间(秒)")]
        public float CircuitBreakerResetTime = 30f;
    }
    
    /// <summary>
    /// 存档服务配置
    /// </summary>
    [Serializable]
    public class SaveServiceConfig
    {
        [Tooltip("存档文件名")]
        public string SaveFileName = "save.dat";
        
        [Tooltip("是否加密存档")]
        public bool EnableEncryption = true;
        
        [Tooltip("加密密钥 (16字节)")]
        public string EncryptionKey = "CYFramework2024!";
        
        [Tooltip("自动存档间隔(秒), 0=禁用")]
        public float AutoSaveInterval = 60f;
        
        [Tooltip("最大存档槽数")]
        public int MaxSaveSlots = 3;
        
        [Tooltip("存档版本")]
        public int SaveVersion = 1;
    }
    
    /// <summary>
    /// 热更新服务配置
    /// </summary>
    [Serializable]
    public class HotUpdateServiceConfig
    {
        [Tooltip("CDN 基础 URL")]
        public string CdnBaseUrl = "";
        
        [Tooltip("版本文件名")]
        public string VersionFileName = "version.json";
        
        [Tooltip("下载超时时间(秒)")]
        public float DownloadTimeout = 30f;
        
        [Tooltip("最大并发下载数")]
        public int MaxConcurrentDownloads = 3;
        
        [Tooltip("下载失败最大重试次数")]
        public int MaxDownloadRetry = 3;
        
        [Tooltip("是否启用增量更新")]
        public bool EnableIncrementalUpdate = true;
    }
    
    /// <summary>
    /// 对象池管理器配置
    /// </summary>
    [Serializable]
    public class PoolManagerConfig
    {
        [Tooltip("默认池初始容量")]
        public int DefaultInitialCapacity = 16;
        
        [Tooltip("默认池最大容量")]
        public int DefaultMaxCapacity = 256;
        
        [Tooltip("默认预热数量")]
        public int DefaultWarmupCount = 8;
        
        [Tooltip("池清理间隔(秒)")]
        public float CleanupInterval = 60f;
        
        [Tooltip("对象空闲超时时间(秒)")]
        public float IdleTimeout = 120f;
        
        [Header("对象池分组")]
        [Tooltip("对象池分组名称列表（运行时自动创建子节点）")]
        public string[] PoolGroups = new string[]
        {
            "Bullets",
            "Effects",
            "UI",
            "Audio",
            "Misc"
        };
    }
    
    /// <summary>
    /// 实体管理器配置（路径在 ResourceLoaderConfig 中配置）
    /// </summary>
    [Serializable]
    public class EntityManagerConfig
    {
        // 注意：实体预制体路径已移至 ResourceLoaderConfig.EntityPath
        
        [Tooltip("默认预加载数量")]
        public int DefaultPreloadCount = 5;
        
        [Tooltip("实体池最大容量")]
        public int MaxPoolSize = 100;
        
        [Tooltip("实体更新间隔(帧), 1=每帧更新")]
        public int UpdateInterval = 1;
        
        [Header("实体分组")]
        [Tooltip("实体分组名称列表（运行时自动创建子节点）")]
        public string[] EntityGroups = new string[]
        {
            "Players",
            "Enemies",
            "NPCs",
            "Props",
            "Effects",
            "Projectiles",
            "Items"
        };
    }
    
    /// <summary>
    /// 资源加载器配置
    /// </summary>
    [Serializable]
    public class ResourceLoaderConfig
    {
        [Tooltip("资源加载模式")]
        public ResourceLoadMode LoadMode = ResourceLoadMode.Resources;
        
        [Tooltip("Addressables 标签")]
        public string AddressablesLabel = "default";
        
        [Tooltip("资源缓存大小 (MB)")]
        public int CacheSizeMB = 100;
        
        [Tooltip("异步加载优先级")]
        public int AsyncLoadPriority = 100;
        
        [Tooltip("是否启用资源引用计数")]
        public bool EnableRefCount = true;
        
        [Header("资源路径配置")]
        [Tooltip("UI 面板预制体路径")]
        public string UIPanelPath = "UI/Panels/";
        
        [Tooltip("实体预制体路径")]
        public string EntityPath = "Entities/";
        
        [Tooltip("音频资源路径")]
        public string AudioPath = "Audio/";
        
        [Tooltip("BGM 资源路径")]
        public string BGMPath = "Audio/BGM/";
        
        [Tooltip("SFX 资源路径")]
        public string SFXPath = "Audio/SFX/";
        
        [Tooltip("精灵资源路径")]
        public string SpritePath = "Sprites/";
        
        [Tooltip("图标资源路径")]
        public string IconPath = "Sprites/Icons/";
        
        [Tooltip("通用预制体路径")]
        public string PrefabPath = "Prefabs/";
        
        [Tooltip("特效预制体路径")]
        public string EffectPath = "Effects/";
        
        [Tooltip("配置文件路径")]
        public string ConfigPath = "Config/";
        
        [Tooltip("材质资源路径")]
        public string MaterialPath = "Materials/";
        
        [Tooltip("动画资源路径")]
        public string AnimationPath = "Animations/";
        
        [Tooltip("场景路径")]
        public string ScenePath = "Scenes/";
    }
    
    /// <summary>
    /// 计时器管理器配置
    /// </summary>
    [Serializable]
    public class TimerManagerConfig
    {
        [Tooltip("计时器池初始容量")]
        public int InitialCapacity = 32;
        
        [Tooltip("是否使用 unscaled time")]
        public bool UseUnscaledTime = false;
    }
    
    /// <summary>
    /// 流程管理器配置
    /// </summary>
    [Serializable]
    public class ProcedureManagerConfig
    {
        [Tooltip("入口流程类型名")]
        public string EntryProcedure = "";
        
        [Tooltip("是否自动注册流程")]
        public bool AutoRegisterProcedures = true;
    }
    
    /// <summary>
    /// 调试工具配置
    /// </summary>
    [Serializable]
    public class DebugToolsConfig
    {
        [Tooltip("是否启用调试控制台")]
        public bool EnableConsole = true;
        
        [Tooltip("控制台快捷键")]
        public KeyCode ConsoleToggleKey = KeyCode.BackQuote;
        
        [Tooltip("是否显示 FPS")]
        public bool ShowFPS = true;
        
        [Tooltip("是否显示内存信息")]
        public bool ShowMemory = true;
        
        [Tooltip("是否启用 GM 命令")]
        public bool EnableGMCommands = true;
    }
    
    #endregion
    
    #region 枚举定义
    
    /// <summary>
    /// 资源加载模式
    /// </summary>
    public enum ResourceLoadMode
    {
        Resources,      // Unity Resources 目录
        Addressables,   // Addressables 系统
        AssetBundle     // AssetBundle
    }
    
    #endregion
}
