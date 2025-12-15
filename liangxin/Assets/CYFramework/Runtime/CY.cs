// ============================================================================
// CYFramework - 统一入口（仅聚合服务访问）
// 说明：
// - 这里不堆叠大量“快捷方法”，避免入口类无限膨胀、难维护。
// - 需要便捷 API 请优先补充到对应 Manager/Service 内部，或用 partial 在项目侧扩展。
// ============================================================================

using CYFramework.Core.Audio;
using CYFramework.Core.DataTable;
using CYFramework.Core.Entity;
using CYFramework.Core.Event;
using CYFramework.Core.FSM;
using CYFramework.Core.Network;
using CYFramework.Core.Pool;
using CYFramework.Core.Procedure;
using CYFramework.Core.Resource;
using CYFramework.Core.Save;
using CYFramework.Core.Scene;
using CYFramework.Core.Timer;
using CYFramework.Core.UI;
using CYFramework.Infrastructure;

namespace CYFramework
{
    /// <summary>
    /// CYFramework 统一入口：提供各 Manager/Service 的访问入口。
    /// </summary>
    public static partial class CY
    {
        private static EventBus _event;
        private static TimerManager _timer;
        private static ProcedureManager _procedure;
        private static EntityManager _entity;
        private static UIManager _ui;
        private static DataTableManager _data;
        private static IAudioService _audio;
        private static NetworkService _network;
        private static SaveService _save;
        private static PoolManager _pool;
        private static IResourceLoader _resource;
        private static SceneLoader _scene;
        private static FSMManager _fsm;

        public static EventBus Event => _event ??= ServiceLocator.Get<EventBus>();
        public static TimerManager Timer => _timer ??= GetOrCreateTimerManager();
        public static ProcedureManager Procedure => _procedure ??= GetOrCreateProcedureManager();
        public static EntityManager Entity => _entity ??= GetOrCreateEntityManager();
        public static UIManager UI => _ui ??= GetOrCreateUIManager();
        public static DataTableManager Data => _data ??= GetOrCreateDataTableManager();
        public static IAudioService Audio => _audio ??= ServiceLocator.Get<IAudioService>();
        public static SaveService Save => _save ??= ServiceLocator.Get<SaveService>();
        public static PoolManager Pool => _pool ??= ServiceLocator.Get<PoolManager>();
        public static IResourceLoader Resource => _resource ??= ServiceLocator.Get<IResourceLoader>();
        public static SceneLoader Scene => _scene ??= ServiceLocator.Get<SceneLoader>();
        public static NetworkService Network => _network ??= ServiceLocator.Get<NetworkService>();
        public static FSMManager FSM => _fsm ??= ServiceLocator.Get<FSMManager>();
        public static Core.GameEntryBase Game => Core.GameEntryBase.Instance;

        /// <summary>
        /// 获取服务（ServiceLocator 快捷入口）
        /// </summary>
        public static T Get<T>() where T : class => ServiceLocator.Get<T>();

        /// <summary>
        /// 注册服务（ServiceLocator 快捷入口）
        /// </summary>
        public static void Register<T>(T service) where T : class => ServiceLocator.RegisterInstance(service);

        // ==================== 日志快捷方法 ====================

        /// <summary>日志 - Debug 级别</summary>
        public static void Log(string message) => CYLog.Debug(message);

        /// <summary>日志 - Info 级别</summary>
        public static void LogInfo(string message) => CYLog.Info(message);

        /// <summary>日志 - Warning 级别</summary>
        public static void LogWarning(string message) => CYLog.Warning(message);

        /// <summary>日志 - Error 级别</summary>
        public static void LogError(string message) => CYLog.Error(message);

        // ==================== 存档快捷方法（与团队口径对齐） ====================

        /// <summary>
        /// 保存存档（等价于 <see cref="SaveService.Save{T}(string,T)"/>）。
        /// </summary>
        public static bool SaveData<T>(string key, T data) where T : SaveDataBase => Save.Save(key, data);

        /// <summary>
        /// 加载存档（等价于 <see cref="SaveService.Load{T}(string)"/>；不存在返回 new T()）。
        /// </summary>
        public static T LoadData<T>(string key) where T : SaveDataBase, new() => Save.Load<T>(key);

        /// <summary>
        /// 存档是否存在（等价于 <see cref="SaveService.Exists(string)"/>）。
        /// </summary>
        public static bool HasSave(string key) => Save.Exists(key);

        /// <summary>
        /// 删除存档（等价于 <see cref="SaveService.Delete(string)"/>）。
        /// </summary>
        public static void DeleteSave(string key) => Save.Delete(key);

        /// <summary>
        /// 保存默认存档（使用 <see cref="SaveService.DefaultSaveKey"/>）。
        /// </summary>
        public static bool SaveData<T>(T data) where T : SaveDataBase => Save.Save(data);

        /// <summary>
        /// 加载默认存档（使用 <see cref="SaveService.DefaultSaveKey"/>）。
        /// </summary>
        public static T LoadData<T>() where T : SaveDataBase, new() => Save.Load<T>();

        /// <summary>
        /// 默认存档是否存在（使用 <see cref="SaveService.DefaultSaveKey"/>）。
        /// </summary>
        public static bool HasSave() => Save.Exists();

        /// <summary>
        /// 删除默认存档（使用 <see cref="SaveService.DefaultSaveKey"/>）。
        /// </summary>
        public static void DeleteSave() => Save.Delete();

        private static TimerManager GetOrCreateTimerManager()
        {
            if (!ServiceLocator.TryGet<TimerManager>(out var manager))
            {
                manager = new TimerManager();
                manager.Initialize(); // 允许在未走 CYBootstrap.InitializeAll 的情况下直接使用
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }

        private static ProcedureManager GetOrCreateProcedureManager()
        {
            if (!ServiceLocator.TryGet<ProcedureManager>(out var manager))
            {
                manager = new ProcedureManager();
                manager.Initialize(); // 允许在未走 CYBootstrap.InitializeAll 的情况下直接使用
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }

        private static EntityManager GetOrCreateEntityManager()
        {
            if (!ServiceLocator.TryGet<EntityManager>(out var manager))
            {
                manager = new EntityManager();
                manager.Initialize();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }

        private static UIManager GetOrCreateUIManager()
        {
            if (!ServiceLocator.TryGet<UIManager>(out var manager))
            {
                manager = new UIManager();
                manager.Initialize();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }

        private static DataTableManager GetOrCreateDataTableManager()
        {
            if (!ServiceLocator.TryGet<DataTableManager>(out var manager))
            {
                manager = new DataTableManager();
                ServiceLocator.RegisterInstance(manager);
                CYBootstrap.Instance?.RegisterLifecycle(manager);
            }
            return manager;
        }
    }
}
