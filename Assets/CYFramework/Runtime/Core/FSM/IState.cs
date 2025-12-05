// ============================================================================
// CYFramework - 状态接口
// 有限状态机的状态基础接口
// ============================================================================

namespace CYFramework.Runtime.Core.FSM
{
    /// <summary>
    /// 状态接口
    /// </summary>
    /// <typeparam name="T">状态机所有者类型</typeparam>
    public interface IState<T>
    {
        /// <summary>
        /// 进入状态
        /// </summary>
        /// <param name="owner">状态机所有者</param>
        void OnEnter(T owner);

        /// <summary>
        /// 状态更新
        /// </summary>
        /// <param name="owner">状态机所有者</param>
        /// <param name="deltaTime">帧间隔时间</param>
        void OnUpdate(T owner, float deltaTime);

        /// <summary>
        /// 退出状态
        /// </summary>
        /// <param name="owner">状态机所有者</param>
        void OnExit(T owner);
    }

    /// <summary>
    /// 状态基类
    /// 提供默认的空实现
    /// </summary>
    /// <typeparam name="T">状态机所有者类型</typeparam>
    public abstract class StateBase<T> : IState<T>
    {
        public virtual void OnEnter(T owner) { }
        public virtual void OnUpdate(T owner, float deltaTime) { }
        public virtual void OnExit(T owner) { }
    }
}
