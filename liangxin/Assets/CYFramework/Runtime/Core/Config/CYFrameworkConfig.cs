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
        /// <summary>
        /// 框架启动配置
        /// </summary>
        public BootstrapConfig Bootstrap = new();
        
        [Header("=== 日志配置 ===")]
        /// <summary>
        /// 日志服务配置
        /// </summary>
        public LogServiceConfig Log = new();
        
        [Header("=== 音频配置 ===")]
        /// <summary>
        /// 音频配置
        /// </summary>
        public AudioConfig Audio = new();
        
        [Header("=== UI 配置 ===")]
        /// <summary>
        /// UI 管理器配置
        /// </summary>
        public UIManagerConfig UI = new();
        
        [Header("=== 网络配置 ===")]
        /// <summary>
        /// 网络服务配置
        /// </summary>
        public NetworkServiceConfig Network = new();
        
        [Header("=== 存档配置 ===")]
        /// <summary>
        /// 存档服务配置
        /// </summary>
        public SaveServiceConfig Save = new();
        
        [Header("=== 热更新配置 ===")]
        /// <summary>
        /// 热更新服务配置
        /// </summary>
        public HotUpdateServiceConfig HotUpdate = new();
        
        [Header("=== 对象池配置 ===")]
        /// <summary>
        /// 对象池配置
        /// </summary>
        public PoolManagerConfig Pool = new();
        
        [Header("=== 实体配置 ===")]
        /// <summary>
        /// 实体管理器配置
        /// </summary>
        public EntityManagerConfig Entity = new();
        
        [Header("=== 资源配置 ===")]
        /// <summary>
        /// 资源加载配置
        /// </summary>
        public ResourceLoaderConfig Resource = new();
        
        [Header("=== 计时器配置 ===")]
        /// <summary>
        /// 计时器配置
        /// </summary>
        public TimerManagerConfig Timer = new();
        
        [Header("=== 流程配置 ===")]
        /// <summary>
        /// 流程管理器配置
        /// </summary>
        public ProcedureManagerConfig Procedure = new();
        
        [Header("=== 调试配置 ===")]
        /// <summary>
        /// 调试工具配置
        /// </summary>
        public DebugToolsConfig Debug = new();
    }
    
    #region 框架级配置类（不与服务模块冲突）
    
    /// <summary>
    /// 框架启动配置
    /// </summary>
    [Serializable]
    public class BootstrapConfig
    {
        /// <summary>
        /// 逻辑帧率 (30/60Hz)
        /// </summary>
        [Tooltip("逻辑帧率 (30/60Hz)")]
        [Range(15, 120)]
        public int FixedTickRate = 30;
        
        /// <summary>
        /// 目标帧率 (-1 表示不限制)
        /// </summary>
        [Tooltip("目标帧率 (-1 表示不限制)")]
        public int TargetFrameRate = 60;
        
        /// <summary>
        /// 垂直同步
        /// </summary>
        [Tooltip("垂直同步")]
        public bool VSync = false;
        
        /// <summary>
        /// 后台运行
        /// </summary>
        [Tooltip("后台运行")]
        public bool RunInBackground = true;
        
        /// <summary>
        /// 屏幕常亮
        /// </summary>
        [Tooltip("屏幕常亮")]
        public bool ScreenNeverSleep = true;
        
        /// <summary>
        /// 最大暂停容忍时间(秒)
        /// </summary>
        [Tooltip("最大暂停容忍时间(秒)")]
        public float MaxPauseTolerance = 5f;
    }
    
    /// <summary>
    /// 日志服务配置
    /// </summary>
    [Serializable]
    public class LogServiceConfig
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        [Tooltip("日志级别")]
        public LogLevel Level = LogLevel.Debug;
        
        /// <summary>
        /// 是否显示时间戳
        /// </summary>
        [Tooltip("是否显示时间戳")]
        public bool ShowTimestamp = true;
        
        /// <summary>
        /// 是否显示调用栈
        /// </summary>
        [Tooltip("是否显示调用栈")]
        public bool ShowStackTrace = false;
        
        /// <summary>
        /// 是否输出到文件
        /// </summary>
        [Tooltip("是否输出到文件")]
        public bool WriteToFile = false;
        
        /// <summary>
        /// 日志文件路径 (相对于 persistentDataPath)
        /// </summary>
        [Tooltip("日志文件路径 (相对于 persistentDataPath)")]
        public string LogFilePath = "Logs/game.log";
        
        /// <summary>
        /// 最大日志文件大小 (MB)
        /// </summary>
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
        
        /// <summary>
        /// 是否启用面板对象池
        /// </summary>
        [Tooltip("是否启用面板对象池")]
        public bool EnablePanelPool = true;
        
        /// <summary>
        /// 面板池容量
        /// </summary>
        [Tooltip("面板池容量")]
        public int PanelPoolCapacity = 5;
        
        /// <summary>
        /// 默认面板动画时长
        /// </summary>
        [Tooltip("默认面板动画时长")]
        public float DefaultAnimDuration = 0.25f;
        
        /// <summary>
        /// Toast 显示时长
        /// </summary>
        [Tooltip("Toast 显示时长")]
        public float ToastDuration = 2f;
        
        /// <summary>
        /// Toast 最大同时显示数
        /// </summary>
        [Tooltip("Toast 最大同时显示数")]
        public int MaxToastCount = 3;
        
        [Header("自定义层级")]
        /// <summary>
        /// 自定义 UI 层级配置（启动时自动创建）
        /// </summary>
        [Tooltip("自定义 UI 层级配置（启动时自动创建）")]
        public CustomUILayer[] CustomLayers = new CustomUILayer[0];
    }
    
    /// <summary>
    /// 自定义 UI 层级配置
    /// </summary>
    [Serializable]
    public class CustomUILayer
    {
        /// <summary>
        /// 层级名称
        /// </summary>
        [Tooltip("层级名称")]
        public string Name;
        
        /// <summary>
        /// 排序顺序（越大越靠前）
        /// </summary>
        [Tooltip("排序顺序（越大越靠前）")]
        public int SortOrder;
    }
    
    /// <summary>
    /// 网络服务配置
    /// </summary>
    [Serializable]
    public class NetworkServiceConfig
    {
        /// <summary>
        /// HTTP 请求超时时间(秒)
        /// </summary>
        [Tooltip("HTTP 请求超时时间(秒)")]
        public float HttpTimeout = 10f;
        
        /// <summary>
        /// HTTP 最大重试次数
        /// </summary>
        [Tooltip("HTTP 最大重试次数")]
        public int HttpMaxRetry = 3;
        
        /// <summary>
        /// WebSocket 心跳间隔(秒)
        /// </summary>
        [Tooltip("WebSocket 心跳间隔(秒)")]
        public float HeartbeatInterval = 30f;
        
        /// <summary>
        /// WebSocket 心跳超时(秒)
        /// </summary>
        [Tooltip("WebSocket 心跳超时(秒)")]
        public float HeartbeatTimeout = 10f;
        
        /// <summary>
        /// WebSocket 最大重连次数
        /// </summary>
        [Tooltip("WebSocket 最大重连次数")]
        public int MaxReconnectAttempts = 5;
        
        /// <summary>
        /// WebSocket 重连间隔(秒)
        /// </summary>
        [Tooltip("WebSocket 重连间隔(秒)")]
        public float ReconnectInterval = 3f;
        
        /// <summary>
        /// 熔断器失败阈值
        /// </summary>
        [Tooltip("熔断器失败阈值")]
        public int CircuitBreakerThreshold = 5;
        
        /// <summary>
        /// 熔断器重置时间(秒)
        /// </summary>
        [Tooltip("熔断器重置时间(秒)")]
        public float CircuitBreakerResetTime = 30f;
    }
    
    /// <summary>
    /// 存档服务配置
    /// </summary>
    [Serializable]
    public class SaveServiceConfig
    {
        /// <summary>
        /// 存档文件名
        /// </summary>
        [Tooltip("存档文件名")]
        public string SaveFileName = "save.dat";
        
        /// <summary>
        /// 是否加密存档
        /// </summary>
        [Tooltip("是否加密存档")]
        public bool EnableEncryption = true;
        
        /// <summary>
        /// 加密密钥 (16字节)
        /// </summary>
        [Tooltip("加密密钥 (16字节)")]
        public string EncryptionKey = "CYFramework2024!";

        /// <summary>
        /// 是否启用校验和（防篡改）
        /// </summary>
        [Tooltip("是否启用校验和（防篡改）")]
        public bool EnableChecksum = true;

        /// <summary>
        /// 是否启用备份（仅 Native 平台有效）
        /// </summary>
        [Tooltip("是否启用备份（仅 Native 平台有效）")]
        public bool EnableBackup = true;

        /// <summary>
        /// 最大备份数量（仅 Native 平台有效）
        /// </summary>
        [Tooltip("最大备份数量（仅 Native 平台有效）")]
        public int MaxBackupCount = 3;
        
        /// <summary>
        /// 自动存档间隔(秒), 0=禁用
        /// </summary>
        [Tooltip("自动存档间隔(秒), 0=禁用")]
        public float AutoSaveInterval = 60f;
        
        /// <summary>
        /// 最大存档槽数
        /// </summary>
        [Tooltip("最大存档槽数")]
        public int MaxSaveSlots = 3;
        
        /// <summary>
        /// 存档版本
        /// </summary>
        [Tooltip("存档版本")]
        public int SaveVersion = 1;
    }
    
    /// <summary>
    /// 热更新服务配置
    /// </summary>
    [Serializable]
    public class HotUpdateServiceConfig
    {
        /// <summary>
        /// CDN 基础 URL
        /// </summary>
        [Tooltip("CDN 基础 URL")]
        public string CdnBaseUrl = "";
        
        /// <summary>
        /// 版本文件名
        /// </summary>
        [Tooltip("版本文件名")]
        public string VersionFileName = "version.json";
        
        /// <summary>
        /// 下载超时时间(秒)
        /// </summary>
        [Tooltip("下载超时时间(秒)")]
        public float DownloadTimeout = 30f;
        
        /// <summary>
        /// 最大并发下载数
        /// </summary>
        [Tooltip("最大并发下载数")]
        public int MaxConcurrentDownloads = 3;
        
        /// <summary>
        /// 下载失败最大重试次数
        /// </summary>
        [Tooltip("下载失败最大重试次数")]
        public int MaxDownloadRetry = 3;
        
        /// <summary>
        /// 是否启用增量更新
        /// </summary>
        [Tooltip("是否启用增量更新")]
        public bool EnableIncrementalUpdate = true;
    }
    
    /// <summary>
    /// 对象池管理器配置
    /// </summary>
    [Serializable]
    public class PoolManagerConfig
    {
        /// <summary>
        /// 默认池初始容量
        /// </summary>
        [Tooltip("默认池初始容量")]
        public int DefaultInitialCapacity = 16;
        
        /// <summary>
        /// 默认池最大容量
        /// </summary>
        [Tooltip("默认池最大容量")]
        public int DefaultMaxCapacity = 256;
        
        /// <summary>
        /// 默认预热数量
        /// </summary>
        [Tooltip("默认预热数量")]
        public int DefaultWarmupCount = 8;
        
        /// <summary>
        /// 池清理间隔(秒)
        /// </summary>
        [Tooltip("池清理间隔(秒)")]
        public float CleanupInterval = 60f;
        
        /// <summary>
        /// 对象空闲超时时间(秒)
        /// </summary>
        [Tooltip("对象空闲超时时间(秒)")]
        public float IdleTimeout = 120f;
        
        [Header("对象池分组")]
        /// <summary>
        /// 对象池分组名称列表（运行时自动创建子节点）
        /// </summary>
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
        
        /// <summary>
        /// 默认预加载数量
        /// </summary>
        [Tooltip("默认预加载数量")]
        public int DefaultPreloadCount = 5;
        
        /// <summary>
        /// 实体池最大容量
        /// </summary>
        [Tooltip("实体池最大容量")]
        public int MaxPoolSize = 100;
        
        /// <summary>
        /// 实体更新间隔(帧), 1=每帧更新
        /// </summary>
        [Tooltip("实体更新间隔(帧), 1=每帧更新")]
        public int UpdateInterval = 1;
        
        [Header("实体分组")]
        /// <summary>
        /// 实体分组名称列表（运行时自动创建子节点）
        /// </summary>
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
        /// <summary>
        /// 资源加载模式
        /// </summary>
        [Tooltip("资源加载模式")]
        public ResourceLoadMode LoadMode = ResourceLoadMode.Resources;
        
        /// <summary>
        /// Addressables 标签
        /// </summary>
        [Tooltip("Addressables 标签")]
        public string AddressablesLabel = "default";
        
        /// <summary>
        /// 资源缓存大小 (MB)
        /// </summary>
        [Tooltip("资源缓存大小 (MB)")]
        public int CacheSizeMB = 100;
        
        /// <summary>
        /// 异步加载优先级
        /// </summary>
        [Tooltip("异步加载优先级")]
        public int AsyncLoadPriority = 100;
        
        /// <summary>
        /// 是否启用资源引用计数
        /// </summary>
        [Tooltip("是否启用资源引用计数")]
        public bool EnableRefCount = true;
        
        [Header("资源路径配置")]
        /// <summary>
        /// UI 面板预制体路径
        /// </summary>
        [Tooltip("UI 面板预制体路径")]
        public string UIPanelPath = "UI/Panels/";
        
        /// <summary>
        /// 实体预制体路径
        /// </summary>
        [Tooltip("实体预制体路径")]
        public string EntityPath = "Entities/";
        
        /// <summary>
        /// 音频资源路径
        /// </summary>
        [Tooltip("音频资源路径")]
        public string AudioPath = "Audio/";
        
        /// <summary>
        /// BGM 资源路径
        /// </summary>
        [Tooltip("BGM 资源路径")]
        public string BGMPath = "Audio/BGM/";
        
        /// <summary>
        /// SFX 资源路径
        /// </summary>
        [Tooltip("SFX 资源路径")]
        public string SFXPath = "Audio/SFX/";
        
        /// <summary>
        /// 精灵资源路径
        /// </summary>
        [Tooltip("精灵资源路径")]
        public string SpritePath = "Sprites/";
        
        /// <summary>
        /// 图标资源路径
        /// </summary>
        [Tooltip("图标资源路径")]
        public string IconPath = "Sprites/Icons/";
        
        /// <summary>
        /// 通用预制体路径
        /// </summary>
        [Tooltip("通用预制体路径")]
        public string PrefabPath = "Prefabs/";
        
        /// <summary>
        /// 特效预制体路径
        /// </summary>
        [Tooltip("特效预制体路径")]
        public string EffectPath = "Effects/";
        
        /// <summary>
        /// 配置文件路径
        /// </summary>
        [Tooltip("配置文件路径")]
        public string ConfigPath = "Config/";
        
        /// <summary>
        /// 材质资源路径
        /// </summary>
        [Tooltip("材质资源路径")]
        public string MaterialPath = "Materials/";
        
        /// <summary>
        /// 动画资源路径
        /// </summary>
        [Tooltip("动画资源路径")]
        public string AnimationPath = "Animations/";
        
        /// <summary>
        /// 场景路径
        /// </summary>
        [Tooltip("场景路径")]
        public string ScenePath = "Scenes/";
    }
    
    /// <summary>
    /// 计时器管理器配置
    /// </summary>
    [Serializable]
    public class TimerManagerConfig
    {
        /// <summary>
        /// 计时器池初始容量
        /// </summary>
        [Tooltip("计时器池初始容量")]
        public int InitialCapacity = 32;
        
        /// <summary>
        /// 是否使用 unscaled time
        /// </summary>
        [Tooltip("是否使用 unscaled time")]
        public bool UseUnscaledTime = false;
    }
    
    /// <summary>
    /// 流程管理器配置
    /// </summary>
    [Serializable]
    public class ProcedureManagerConfig
    {
        /// <summary>
        /// 入口流程类型名
        /// </summary>
        [Tooltip("入口流程类型名")]
        public string EntryProcedure = "";
        
        /// <summary>
        /// 是否自动注册流程
        /// </summary>
        [Tooltip("是否自动注册流程")]
        public bool AutoRegisterProcedures = true;
    }
    
    /// <summary>
    /// 调试工具配置
    /// </summary>
    [Serializable]
    public class DebugToolsConfig
    {
        /// <summary>
        /// 是否启用调试控制台
        /// </summary>
        [Tooltip("是否启用调试控制台")]
        public bool EnableConsole = true;
        
        /// <summary>
        /// 控制台快捷键
        /// </summary>
        [Tooltip("控制台快捷键")]
        public KeyCode ConsoleToggleKey = KeyCode.BackQuote;
        
        /// <summary>
        /// 是否显示 FPS
        /// </summary>
        [Tooltip("是否显示 FPS")]
        public bool ShowFPS = true;
        
        /// <summary>
        /// 是否显示内存信息
        /// </summary>
        [Tooltip("是否显示内存信息")]
        public bool ShowMemory = true;
        
        /// <summary>
        /// 是否启用 GM 命令
        /// </summary>
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
