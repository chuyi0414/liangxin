// ============================================================================
// CYFramework 2.2 - 生命周期接口定义
// 文档位置：3.1.2 基础设施 - ServiceLocator 生命周期管理
// ============================================================================

namespace CYFramework.Infrastructure
{
    /// <summary>
    /// 可初始化接口
    /// 在 ServiceLocator 注册后按依赖顺序调用
    /// </summary>
    public interface IInitializable
    {
        /// <summary>
        /// 初始化优先级，数值越小越先执行
        /// </summary>
        int InitOrder => 0;
        
        /// <summary>
        /// 初始化方法
        /// </summary>
        void Initialize();
    }
    
    /// <summary>
    /// 可逻辑帧更新接口 (FixedUpdate)
    /// 用于物理、AI、状态机等固定帧率逻辑
    /// </summary>
    public interface ITickable
    {
        /// <summary>
        /// Tick 优先级
        /// </summary>
        int TickOrder => 0;
        
        /// <summary>
        /// 固定逻辑帧更新 (30/60Hz)
        /// </summary>
        /// <param name="deltaTime">固定时间步长</param>
        void Tick(float deltaTime);
    }
    
    /// <summary>
    /// 可渲染帧更新接口 (Update)
    /// 用于输入收集、UI 更新、渲染插值
    /// </summary>
    public interface IUpdateable
    {
        /// <summary>
        /// Update 优先级
        /// </summary>
        int UpdateOrder => 0;
        
        /// <summary>
        /// 渲染帧更新 (变帧率)
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        void OnUpdate(float deltaTime);
    }
    
    /// <summary>
    /// 可延迟更新接口 (LateUpdate)
    /// 用于相机跟随、Job Complete 等
    /// </summary>
    public interface ILateUpdateable
    {
        /// <summary>
        /// LateUpdate 优先级
        /// </summary>
        int LateUpdateOrder => 0;
        
        /// <summary>
        /// 延迟更新
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        void OnLateUpdate(float deltaTime);
    }
    
    /// <summary>
    /// 可销毁接口
    /// 扩展 IDisposable，支持优先级
    /// </summary>
    public interface IDisposableEx : System.IDisposable
    {
        /// <summary>
        /// 销毁优先级，数值越大越先销毁（与初始化相反）
        /// </summary>
        int DisposeOrder => 0;
    }
    
    /// <summary>
    /// 可暂停接口
    /// 用于处理游戏暂停/恢复（如微信切后台）
    /// </summary>
    public interface IPausable
    {
        /// <summary>
        /// 暂停回调
        /// </summary>
        void OnPause();
        
        /// <summary>
        /// 恢复回调
        /// </summary>
        /// <param name="pauseDuration">暂停时长（秒）</param>
        void OnResume(float pauseDuration);
    }
    
    /// <summary>
    /// 服务作用域
    /// </summary>
    public enum ServiceScope
    {
        /// <summary>
        /// 全局单例，整个应用生命周期
        /// </summary>
        Singleton,
        
        /// <summary>
        /// 场景级，场景切换时销毁
        /// </summary>
        Scoped,
        
        /// <summary>
        /// 瞬态，每次获取都新建
        /// </summary>
        Transient
    }
}
