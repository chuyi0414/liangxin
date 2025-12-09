// ============================================================================
// CYFramework - 统一入口
// 类似 GameFramework 的 GameEntry，提供简洁的 API
// ============================================================================

using System;
using CYFramework.Core.Event;
using CYFramework.Core.FSM;
using CYFramework.Core.DataTable;
using CYFramework.Core.Entity;
using CYFramework.Core.Procedure;
using CYFramework.Core.Timer;
using CYFramework.Infrastructure;

namespace CYFramework
{
    /// <summary>
    /// CYFramework 统一入口
    /// </summary>
    public static class CY
    {
        // ==================== 核心服务 ====================
        
        /// <summary>
        /// 事件系统
        /// </summary>
        public static class Event
        {
            private static EventBus _eventBus;
            private static EventBus EventBus => _eventBus ??= ServiceLocator.Get<EventBus>();
            
            /// <summary>
            /// 订阅事件
            /// </summary>
            public static void Subscribe<T>(Core.Event.EventHandler<T> handler, object owner = null) where T : struct
            {
                EventBus.Subscribe(handler, owner);
            }
            
            /// <summary>
            /// 取消订阅
            /// </summary>
            public static void Unsubscribe<T>(Core.Event.EventHandler<T> handler) where T : struct
            {
                EventBus.Unsubscribe(handler);
            }
            
            /// <summary>
            /// 发布事件
            /// </summary>
            public static void Fire<T>(T evt) where T : struct
            {
                EventBus.Post(ref evt);
            }
            
            /// <summary>
            /// 发布事件（ref 版本，避免装箱）
            /// </summary>
            public static void Fire<T>(ref T evt) where T : struct
            {
                EventBus.Post(ref evt);
            }
            
            /// <summary>
            /// 取消所有订阅
            /// </summary>
            public static void UnsubscribeAll(object owner)
            {
                EventBus.UnsubscribeAll(owner);
            }
            
            /// <summary>
            /// 自动扫描并订阅所有标记 [OnEvent] 的方法
            /// </summary>
            public static void SubscribeAll(object target)
            {
                EventBus.SubscribeAll(target);
            }
        }
        
        /// <summary>
        /// 日志系统
        /// </summary>
        public static class Log
        {
            public static void Info(string message) => CYLog.Info(message);
            public static void Warning(string message) => CYLog.Warning(message);
            public static void Error(string message) => CYLog.Error(message);
            public static void Info(string tag, string message) => CYLog.Info($"[{tag}] {message}");
        }
        
        /// <summary>
        /// 计时器系统
        /// </summary>
        public static class Timer
        {
            private static TimerManager _timerManager;
            internal static TimerManager Manager => _timerManager ??= GetOrCreateTimerManager();
            
            private static TimerManager GetOrCreateTimerManager()
            {
                var manager = ServiceLocator.Get<TimerManager>();
                if (manager == null)
                {
                    manager = new TimerManager();
                    ServiceLocator.RegisterInstance(manager);
                    // 注册生命周期
                    CYBootstrap.Instance?.RegisterLifecycle(manager);
                }
                return manager;
            }
            
            /// <summary>
            /// 延迟执行
            /// </summary>
            public static Core.Timer.Timer Delay(float seconds, Action onComplete, bool useUnscaledTime = false)
            {
                return Manager.Delay(seconds, onComplete, useUnscaledTime);
            }
            
            /// <summary>
            /// 循环执行
            /// </summary>
            public static Core.Timer.Timer Loop(float interval, Action onTick, bool useUnscaledTime = false)
            {
                return Manager.Loop(interval, onTick, useUnscaledTime);
            }
            
            /// <summary>
            /// 下一帧执行
            /// </summary>
            public static Core.Timer.Timer NextFrame(Action onComplete)
            {
                return Manager.NextFrame(onComplete);
            }
            
            /// <summary>
            /// 取消所有计时器
            /// </summary>
            public static void CancelAll()
            {
                Manager.CancelAll();
            }
        }
        
        /// <summary>
        /// 流程系统
        /// </summary>
        public static class Procedure
        {
            private static ProcedureManager _procedureManager;
            internal static ProcedureManager Manager => _procedureManager ??= GetOrCreateProcedureManager();
            
            private static ProcedureManager GetOrCreateProcedureManager()
            {
                var manager = ServiceLocator.Get<ProcedureManager>();
                if (manager == null)
                {
                    manager = new ProcedureManager();
                    ServiceLocator.RegisterInstance(manager);
                    // 注册生命周期
                    CYBootstrap.Instance?.RegisterLifecycle(manager);
                }
                return manager;
            }
            
            /// <summary>
            /// 注册流程
            /// </summary>
            public static void Add<T>(string name = null) where T : ProcedureBase, new()
            {
                Manager.AddProcedure<T>(name);
            }
            
            /// <summary>
            /// 自动扫描注册所有标记 [AutoRegisterProcedure] 的流程
            /// </summary>
            public static void AutoRegisterAll(System.Reflection.Assembly assembly = null)
            {
                Manager.AutoRegisterAll(assembly);
            }
            
            /// <summary>
            /// 启动流程系统
            /// </summary>
            public static void Start<T>() where T : ProcedureBase
            {
                Manager.Start<T>();
            }
            
            /// <summary>
            /// 按名称启动流程系统
            /// </summary>
            public static void Start(string procedureName)
            {
                Manager.Start(procedureName);
            }
            
