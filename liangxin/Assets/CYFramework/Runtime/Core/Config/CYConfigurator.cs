// ============================================================================
// CYFramework 2.2 - 运行时配置器
// 功能：挂载到 CYFramework 预制体上，提供可视化配置
// ============================================================================

using UnityEngine;
using CYFramework.Core.Audio;

namespace CYFramework.Core.Config
{
    /// <summary>
    /// CYFramework 运行时配置器
    /// 挂载到场景中的 CYFramework GameObject 上
    /// </summary>
    [DisallowMultipleComponent]
    // 必须早于 CYBootstrap.Awake 执行：框架启动器在 Awake 内初始化所有服务，服务会在 Initialize 时读取 CYConfigurator.Instance。
    // 如果 CYConfigurator 的 Awake 晚于 CYBootstrap，就会出现“配置字段明明改了但运行时无效”的问题。
    [DefaultExecutionOrder(-1100)]
    public class CYConfigurator : MonoBehaviour
    {
        [Header("框架配置文件")]
        /// <summary>
        /// 引用 ScriptableObject 配置文件，留空则使用默认配置
        /// </summary>
        [Tooltip("引用 ScriptableObject 配置文件，留空则使用默认配置")]
        /// <summary>
        /// 框架配置资产
        /// </summary>
        public CYFrameworkConfig ConfigAsset;
        
        [Header("=== 或使用内联配置 ===")]
        [Space(10)]
        
        [Header("框架启动")]
        /// <summary>
        /// 启动配置
        /// </summary>
        public BootstrapConfig Bootstrap = new();
        
        [Header("日志")]
        /// <summary>
        /// 日志配置
        /// </summary>
        public LogServiceConfig Log = new();
        
        [Header("音频")]
        /// <summary>
        /// 音频配置
        /// </summary>
        public AudioConfig Audio = new();
        
        [Header("UI")]
        /// <summary>
        /// UI 配置
        /// </summary>
        public UIManagerConfig UI = new();
        
        [Header("网络")]
        /// <summary>
        /// 网络配置
        /// </summary>
        public NetworkServiceConfig Network = new();
        
        [Header("存档")]
        /// <summary>
        /// 存档配置
        /// </summary>
        public SaveServiceConfig Save = new();
        
        [Header("热更新")]
        /// <summary>
        /// 热更新配置
        /// </summary>
        public HotUpdateServiceConfig HotUpdate = new();
        
        [Header("对象池")]
        /// <summary>
        /// 对象池配置
        /// </summary>
        public PoolManagerConfig Pool = new();
        
        [Header("实体")]
        /// <summary>
        /// 实体配置
        /// </summary>
        public EntityManagerConfig Entity = new();
        
        [Header("资源")]
        /// <summary>
        /// 资源配置
        /// </summary>
        public ResourceLoaderConfig Resource = new();
        
        [Header("计时器")]
        /// <summary>
        /// 计时器配置
        /// </summary>
        public TimerManagerConfig Timer = new();
        
        [Header("流程")]
        /// <summary>
        /// 流程配置
        /// </summary>
        public ProcedureManagerConfig Procedure = new();
        
        [Header("调试")]
        /// <summary>
        /// 调试配置
        /// </summary>
        public DebugToolsConfig Debug = new();
        
        /// <summary>
        /// 获取配置（优先使用 ConfigAsset）
        /// </summary>
        public T GetConfig<T>() where T : class
        {
            var type = typeof(T); // 目标类型
            
            if (type == typeof(BootstrapConfig))
                return (ConfigAsset != null ? ConfigAsset.Bootstrap : Bootstrap) as T;
            if (type == typeof(LogServiceConfig))
                return (ConfigAsset != null ? ConfigAsset.Log : Log) as T;
            if (type == typeof(AudioConfig))
                return (ConfigAsset != null ? ConfigAsset.Audio : Audio) as T;
            if (type == typeof(UIManagerConfig))
                return (ConfigAsset != null ? ConfigAsset.UI : UI) as T;
            if (type == typeof(NetworkServiceConfig))
                return (ConfigAsset != null ? ConfigAsset.Network : Network) as T;
            if (type == typeof(SaveServiceConfig))
                return (ConfigAsset != null ? ConfigAsset.Save : Save) as T;
            if (type == typeof(HotUpdateServiceConfig))
                return (ConfigAsset != null ? ConfigAsset.HotUpdate : HotUpdate) as T;
            if (type == typeof(PoolManagerConfig))
                return (ConfigAsset != null ? ConfigAsset.Pool : Pool) as T;
            if (type == typeof(EntityManagerConfig))
                return (ConfigAsset != null ? ConfigAsset.Entity : Entity) as T;
            if (type == typeof(ResourceLoaderConfig))
                return (ConfigAsset != null ? ConfigAsset.Resource : Resource) as T;
            if (type == typeof(TimerManagerConfig))
                return (ConfigAsset != null ? ConfigAsset.Timer : Timer) as T;
            if (type == typeof(ProcedureManagerConfig))
                return (ConfigAsset != null ? ConfigAsset.Procedure : Procedure) as T;
            if (type == typeof(DebugToolsConfig))
                return (ConfigAsset != null ? ConfigAsset.Debug : Debug) as T;
            
            return null;
        }
        
        /// <summary>
        /// 单例访问
        /// </summary>
        public static CYConfigurator Instance { get; private set; }
        
        /// <summary>
        /// Unity Awake
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }
        
        /// <summary>
        /// Unity OnDestroy
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// 从 ConfigAsset 复制配置到内联字段（编辑器用）
        /// </summary>
        [ContextMenu("从配置文件复制到内联配置")]
        private void CopyFromAsset()
        {
            if (ConfigAsset == null) return;
            
            Bootstrap = ConfigAsset.Bootstrap;
            Log = ConfigAsset.Log;
            Audio = ConfigAsset.Audio;
            UI = ConfigAsset.UI;
            Network = ConfigAsset.Network;
            Save = ConfigAsset.Save;
            HotUpdate = ConfigAsset.HotUpdate;
            Pool = ConfigAsset.Pool;
            Entity = ConfigAsset.Entity;
            Resource = ConfigAsset.Resource;
            Timer = ConfigAsset.Timer;
            Procedure = ConfigAsset.Procedure;
            Debug = ConfigAsset.Debug;
            
            UnityEngine.Debug.Log("[CYConfigurator] 配置已复制");
        }
        #endif
    }
}
