// ============================================================================
// CYFramework - 服务基类
// 提供所有生命周期的默认实现，子类选择性重写
// ============================================================================

using System;

namespace CYFramework.Infrastructure
{
    /// <summary>
    /// 服务基类
    /// 继承后只需重写你需要的方法
    /// </summary>
    public abstract class ServiceBase : IInitializable, ITickable, IUpdateable, ILateUpdateable, IPausable, IDisposableEx
    {
        // ==================== 优先级（可重写） ====================
        
        public virtual int InitOrder => 0;
        public virtual int TickOrder => 0;
        public virtual int UpdateOrder => 0;
        public virtual int LateUpdateOrder => 0;
        public virtual int DisposeOrder => 0;
        
        // ==================== 生命周期（选择性重写） ====================
        
        /// <summary>
        /// 初始化（可选重写）
        /// </summary>
        public virtual void Initialize() { }
        
        /// <summary>
        /// 固定帧更新 - FixedUpdate（可选重写）
        /// </summary>
        public virtual void Tick(float deltaTime) { }
        
        /// <summary>
        /// 每帧更新 - Update（可选重写）
        /// </summary>
        public virtual void OnUpdate(float deltaTime) { }
        
        /// <summary>
        /// 延迟更新 - LateUpdate（可选重写）
        /// </summary>
        public virtual void OnLateUpdate(float deltaTime) { }
        
        /// <summary>
        /// 暂停回调（可选重写）
        /// </summary>
        public virtual void OnPause() { }
        
        /// <summary>
        /// 恢复回调（可选重写）
        /// </summary>
        public virtual void OnResume(float pauseDuration) { }
        
        /// <summary>
        /// 销毁清理（可选重写）
        /// </summary>
        public virtual void Dispose() { }
        
        // ==================== 便捷属性 ====================
        
        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized { get; protected set; }
        
        /// <summary>
        /// 是否已销毁
        /// </summary>
        public bool IsDisposed { get; protected set; }
    }
    
    /// <summary>
    /// MonoBehaviour 服务基类
    /// 用于需要挂载到 GameObject 的服务
    /// </summary>
    public abstract class MonoServiceBase : UnityEngine.MonoBehaviour, IInitializable, IUpdateable, IPausable
    {
        public virtual int InitOrder => 0;
        public virtual int UpdateOrder => 0;
        
        public virtual void Initialize() { }
        public virtual void OnUpdate(float deltaTime) { }
        public virtual void OnPause() { }
        public virtual void OnResume(float pauseDuration) { }
    }
}
