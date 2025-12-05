// ============================================================================
// CYFramework - 全局快捷访问类
// 简化 CYFrameworkEntry.Instance.xxx 的调用
// ============================================================================

using CYFramework.Runtime.Core.UI;
using CYFramework.Runtime.Gameplay.Abstraction;

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 框架全局快捷访问
    /// 用法：CYFW.Timer.Delay(1f, DoSomething);
    /// </summary>
    public static class CYFW
    {
        /// <summary>
        /// 框架实例
        /// </summary>
        public static CYFrameworkEntry Instance => CYFrameworkEntry.Instance;

        /// <summary>
        /// 框架是否已初始化
        /// </summary>
        public static bool IsReady => Instance != null && Instance.IsInitialized;

        // ====================================================================
        // 核心模块快捷访问
        // ====================================================================

        /// <summary>
        /// 日志模块
        /// </summary>
        public static LogModule Log => Instance.Log;

        /// <summary>
        /// 事件模块
        /// </summary>
        public static EventModule Event => Instance.Event;

        /// <summary>
        /// 定时器模块
        /// </summary>
        public static TimerModule Timer => Instance.Timer;

        /// <summary>
        /// 对象池模块
        /// </summary>
        public static ObjectPoolModule Pool => Instance.Pool;

        /// <summary>
        /// 资源模块
        /// </summary>
        public static ResourceModule Resource => Instance.Resource;

        /// <summary>
        /// 存储模块
        /// </summary>
        public static StorageModule Storage => Instance.Storage;

        /// <summary>
        /// 声音模块
        /// </summary>
        public static SoundModule Sound => Instance.Sound;

        /// <summary>
        /// UI 模块
        /// </summary>
        public static UIModule UI => Instance.UI;

        /// <summary>
        /// 流程模块
        /// </summary>
        public static ProcedureModule Procedure => Instance.Procedure;

        /// <summary>
        /// 分帧调度模块
        /// </summary>
        public static SchedulerModule Scheduler => Instance.Scheduler;

        /// <summary>
        /// 玩法世界
        /// </summary>
        public static IGameplayWorld World => Instance.GameplayWorld;
    }
}