            /// <summary>
            /// 切换流程
            /// </summary>
            public static void Change<T>() where T : ProcedureBase
            {
                Manager.ChangeProcedure<T>();
            }
            
            /// <summary>
            /// 切换流程（带参数）
            /// </summary>
            public static void Change<T>(object userData) where T : ProcedureBase
            {
                Manager.ChangeProcedure<T>(userData);
            }
            
            /// <summary>
            /// 按名称切换流程
            /// </summary>
            public static void Change(string procedureName, object userData = null)
            {
                Manager.Change(procedureName, userData);
            }
            
            /// <summary>
            /// 获取当前流程
            /// </summary>
            public static ProcedureBase Current => Manager.CurrentProcedure;
            
            /// <summary>
            /// 获取当前流程名称
            /// </summary>
            public static string CurrentName => Manager.CurrentProcedureName;
        }
        
        /// <summary>
        /// 实体系统
        /// </summary>
        public static class Entity
        {
            private static EntityManager _entityManager;
            internal static EntityManager Manager => _entityManager ??= GetOrCreateEntityManager();
            
            private static EntityManager GetOrCreateEntityManager()
            {
                var manager = ServiceLocator.Get<EntityManager>();
                if (manager == null)
                {
                    manager = new EntityManager();
                    manager.Initialize();
                    ServiceLocator.RegisterInstance(manager);
                    CYBootstrap.Instance?.RegisterLifecycle(manager);
                }
                return manager;
            }
            
            /// <summary>
            /// 注册实体类型
            /// </summary>
            public static void Register(string entityType, UnityEngine.GameObject prefab, int preloadCount = 0)
            {
                Manager.RegisterEntity(entityType, prefab, preloadCount);
            }
            
            /// <summary>
            /// 显示实体
            /// </summary>
            public static T Show<T>(string entityType, object userData = null) where T : class, IEntity
            {
                return Manager.ShowEntity<T>(entityType, userData);
            }
            
            /// <summary>
            /// 显示实体
            /// </summary>
            public static IEntity Show(string entityType, object userData = null)
            {
                return Manager.ShowEntity(entityType, userData);
            }
            
            /// <summary>
            /// 隐藏实体
            /// </summary>
            public static void Hide(int entityId) => Manager.HideEntity(entityId);
            
            /// <summary>
            /// 隐藏实体
            /// </summary>
            public static void Hide(IEntity entity) => Manager.HideEntity(entity);
            
            /// <summary>
            /// 隐藏所有指定类型的实体
            /// </summary>
            public static void HideAll(string entityType) => Manager.HideAllEntities(entityType);
            
            /// <summary>
            /// 隐藏所有实体
            /// </summary>
            public static void HideAll() => Manager.HideAllEntities();
            
            /// <summary>
            /// 获取实体
            /// </summary>
            public static T Get<T>(int entityId) where T : class, IEntity => Manager.GetEntity<T>(entityId);
            
            /// <summary>
            /// 获取实体数量
            /// </summary>
            public static int Count(string entityType = null) => Manager.GetEntityCount(entityType);
        }
        
        /// <summary>
        /// 数据表系统
        /// </summary>
        public static class Data
        {
            private static DataTableManager _dataTableManager;
            internal static DataTableManager Manager => _dataTableManager ??= GetOrCreateDataTableManager();
            
            private static DataTableManager GetOrCreateDataTableManager()
            {
                var manager = ServiceLocator.Get<DataTableManager>();
                if (manager == null)
                {
                    manager = new DataTableManager();
                    ServiceLocator.RegisterInstance(manager);
                    CYBootstrap.Instance?.RegisterLifecycle(manager);
                }
                return manager;
            }
            
            /// <summary>
            /// 创建数据表
            /// </summary>
            public static DataTable<T> Create<T>(string name = null) where T : class, IDataRow, new()
            {
                return Manager.CreateDataTable<T>(name);
            }
            
            /// <summary>
            /// 获取数据表
            /// </summary>
            public static DataTable<T> GetTable<T>(string name = null) where T : class, IDataRow, new()
            {
                return Manager.GetDataTable<T>(name);
            }
            
            /// <summary>
            /// 从 CSV 加载
            /// </summary>
            public static DataTable<T> LoadCsv<T>(string csvText, string name = null) where T : class, IDataRow, new()
            {
                return Manager.LoadFromCsv<T>(csvText, name);
            }
            
            /// <summary>
            /// 获取数据行
            /// </summary>
            public static T GetRow<T>(int id, string tableName = null) where T : class, IDataRow, new()
            {
                return Manager.GetDataTable<T>(tableName)?.GetRow(id);
            }
        }
        
        // ==================== 服务定位器快捷方法 ====================
        
        /// <summary>
        /// 获取服务
        /// </summary>
        public static T Get<T>() where T : class
        {
            return ServiceLocator.Get<T>();
        }
        
        /// <summary>
        /// 注册服务
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            ServiceLocator.RegisterInstance(service);
        }
        
        // ==================== 游戏入口 ====================
        
        /// <summary>
        /// 游戏入口快捷访问
        /// </summary>
        public static class Game
        {
            /// <summary>
            /// 获取游戏入口实例
            /// </summary>
            public static Core.GameEntryBase Entry => Core.GameEntryBase.Instance;
            
            /// <summary>
            /// 获取类型化的游戏入口
            /// </summary>
            public static T GetEntry<T>() where T : Core.GameEntryBase => Core.GameEntryBase.Get<T>();
        }
    }
}
