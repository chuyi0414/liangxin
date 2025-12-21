// ============================================================================
// CYFramework - 游戏入口基类
// 用户继承此类，实现游戏初始化逻辑
// ============================================================================

using CYFramework.Core.Event;
using CYFramework.Core.Procedure;
using CYFramework.Infrastructure;
using UnityEngine;

namespace CYFramework.Core
{
    /// <summary>
    /// 游戏入口基类
    /// 继承此类来创建你的游戏入口
    /// </summary>
    [DefaultExecutionOrder(100)] // 确保在 CYBootstrap(-1000) 之后执行
    public abstract class GameEntryBase : MonoBehaviour
    {
        // ==================== 配置 ====================
        
        /// <summary>
        /// 是否自动注册流程（扫描 [AutoRegisterProcedure] 特性）
        /// </summary>
        protected virtual bool AutoRegisterProcedures => false;
        
        /// <summary>
        /// 是否自动订阅事件（扫描 [OnEvent] 特性）
        /// </summary>
        protected virtual bool AutoSubscribeEvents => true;

        // ==================== 单例 ====================
        
        /// <summary>
        /// 全局入口实例
        /// </summary>
        private static GameEntryBase _instance;
        /// <summary>
        /// 获取入口实例
        /// </summary>
        public static GameEntryBase Instance => _instance;
        
        /// <summary>
        /// 获取类型化的实例
        /// </summary>
        public static T Get<T>() where T : GameEntryBase => _instance as T;
        
        // ==================== 服务引用 ====================
        
        /// <summary>
        /// 事件总线引用
        /// </summary>
        protected EventBus EventBus { get; private set; }
        /// <summary>
        /// 流程管理器引用
        /// </summary>
        protected ProcedureManager ProcedureManager { get; private set; }
        
        // ==================== 生命周期 ====================
        
        /// <summary>
        /// Unity Awake
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        
        /// <summary>
        /// Unity Start
        /// </summary>
        protected virtual void Start()
        {
            InitializeFrameworkServices();
            OnGameInit();
            
            // 自动或手动注册流程
            if (AutoRegisterProcedures)
            {
                CY.Procedure.AutoRegisterAll();
            }
            else
            {
                RegisterProcedures();
            }
            
            // 自动订阅事件
            if (AutoSubscribeEvents)
            {
                EventBus?.SubscribeAll(this);
            }
            
            OnGameStart();
        }
        
        /// <summary>
        /// Unity OnDestroy
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                OnGameShutdown();
                EventBus?.UnsubscribeAll(this);
                _instance = null;
            }
        }
        
        // ==================== 框架服务初始化 ====================
        
        /// <summary>
        /// 初始化框架服务引用
        /// </summary>
        private void InitializeFrameworkServices()
        {
            EventBus = ServiceLocator.Get<EventBus>();
            // 流程管理器实例
            ServiceLocator.TryGet(out ProcedureManager _procedureManager);
            ProcedureManager = _procedureManager;
            
            CYLog.Info("=== 游戏初始化开始 ===");
        }
        
        // ==================== 子类实现 ====================
        
        /// <summary>
        /// 游戏初始化（注册服务、加载配置）
        /// </summary>
        protected abstract void OnGameInit();
        
        /// <summary>
        /// 注册游戏流程（AutoRegisterProcedures=false 时必须实现）
        /// </summary>
        protected virtual void RegisterProcedures() { }
        
        /// <summary>
        /// 游戏启动（启动第一个流程）
        /// </summary>
        protected abstract void OnGameStart();
        
        /// <summary>
        /// 游戏关闭
        /// </summary>
        protected virtual void OnGameShutdown()
        {
            CYLog.Info("=== 游戏关闭 ===");
        }
        
        // ==================== 便捷方法 ====================
        
        /// <summary>
        /// 发送事件
        /// </summary>
        protected void FireEvent<T>(ref T evt) where T : struct
        {
            EventBus?.Post(ref evt);
        }
        
        /// <summary>
        /// 订阅事件
        /// </summary>
        protected void Subscribe<T>(EventHandler<T> handler) where T : struct
        {
            EventBus?.Subscribe(handler, this);
        }
        
        /// <summary>
        /// 切换流程
        /// </summary>
        protected void ChangeProcedure<T>(object userData = null) where T : ProcedureBase, new()
        {
            CY.Procedure.ChangeProcedure<T>(userData);
        }
        
        /// <summary>
        /// 按名称切换流程
        /// </summary>
        protected void ChangeProcedure(string procedureName, object userData = null)
        {
            CY.Procedure.Change(procedureName, userData);
        }
    }
}
