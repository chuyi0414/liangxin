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
        [Tooltip("引用 ScriptableObject 配置文件，留空则使用默认配置")]
        public CYFrameworkConfig ConfigAsset;
        
        [Header("=== 或使用内联配置 ===")]
        [Space(10)]
        
        [Header("框架启动")]
        public BootstrapConfig Bootstrap = new();
        
        [Header("日志")]
        public LogServiceConfig Log = new();
        
        [Header("音频")]
        public AudioConfig Audio = new();
        
        [Header("UI")]
        public UIManagerConfig UI = new();
        
        [Header("网络")]
        public NetworkServiceConfig Network = new();
        
        [Header("存档")]
        public SaveServiceConfig Save = new();
        
        [Header("热更新")]
        public HotUpdateServiceConfig HotUpdate = new();
        
        [Header("对象池")]
        public PoolManagerConfig Pool = new();
        
        [Header("实体")]
        public EntityManagerConfig Entity = new();
        
        [Header("资源")]
        public ResourceLoaderConfig Resource = new();
        
        [Header("计时器")]
        public TimerManagerConfig Timer = new();
        
        [Header("流程")]
        public ProcedureManagerConfig Procedure = new();
        
        [Header("调试")]
        public DebugToolsConfig Debug = new();
        
        /// <summary>
        /// 获取配置（优先使用 ConfigAsset）
        /// </summary>
        public T GetConfig<T>() where T : class
        {
            var type = typeof(T);
            
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
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }
        
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
