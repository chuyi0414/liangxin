// ============================================================================
// CYFramework - 模块接口定义
// 所有框架模块都需要实现此接口，由 ModuleManager 统一管理生命周期
// ============================================================================

namespace CYFramework.Runtime.Core
{
    /// <summary>
    /// 框架模块接口
    /// 定义模块的基本生命周期：初始化、更新、关闭
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// 模块优先级，数值越小越先初始化和更新
        /// 建议范围：0-1000，核心模块用 0-100，业务模块用 100+
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 是否需要每帧更新
        /// 对于不需要帧更新的模块（如配置、存档），返回 false 可减少开销
        /// </summary>
        bool NeedUpdate { get; }

        /// <summary>
        /// 模块初始化
        /// 在框架启动时由 ModuleManager 按优先级顺序调用
        /// </summary>
        void Initialize();

        /// <summary>
        /// 模块帧更新
        /// 仅当 NeedUpdate 为 true 时，每帧由 ModuleManager 调用
        /// </summary>
        /// <param name="deltaTime">距离上一帧的时间（秒）</param>
        void Update(float deltaTime);

        /// <summary>
        /// 模块关闭
        /// 在框架退出或场景切换时由 ModuleManager 调用
        /// 用于释放资源、取消事件订阅等
        /// </summary>
        void Shutdown();
    }
}
